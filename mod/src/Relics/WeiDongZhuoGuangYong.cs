using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Runtime.CompilerServices;

namespace Yjcm;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class WeiDongZhuoGuangYong : WeiDongZhuoGuangYongBase
{
    protected override bool Upgraded => false;
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class WeiDongZhuoJieGuangYong : WeiDongZhuoGuangYongBase
{
    protected override bool Upgraded => true;
}

public abstract class WeiDongZhuoGuangYongBase : ModRelicTemplate
{
    internal static readonly Type[] Priority =
    [
        typeof(StrengthPower), typeof(DexterityPower), typeof(FocusPower), typeof(VigorPower),
        typeof(DoubleDamagePower), typeof(ArtifactPower), typeof(BufferPower), typeof(IntangiblePower),
        typeof(PlatingPower), typeof(ThornsPower), typeof(RegenPower), typeof(BarricadePower),
        typeof(RitualPower),
        typeof(HardToKillPower), typeof(SlipperyPower), typeof(HardenedShellPower)
    ];
    private int _seenCaps;
    private bool _voicePlayedThisTurn;
    protected abstract bool Upgraded { get; }
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => "res://WeiDongZhuo/images/guang_yong.png";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [];

    internal static IEnumerable<Type> StealPriority(bool upgraded) => Priority.Where(t => upgraded ||
        (t != typeof(RitualPower) && t != typeof(HardToKillPower) &&
         t != typeof(SlipperyPower) && t != typeof(HardenedShellPower)));

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _seenCaps = 0;
        _voicePlayedThisTurn = false;
    }
    public override Task BeforeCombatStart()
    {
        _seenCaps = 0;
        _voicePlayedThisTurn = false;
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner) _voicePlayedThisTurn = false;
        return Task.CompletedTask;
    }
    private static int CapBit(Type type) => type == typeof(HardToKillPower) ? 1 : type == typeof(HardenedShellPower) ? 2 : 0;
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power.Owner == Owner.Creature && power.Amount > 0) _seenCaps |= CapBit(power.GetType());
        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (WeiDongZhuoRandomTargetScope.PlayerOf(play) != Owner || Owner.Creature.IsDead || Owner.Creature.CombatState == null) return;
        // If a debug command grants both versions, only the upgraded one resolves.
        if (!Upgraded && Owner.Relics.Any(r => r is WeiDongZhuoJieGuangYong)) return;
        var card = play.Card;
        var self = Owner.Creature;
        var context = WeiDongZhuoRandomTargetScope.ContextFor(card) ?? new ThrowingPlayerChoiceContext();
        if (card.TargetType == TargetType.Self || play.Target == self)
        {
            Flash();
            PlayTriggerVoice();
            await CardPileCmd.Draw(context, 1, Owner, false);
            if (self.IsDead) return;
            // 普通犷勇只增加上限；界犷勇额外按实际增加量回复生命。
            decimal gained = await CreatureCmd.SetMaxHp(self, self.MaxHp + 1);
            var history = Owner.RunState.CurrentMapPointHistoryEntry;
            if (history != null) history.GetEntry(Owner.NetId).MaxHpGained += (int)gained;
            if (Upgraded && gained > 0)
                await CreatureCmd.Heal(self, gained);
            return;
        }
        IEnumerable<Creature> targets = [];
        if (card.TargetType == TargetType.AllEnemies)
            targets = self.CombatState.GetOpponentsOf(self).Where(c => c.IsAlive).ToArray();
        else if (card.TargetType == TargetType.RandomEnemy)
        {
            // Reserve the actual first random target before card effects. Its native
            // roll is consumed here and reused by the first target-selection call.
            var target = WeiDongZhuoRandomTargetScope.Reserve(play);
            if (target != null) targets = [target];
        }
        else if (play.Target is { IsAlive: true } target && target.Side != self.Side)
            targets = [target];
        bool triggered = false;
        foreach (var target in targets)
        {
            if (self.IsDead) break;
            triggered |= await Steal(context, target, card);
        }
        if (triggered) PlayTriggerVoice();
    }

    private async Task<bool> Steal(PlayerChoiceContext context, Creature enemy, CardModel card)
    {
        var self = Owner.Creature;
        if (!enemy.IsAlive || !self.CanReceivePowers) return false;
        var source = StealPriority(Upgraded).Select(t => enemy.Powers.FirstOrDefault(p => p.GetType() == t && p.Amount > 0)).FirstOrDefault(p => p != null);
        if (source == null) return false;
        int bit = CapBit(source.GetType());
        var own = self.GetPower(source.Id);
        var transfer = WeiDongZhuoGuangYongRules.Calculate(source.Amount, own?.Amount ?? 0, Upgraded, bit != 0, (_seenCaps & bit) != 0);
        if (!transfer.Success) return false;
        Flash();
        if (!await TransferBuff(context, source, card, transfer)) return false;
        if (bit != 0) _seenCaps |= bit;
        // Check wounded status anew for EACH enemy. Reaching full HP stops the cost.
        if (WeiDongZhuoGuangYongRules.PayMaxHp(self.CurrentHp, self.MaxHp, true))
            await YjcmCreatureCompat.LoseMaxHp(context, self, 1, null);
        var node = NCombatRoom.Instance?.GetCreatureNode(enemy);
        if (node != null && enemy.IsAlive) await node.UpdateIntent(enemy.CombatState!.Allies);
        return true;
    }

    internal static bool CanStealOne(PowerModel source, Creature recipient) =>
        source.Owner.IsAlive && recipient.CanReceivePowers && source.Amount > 0 &&
        Priority.Contains(source.GetType()) &&
        ((source.StackType != PowerStackType.None && CapBit(source.GetType()) == 0) || recipient.GetPower(source.Id) == null);

    internal static async Task<bool> StealOne(PlayerChoiceContext context, PowerModel source, CardModel card)
    {
        var recipient = card.Owner.Creature;
        if (!CanStealOne(source, recipient)) return false;
        int amount = (recipient.GetPower(source.Id)?.Amount ?? 0) + 1;
        if (!await TransferBuff(context, source, card, new(source.Amount - 1, amount, true))) return false;
        var enemy = source.Owner;
        var node = NCombatRoom.Instance?.GetCreatureNode(enemy);
        if (node != null && enemy.IsAlive) await node.UpdateIntent(enemy.CombatState!.Allies);
        return true;
    }

    private static async Task<bool> TransferBuff(PlayerChoiceContext context, PowerModel source, CardModel card,
        WeiDongZhuoGuangYongRules.Transfer transfer)
    {
        var self = card.Owner.Creature;
        var enemy = source.Owner;
        var own = self.GetPower(source.Id);
        if (source is HardenedShellPower)
        {
            WeiDongZhuoShellSafety.Mark(enemy);
            WeiDongZhuoShellSafety.Mark(self);
        }
        // A transfer is atomic from the target's point of view: never remove an
        // enemy power unless the player now actually has the corresponding power.
        // This is especially important for single-stack powers such as Barricade.
        if (own == null)
        {
            var canonical = ModelDb.GetById<PowerModel>(source.Id);
            await PowerCmd.Apply(context, canonical.ToMutable(), self, transfer.Player, self, card);
            own = self.GetPower(source.Id);
        }
        if (own == null || own.Amount <= 0)
        {
            MainFile.Logger.Warn($"BUFF_TRANSFER_ABORTED power={source.Id}");
            return false;
        }
        // Damage-cap upgrades must set 1, never add 1 to an existing cap.
        if (own.Amount != transfer.Player)
            await SetExisting(context, own, transfer.Player, card);
        if (own.Amount != transfer.Player)
        {
            MainFile.Logger.Warn($"BUFF_TRANSFER_ABORTED amount power={source.Id} expected={transfer.Player} actual={own.Amount}");
            return false;
        }
        // Change the aggregate stat exactly once. Temporary wrappers are bookkeeping,
        // not extra steal candidates, and their removal must not subtract it again.
        await ReduceTemporaryExpiry(enemy, source, source.Amount - transfer.Enemy);
        await SetExisting(context, source, transfer.Enemy, card);
        if (source is HardenedShellPower)
        {
            WeiDongZhuoShellSafety.Mark(enemy);
            WeiDongZhuoShellSafety.Mark(self);
            if (transfer.Enemy == 0) enemy.HpDisplay = HpDisplay.Normal;
        }
        return true;
    }

    private void PlayTriggerVoice()
    {
        if (WuZhugeliangSettings.GuangYongVoiceOncePerTurn && _voicePlayedThisTurn) return;
        _voicePlayedThisTurn = true;
        string prefix = Upgraded ? "jie_guang_yong" : "guang_yong";
        string file = $"{prefix}_{Random.Shared.Next(1, 3)}.wav";
        VoicePlayer.PlayWav(Path.Combine(
            Path.GetDirectoryName(typeof(WeiDongZhuoGuangYongBase).Assembly.Location)!,
            "Voice", "WeiDongZhuo", file));
    }

    private static async Task ReduceTemporaryExpiry(Creature enemy, PowerModel source, int taken)
    {
        if (taken <= 0) return;
        foreach (var wrapper in enemy.Powers.Where(p => p is ITemporaryPower temp && temp.InternallyAppliedPower.Id == source.Id
                     && p.Amount > 0 && p.Type == PowerType.Buff).OrderBy(p => p.Id.Entry, StringComparer.Ordinal).ToArray())
        {
            int removed = Math.Min(taken, wrapper.Amount);
            wrapper.SetAmount(wrapper.Amount - removed);
            if (wrapper.Amount == 0) await PowerCmd.Remove(wrapper);
            taken -= removed;
            if (taken == 0) break;
        }
    }

    private static async Task SetExisting(PlayerChoiceContext context, PowerModel power, int amount, CardModel card)
    {
        int delta = amount - power.Amount;
        if (delta == 0) return;
        // This is a transfer/removal, not applying a negative buff: Artifact must not
        // block stealing Strength or be consumed while a higher-priority buff exists.
        power.SetAmount(amount);
        var state = power.Owner.CombatState;
        if (state != null)
        {
            CombatManager.Instance.History.PowerReceived(state, power, delta, card.Owner.Creature);
            await Hook.AfterPowerAmountChanged(state, context, power, delta, card.Owner.Creature, card);
        }
        if (power.Amount == 0) await PowerCmd.Remove(power);
    }
}

[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
internal static class WeiDongZhuoGuangYongUpgrade
{
    private static bool Prefix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is not WeiDongZhuoGuangYong) return true;
        __result = ModelDb.Relic<WeiDongZhuoJieGuangYong>();
        return false;
    }
}

// Native HardenedShell can return a negative loss when its cap is lowered below
// damage already received this turn. Clamp only creatures touched by our relic.
[HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate))]
internal static class WeiDongZhuoShellSafety
{
    private static readonly ConditionalWeakTable<Creature, object> Changed = new();
    internal static void Mark(Creature creature) => Changed.GetValue(creature, _ => new object());
    private static void Postfix(HardenedShellPower __instance, Creature target, ref decimal __result)
    {
        if (target == __instance.Owner && Changed.TryGetValue(target, out _)) __result = Math.Max(0, __result);
    }
}
