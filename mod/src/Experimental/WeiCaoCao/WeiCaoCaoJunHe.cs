using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

internal static class WeiCaoCaoAudio
{
    internal static void Play(string name) => VoicePlayer.PlayWav(Path.Combine(
        Path.GetDirectoryName(typeof(WeiCaoCao).Assembly.Location)!, "Voice", "WeiCaoCao", name + ".wav"));

    // Cosmetic randomness must never consume synchronized combat RNG.
    internal static void PlayJunHe() => Play(Random.Shared.Next(2) == 0 ? "jun_he_1" : "jun_he_2");
    internal static void PlayVariant(string prefix, int count) => Play(prefix + "_" + (Random.Shared.Next(count) + 1));
}

[RegisterPower]
public sealed class WeiCaoCaoJunHePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/jun_he_buff.png";
    public override string CustomBigIconPath => CustomIconPath;

    public static int AmountOf(Creature owner) => owner.Powers.OfType<WeiCaoCaoJunHePower>()
        .FirstOrDefault()?.Amount ?? 0;

    public static async Task Gain(PlayerChoiceContext context, Creature owner, int amount)
    {
        if (amount > 0)
            await PowerCmd.Apply<WeiCaoCaoJunHePower>(context, owner, amount, owner, null, false);
    }

    public static async Task<bool> TryConsume(PlayerChoiceContext context, Creature owner, int amount, CardModel? source)
    {
        if (amount <= 0) return false;
        var resource = owner.Powers.OfType<WeiCaoCaoJunHePower>().FirstOrDefault();
        if (resource == null || resource.Amount < amount) return false;
        int before = resource.Amount;
        await PowerCmd.ModifyAmount(context, resource, -amount, owner, source, false);
        if (AmountOf(owner) > before - amount) return false;
        if (owner.Powers.OfType<WeiCaoCaoGuiXinPower>().FirstOrDefault() is { } guiXin)
            await guiXin.OnTriggered(context, source);
        if (owner.Player?.Relics.OfType<WeiCaoCaoJieJunHe>().FirstOrDefault(relic => !relic.IsMelted) is { } relic)
            await relic.OnJunHeConsumed(context);
        return true;
    }
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class WeiCaoCaoJunHe : WeiCaoCaoJunHeBase;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class WeiCaoCaoJieJunHe : WeiCaoCaoJunHeBase
{
    [SavedProperty] public bool RewardedThisTurn { get; set; }
    protected override void PlayGrantVoice() => WeiCaoCaoAudio.Play("jie_jun_he");

    public override Task BeforeCombatStart()
    {
        RewardedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        RewardedThisTurn = false;
        return Task.CompletedTask;
    }

    internal async Task OnJunHeConsumed(PlayerChoiceContext context)
    {
        if (RewardedThisTurn || Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        RewardedThisTurn = true;
        Flash();
        await PlayerCmd.GainEnergy(1, Owner);
        await CardPileCmd.Draw(context, Owner);
    }
}

public abstract class WeiCaoCaoJunHeBase : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => "res://Experimental/WeiCaoCao/images/jun_he.png";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<WeiCaoCaoJunHePower>()];
    protected virtual void PlayGrantVoice() => WeiCaoCaoAudio.PlayJunHe();

    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        WeiCaoCaoJunHeTurnEffect.Resolve(context, Owner, player, this, () =>
        {
            Flash();
            PlayGrantVoice();
        });
}

[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
internal static class WeiCaoCaoJunHeUpgrade
{
    private static bool Prefix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is not WeiCaoCaoJunHe) return true;
        __result = ModelDb.Relic<WeiCaoCaoJieJunHe>();
        return false;
    }
}

// Keep the selection/resource transaction independent of the engine's visual
// RelicModel initialization, so the actual turn flow can be simulated in isolation.
internal static class WeiCaoCaoJunHeTurnEffect
{
    internal static async Task Resolve(PlayerChoiceContext context, Player owner, Player player,
        AbstractModel? source, Action onGranted)
    {
        if (player != owner || player.Creature.IsDead) return;
        var hand = PileType.Hand.GetPile(player);
        if (hand.Cards.Count == 0) return;
        // FromHand does not discard/move the selection. Count every matching card,
        // including the selected card, status/curse cards, and cards which cannot be played.
        var selected = (await CardSelectCmd.FromHand(context, player,
            new CardSelectorPrefs(new LocString("relics", "YJCM_RELIC_WEI_CAO_CAO_JUN_HE.select_prompt"), 0, 1)
            { RequireManualConfirmation = true }, null, source)).FirstOrDefault();
        if (selected == null || !hand.Cards.Contains(selected) || player.Creature.IsDead) return;
        int amount = hand.Cards.Count(card => card.Type == selected.Type) + 1;
        if (amount <= 0) return;
        onGranted();
        await WeiCaoCaoJunHePower.Gain(context, player.Creature, amount);
    }
}
