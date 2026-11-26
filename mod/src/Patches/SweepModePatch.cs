using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using System.Runtime.CompilerServices;
using GameRng = MegaCrit.Sts2.Core.Random.Rng;

namespace Yjcm;

/// <summary>
/// 第一回合所有玩家的初始手牌摸取完成后执行扫荡。决定由队伍中首位本 Mod 角色玩家
/// 生成并通过 PlayerChoiceSynchronizer 广播，避免联机玩家的本地设置不同而导致战斗状态分叉。
/// </summary>
[HarmonyPatch(
    typeof(CardPileCmd),
    nameof(CardPileCmd.Draw),
    [typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)])]
internal static class SweepModePatch
{
    private const float EscapeDistance = 2400f;
    private const double EscapeDuration = 1.35;
    private static readonly ConditionalWeakTable<ICombatState, InitialDrawGate> InitialDrawGates = new();
    private static readonly string[] EscapeBanterKeys =
    [
        "YJCM_SWEEP_ESCAPE_1.banter",
        "YJCM_SWEEP_ESCAPE_2.banter",
        "YJCM_SWEEP_ESCAPE_3.banter",
        "YJCM_SWEEP_ESCAPE_4.banter",
        "YJCM_SWEEP_ESCAPE_5.banter",
        "YJCM_SWEEP_ESCAPE_6.banter",
        "YJCM_SWEEP_ESCAPE_7.banter",
        "YJCM_SWEEP_ESCAPE_8.banter"
    ];

    private static void Postfix(
        Player player,
        bool fromHandDraw,
        ref Task<IEnumerable<CardModel>> __result)
    {
        __result = RunAfterInitialDraw(__result, player, fromHandDraw);
    }

    private static async Task<IEnumerable<CardModel>> RunAfterInitialDraw(
        Task<IEnumerable<CardModel>> original,
        Player player,
        bool fromHandDraw)
    {
        IEnumerable<CardModel> drawnCards = await original;
        if (!fromHandDraw || player.PlayerCombatState?.TurnNumber != 1)
            return drawnCards;

        ICombatState combatState = player.Creature.CombatState;
        if (!combatState.IsLiveCombat())
            return drawnCards;

        InitialDrawGate gate = InitialDrawGates.GetValue(combatState, static _ => new InitialDrawGate());
        bool shouldSweep;
        lock (gate)
        {
            gate.CompletedPlayers.Add(player.NetId);
            shouldSweep = !gate.SweepStarted
                && combatState.Players
                    .Where(static candidate => !candidate.Creature.IsDead && candidate.PlayerCombatState != null)
                    .All(candidate => gate.CompletedPlayers.Contains(candidate.NetId));
            if (shouldSweep)
                gate.SweepStarted = true;
        }

        if (!shouldSweep)
            return drawnCards;

        await RunSweep(combatState.RunState, combatState, gate);
        return drawnCards;
    }

    private static async Task RunSweep(
        IRunState runState,
        ICombatState combatState,
        InitialDrawGate gate)
    {
        if (!combatState.IsLiveCombat())
            return;

        Player? controller = combatState.Players
            .Where(IsModCharacter)
            .OrderBy(static player => player.NetId)
            .FirstOrDefault();
        if (controller == null)
            return;

        List<Creature> initialEnemies = combatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue)
            .ToList();
        if (initialEnemies.Count == 0)
            return;

        IReadOnlyList<Creature> escaping = await ResolveEscapingEnemies(
            runState, controller, initialEnemies);
        if (escaping.Count == 0)
            return;

        MainFile.Logger.Info(
            $"扫荡触发：{escaping.Count}/{initialEnemies.Count} 名敌人逃跑，房间类型={runState.CurrentRoom?.RoomType}");

        Creature speaker = escaping[0];
        TalkCmd.Play(
            new LocString(
                "monsters",
            EscapeBanterKeys[GameRng.Chaotic.NextInt(EscapeBanterKeys.Length)]),
            speaker,
            VfxColor.Swamp,
            VfxDuration.Standard);

        foreach (Creature enemy in escaping)
#if GAME_V0113
            NCombatRoom.Instance?.GetCreatureNode(enemy)?.SetIsInteractable(on: false, changeVisibilityImmediately: true);
#else
            NCombatRoom.Instance?.GetCreatureNode(enemy)?.ToggleIsInteractable(on: false);
#endif

        await Cmd.CustomScaledWait(0.35f, 0.65f);
        await Task.WhenAll(escaping.Select(AnimateEscape));

        foreach (Creature enemy in escaping)
        {
            if (enemy.IsAlive && combatState.ContainsCreature(enemy))
            {
                await CreatureCmd.Escape(enemy);
                lock (gate)
                    gate.SweepEscapedEnemies.Add(enemy);
            }
        }

        // CreatureCmd.Escape 只负责移出战斗；显式调用原版胜利检查，确保全部逃跑时
        // 走正常 CombatWon/奖励流程。
        await CombatManager.Instance.CheckWinCondition();
    }

    private static async Task<IReadOnlyList<Creature>> ResolveEscapingEnemies(
        IRunState runState,
        Player controller,
        IReadOnlyList<Creature> enemies)
    {
        if (TestMode.IsOn)
        {
            int testBattleChance = GameRng.Chaotic.NextInt(101);
            int[] testRolls = enemies.Select(_ => GameRng.Chaotic.NextInt(100)).ToArray();
            return BuildLocalResult(runState, enemies, testBattleChance, testRolls);
        }

        uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(controller);
        List<int> escapingIndexes;
        if (LocalContext.IsMe(controller) && RunManager.Instance.NetService.Type != NetGameType.Replay)
        {
            // 只由控制玩家生成随机数并同步最终结果；不消耗游戏本体的 RunRng，
            // 因此即使三档概率均为 0，也不会改变其他原版随机结果。
            int randomBattleChance = GameRng.Chaotic.NextInt(101);
            int[] rolls = enemies.Select(_ => GameRng.Chaotic.NextInt(100)).ToArray();
            escapingIndexes = BuildLocalIndexes(runState, enemies.Count, randomBattleChance, rolls);
            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                controller,
                choiceId,
                PlayerChoiceResult.FromIndexes(escapingIndexes));
        }
        else
        {
            escapingIndexes = (await RunManager.Instance.PlayerChoiceSynchronizer
                    .WaitForRemoteChoice(controller, choiceId))
                .AsIndexes();
        }

        return escapingIndexes
            .Where(index => index >= 0 && index < enemies.Count)
            .Distinct()
            .Select(index => enemies[index])
            .ToList();
    }

    private static IReadOnlyList<Creature> BuildLocalResult(
        IRunState runState,
        IReadOnlyList<Creature> enemies,
        int randomBattleChance,
        IReadOnlyList<int> rolls) =>
        BuildLocalIndexes(runState, enemies.Count, randomBattleChance, rolls)
            .Select(index => enemies[index])
            .ToList();

    private static List<int> BuildLocalIndexes(
        IRunState runState,
        int enemyCount,
        int randomBattleChance,
        IReadOnlyList<int> rolls)
    {
        int chance = WuZhugeliangSettings.CurrentSweepMode switch
        {
            WuZhugeliangSettings.SweepModeOneClick => 100,
            WuZhugeliangSettings.SweepModeRandomBattle => randomBattleChance,
            _ => GetCustomChance(runState.CurrentRoom?.RoomType)
        };

        List<int> result = [];
        for (int i = 0; i < enemyCount; i++)
        {
            if (rolls[i] < chance)
                result.Add(i);
        }

        MainFile.Logger.Info(
            $"扫荡判定：模式={WuZhugeliangSettings.CurrentSweepMode}，本场概率={chance}%");
        return result;
    }

    private static int GetCustomChance(RoomType? roomType) => roomType switch
    {
        RoomType.Elite => WuZhugeliangSettings.SweepEliteChance,
        RoomType.Boss => WuZhugeliangSettings.SweepBossChance,
        _ => WuZhugeliangSettings.SweepNormalChance
    };

    private static bool IsModCharacter(Player player) =>
        WuZhugeliangSettings.MatchesCharacterScope(WuZhugeliangSettings.OwnModCharacters, player.Character.GetType());

    private static async Task AnimateEscape(Creature enemy)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(enemy);
        if (node == null)
            return;

        Tween tween = node.CreateTween();
        tween.TweenProperty(node, "position:x", node.Position.X + EscapeDistance, EscapeDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        await node.ToSignal(tween, Tween.SignalName.Finished);
    }

    private sealed class InitialDrawGate
    {
        public HashSet<ulong> CompletedPlayers { get; } = [];
        public HashSet<Creature> SweepEscapedEnemies { get; } = [];
        public bool SweepStarted { get; set; }
    }

    /// <summary>
    /// 原版普通战斗会按逃跑敌人的比例扣减金币。扫荡是本 Mod 的胜利方式，因此只把
    /// 被扫荡赶跑的敌人补回为“已击败”；其他原生逃跑仍维持原版扣减。
    /// </summary>
    [HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CalculateGoldProportion))]
    private static class SweepGoldRewardPatch
    {
        private static void Postfix(
            EncounterModel __instance,
            CombatState combatState,
            ref float __result)
        {
            if (__instance.SpawnedEnemies.Count == 0
                || !InitialDrawGates.TryGetValue(combatState, out InitialDrawGate? gate))
                return;

            int creditedEscapes;
            lock (gate)
            {
                creditedEscapes = combatState.EscapedCreatures
                    .Count(gate.SweepEscapedEnemies.Contains);
            }

            if (creditedEscapes == 0)
                return;

            float original = __result;
            __result = Math.Clamp(
                __result + (float)creditedEscapes / __instance.SpawnedEnemies.Count,
                0f,
                1f);
            MainFile.Logger.Info(
                $"扫荡金币修正：扫荡逃跑={creditedEscapes}，金币比例={original:P0}->{__result:P0}");
        }
    }
}
