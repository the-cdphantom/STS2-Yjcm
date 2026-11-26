using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace Yjcm;

// Opt-in, isolated in-memory model fixtures. Never attaches a run to RunManager,
// starts a combat room, modifies TestMode, or loads/saves the user's run.
internal static class WeiDongZhuoGuangYongPreflight
{
    private sealed class FixtureState(IRunState run) : CombatState(runState: run), ICombatState
    {
        // Native AttackCommand permits isolated non-live simulation outside a room.
        bool ICombatState.IsLiveCombat() => false;
    }
    internal static async Task Check()
    {
        int checks = 0;
        void Require(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("GuangYong fixture: " + name);
            checks++;
        }
        var player = Player.CreateForNewRun<WeiDongZhuo>(UnlockState.all, 1);
        var ally = Player.CreateForNewRun<Ironclad>(UnlockState.all, 2);
        var run = RunState.CreateForTest([player, ally], seed: "GUANGYONG");
        var state = new FixtureState(run);
        state.AddPlayer(player);
        state.AddPlayer(ally);
        player.ResetCombatState();
        ally.ResetCombatState();
        var enemies = Enumerable.Range(0, 3).Select(i =>
        {
            var enemy = state.CreateCreature(ModelDb.Monster<BigDummy>().ToMutable(), CombatSide.Enemy, "fixture" + i);
            state.AddCreature(enemy);
            return enemy;
        }).ToArray();
        var self = player.Creature;
        var relic = player.GetRelic<WeiDongZhuoGuangYong>()!;
        var context = new ThrowingPlayerChoiceContext();
        var strike = state.CreateCard<StrikeIronclad>(player);
        CardPlay Play(CardModel card, Creature? target, int index = 0) => new()
        {
            Card = card, Target = target, PlayIndex = index, PlayCount = 2,
#if GAME_BETA
            Player = card.Owner,
#endif
            IsAutoPlay = index > 0, ResultPile = PileType.Discard, Resources = default
        };
        void Raw(Type type, Creature creature, int amount) => ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).ToMutable().ApplyInternal(creature, amount, true);
        int Amount<T>(Creature creature) where T : PowerModel => creature.GetPower<T>()?.Amount ?? 0;
        async Task Reset()
        {
            foreach (var creature in enemies.Append(self)) creature.RemoveAllPowersInternalExcept();
            self.SetMaxHpInternal(80);
            self.SetCurrentHpInternal(80);
            await relic.BeforeCombatStart();
        }

        foreach (var type in WeiDongZhuoGuangYongBase.StealPriority(false))
        {
            await Reset();
            Raw(type, enemies[0], 3);
            await relic.BeforeCardPlayed(Play(strike, enemies[0]));
            bool cap = type == typeof(HardToKillPower) || type == typeof(HardenedShellPower);
            Require(enemies[0].GetPower(ModelDb.GetId(type))!.Amount == (cap ? 3 : 2), "enemy amount " + type.Name);
            Require(self.GetPower(ModelDb.GetId(type))!.Amount == (cap ? 3 : 1), "player amount " + type.Name);
            if (type == typeof(BarricadePower))
                Require(!self.GetPower<BarricadePower>()!.ShouldClearBlock(self), "Barricade retains player block");
            Require(self.MaxHp == 80 && self.CurrentHp == 80, "full HP free steal " + type.Name);
        }
        await Reset();
        Raw(typeof(StrengthPower), enemies[0], 2);
        Raw(typeof(ArtifactPower), enemies[0], 3);
        self.SetCurrentHpInternal(75);
        await relic.BeforeCardPlayed(Play(strike, enemies[0]));
        await relic.BeforeCardPlayed(Play(strike, enemies[0], 1));
        Require(Amount<StrengthPower>(enemies[0]) == 0 && Amount<StrengthPower>(self) == 2, "replay steals again");
        Require(Amount<ArtifactPower>(enemies[0]) == 3 && self.MaxHp == 78, "artifact did not block Strength theft");
        await relic.BeforeCardPlayed(Play(strike, enemies[0]));
        Require(Amount<ArtifactPower>(enemies[0]) == 2 && Amount<ArtifactPower>(self) == 1, "next priority after zero");
        await Reset();
        Raw(typeof(StrengthPower), enemies[0], -2);
        await relic.BeforeCardPlayed(Play(strike, enemies[0]));
        Require(Amount<StrengthPower>(enemies[0]) == -2 && self.MaxHp == 80, "negative not stolen");

        await Reset();
        self.SetCurrentHpInternal(75);
        foreach (var enemy in enemies) Raw(typeof(DexterityPower), enemy, 2);
        await relic.BeforeCardPlayed(Play(state.CreateCard<WeiDongZhuoWuSeSha>(player), null));
        Require(self.MaxHp == 77 && self.CurrentHp == 75 && Amount<DexterityPower>(self) == 3, "AOE per-enemy cost");
        await relic.BeforeCardPlayed(Play(state.CreateCard<WeiDongZhuoWuSeSha>(player), null));
        Require(self.MaxHp == 75 && self.CurrentHp == 75 && Amount<DexterityPower>(self) == 6, "AOE reaches full HP then stops cost");

        await Reset();
        Raw(typeof(StrengthPower), enemies[0], 7);
        Raw(typeof(FlexPotionPower), enemies[0], 7);
        await relic.BeforeCardPlayed(Play(strike, enemies[0]));
        var expiry = enemies[0].GetPower<FlexPotionPower>()!;
        Require(expiry.Amount == 6 && Amount<StrengthPower>(enemies[0]) == 6, "temporary expiry adjusted once");
        await expiry.AfterSideTurnEnd(context, CombatSide.Enemy, [enemies[0]]);
        Require(Amount<StrengthPower>(enemies[0]) == 0 && Amount<StrengthPower>(self) == 1, "temporary transfer retained and no negative expiry");

        var upgradeOnly = new[] { typeof(RitualPower), typeof(HardToKillPower), typeof(SlipperyPower), typeof(HardenedShellPower) };
        foreach (var type in upgradeOnly)
        {
            await Reset();
            self.SetCurrentHpInternal(75);
            Raw(type, enemies[0], 3);
            await relic.BeforeCardPlayed(Play(strike, enemies[0]));
            await relic.BeforeCardPlayed(Play(strike, enemies[0], 1));
            Require(enemies[0].GetPower(ModelDb.GetId(type))!.Amount == 3 && self.GetPower(ModelDb.GetId(type)) == null,
                "normal cannot steal upgrade-only power " + type.Name);
            Require(self.MaxHp == 80 && self.CurrentHp == 75, "excluded power must not cost max HP " + type.Name);
        }

        MainFile.Logger.Info($"GUANGYONG_MODEL_CASES_OK checks={checks}");
        // Exercise the selector with an actual AttackCommand as its source. We do
        // not spawn damage VFX or wait for a nonexistent combat room in this fixture.
        await Reset();
        foreach (var enemy in enemies) Raw(typeof(StrengthPower), enemy, 3);
        var randomCard = state.CreateCard<SwordBoomerang>(player);
        var randomPlay = Play(randomCard, null);
        var prediction = new Rng(run.Rng.CombatTargets.ToSerializable());
        var expectedFirst = prediction.NextItem(state.HittableEnemies);
        var scope = typeof(WeiDongZhuoRandomTargetScope);
        object?[] arguments = [randomCard, context, null];
        AccessTools.Method(scope, "Prefix").Invoke(null, arguments);
        try
        {
            Require(WeiDongZhuoRandomTargetScope.ContextFor(randomCard) == context, "original choice context retained");
            await relic.BeforeCardPlayed(randomPlay);
            Require(Amount<StrengthPower>(expectedFirst!) == 2 && Amount<StrengthPower>(self) == 1, "random reserved before damage");
            var attack = DamageCmd.Attack(1);
#if GAME_BETA
            attack.FromCard(randomCard, randomPlay);
#else
            attack.FromCard(randomCard);
#endif
            attack.TargetingRandomOpponents(state).WithHitCount(3).WithNoAttackerAnim();
            await Task.Yield(); // Verify the reserved frame survives an async continuation.
            var actualFirst = WeiDongZhuoRandomTargetScope.NextTarget(run.Rng.CombatTargets, state.HittableEnemies, attack);
            Require(actualFirst == expectedFirst, "random selected hit matches stolen target");
            WeiDongZhuoRandomTargetScope.NextTarget(run.Rng.CombatTargets, state.HittableEnemies, attack);
            WeiDongZhuoRandomTargetScope.NextTarget(run.Rng.CombatTargets, state.HittableEnemies, attack);
            Require(Amount<StrengthPower>(self) == 1, "three random hits steal only once");
#if GAME_BETA
            prediction.NextItem(state.HittableEnemies);
            prediction.NextItem(state.HittableEnemies);
            Require(run.Rng.CombatTargets.NextInt(100000) == prediction.NextInt(100000), "random draw count unchanged");
#endif
        }
        finally { AccessTools.Method(scope, "Finalizer").Invoke(null, [arguments[2]]); }

        await Reset();
        Raw(typeof(StrengthPower), enemies[0], 3);
        await relic.BeforeCardPlayed(Play(state.CreateCard<StrikeIronclad>(ally), enemies[0]));
        await relic.BeforeCardPlayed(Play(strike, ally.Creature));
        Require(Amount<StrengthPower>(enemies[0]) == 3 && Amount<StrengthPower>(self) == 0, "ally play/target excluded");
        var defense = state.CreateCard<DefendIronclad>(player);
        // Empty draw pile here: test the max-HP operation without changing any real deck.
        await relic.BeforeCardPlayed(Play(defense, null));
        Require(self.CurrentHp == 80 && self.MaxHp == 81, "self max HP gain without healing");

        await Reset();
        var upgraded = ModelDb.Relic<WeiDongZhuoJieGuangYong>().ToMutable();
        player.RemoveRelicInternal(relic, true);
        player.AddRelicInternal(upgraded, silent: true);
        Raw(typeof(StrengthPower), enemies[0], 7);
        await upgraded.BeforeCardPlayed(Play(strike, enemies[0]));
        Require(Amount<StrengthPower>(enemies[0]) == 0 && Amount<StrengthPower>(self) == 7, "upgraded all layers");
        Raw(typeof(HardToKillPower), enemies[0], 20);
        Raw(typeof(HardToKillPower), self, 7);
        await upgraded.BeforeCardPlayed(Play(strike, enemies[0]));
        Require(Amount<HardToKillPower>(enemies[0]) == 0 && Amount<HardToKillPower>(self) == 1, "upgraded cap set not add");
        foreach (var type in upgradeOnly)
        {
            await Reset();
            Raw(type, enemies[0], 7);
            await upgraded.BeforeCardPlayed(Play(strike, enemies[0]));
            bool cap = type == typeof(HardToKillPower) || type == typeof(HardenedShellPower);
            Require(enemies[0].GetPower(ModelDb.GetId(type)) == null && self.GetPower(ModelDb.GetId(type))!.Amount == (cap ? 1 : 7),
                "upgraded can steal exclusive power " + type.Name);
        }
        MainFile.Logger.Info($"GUANGYONG_MECHANICS_OK checks={checks} in_memory_native_models; no player save touched");
    }
}
