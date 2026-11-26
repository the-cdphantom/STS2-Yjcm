using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_LE_CAO_ZHI_FU_LE")]
public sealed class WeiCaoCaoLeCaoZhiFuLe : WeiCaoCaoCard
{
    private bool _autoPlayed;
    public WeiCaoCaoLeCaoZhiFuLe() : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/fu_le.png";
    public override int MaxUpgradeLevel => 0;
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang, YjcmCardTags.Wei];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        HoverTipFactory.FromEnchantment<WeiCaoCaoFu>().Concat([
            HoverTipFactory.FromPower<WeiCaoCaoWenLan>(), WuJiangHoverTip.ForFaction(YjcmCardTags.Wei)]);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        WeiCaoCaoAudio.PlayVariant("fu_le", 4);
        foreach (var card in PileType.Hand.GetPile(Owner).Cards.ToArray())
            WeiCaoCaoFu.Assign(card);
        var existing = Owner.Creature.Powers.OfType<WeiCaoCaoWenLan>().FirstOrDefault();
        if (existing != null) return;
        await PowerCmd.Apply<WeiCaoCaoWenLan>(context, Owner.Creature, 1, Owner.Creature, this, false);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (_autoPlayed || player != Owner || Pile?.Type != PileType.Hand ||
            Owner.Creature.CombatState?.RoundNumber != 1 || CombatManager.Instance.IsOverOrEnding) return;
        _autoPlayed = true;
        await CardCmd.AutoPlay(context, this, null, AutoPlayType.Default, false, false);
    }
    protected override void AfterCloned() { base.AfterCloned(); _autoPlayed = false; }
}

[RegisterEnchantment]
public sealed class WeiCaoCaoFu : YjcmMusicEnchantment
{
    protected override string EnchantmentIconPath => "res://Experimental/WeiCaoCao/images/fu_enchantment.png";
    [SavedProperty] public string AlternateEntry { get; set; } = "";
    [SavedProperty] public int AlternatePlaysThisTurn { get; set; }
    [SavedProperty] public int AlternateRetainedTurns { get; set; }
    [SavedProperty] public bool AlternateReturnedThisTurn { get; set; }
    public override bool HasExtraCardText => true;
    protected override void OnEnchant()
    {
        base.OnEnchant();
        // The alias survives serialization; its Retain keyword must also be
        // restored even when the inherited AddedRetain flag was already saved.
        if (!Card.Keywords.Contains(CardKeyword.Retain)) Card.AddKeyword(CardKeyword.Retain);
    }
    public override bool IsStackable => true; // Reassign the alias, never add another choice.
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromKeyword(CardKeyword.Retain);
            if (!IsCanonical && HasCard && Resolve(AlternateEntry) != null)
                yield return HoverTipFactory.FromCard(WeiCaoCaoFuPlay.AliasPreview(Card));
        }
    }

    internal static CardModel? Resolve(string entry) => string.IsNullOrEmpty(entry) ? null :
        ModelDb.CardPool<WeiCaoCaoCardPool>().AllCards.FirstOrDefault(c => c.Id.Entry == entry);

    // Name identity must not depend on a client's selected UI language.
    internal static string NameKey(string entry) => entry.Replace("WEI_CAO_CAO_", "").Replace("WEI_DONG_ZHUO_", "");
    internal static string[] Names(CardModel card) => card.Enchantment is WeiCaoCaoFu { Status: EnchantmentStatus.Normal } fu
        ? new[] { card.Id.Entry, fu.AlternateEntry }.Where(e => !string.IsNullOrEmpty(e)).DistinctBy(NameKey).ToArray() : [];

    internal static bool CanAssign(CardModel card)
    {
        if (card.Type is not (CardType.Attack or CardType.Skill or CardType.Power)) return false;
        if (card.Enchantment == null || card.Enchantment is WeiCaoCaoFu)
            return ModelDb.Enchantment<WeiCaoCaoFu>().CanEnchant(card);
        var probe = (CardModel)card.ClonePreservingMutability();
        probe.ClearEnchantmentInternal();
        return ModelDb.Enchantment<WeiCaoCaoFu>().CanEnchant(probe);
    }

    internal static bool IsAliasCandidate(CardModel candidate, CardModel source) =>
        candidate.Type is CardType.Attack or CardType.Skill or CardType.Power &&
        candidate is not WeiCaoCaoLeCaoZhiFuLe &&
        candidate.Rarity is not (CardRarity.Basic or CardRarity.Token or CardRarity.Ancient) &&
        NameKey(candidate.Id.Entry) != NameKey(source.Id.Entry);

    internal static void Assign(CardModel card)
    {
        if (!CanAssign(card) || card.Owner?.Creature.CombatState == null) return;
        var candidates = ModelDb.CardPool<WeiCaoCaoCardPool>()
            .GetUnlockedCards(card.Owner.UnlockState, card.Owner.RunState.CardMultiplayerConstraint)
            .Where(c => IsAliasCandidate(c, card)).DistinctBy(c => NameKey(c.Id.Entry)).ToArray();
        if (candidates.Length == 0) return;
        var alias = card.Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
        if (card.Enchantment is WeiCaoCaoFu existing)
        {
            existing.AlternateEntry = alias.Id.Entry;
            existing.AlternatePlaysThisTurn = 0;
            existing.AlternateRetainedTurns = 0;
            existing.AlternateReturnedThisTurn = false;
            CardCmd.ClearEnchantment(card);
            CardCmd.Enchant(existing, card, 1); // Refresh native card text/hover visuals too.
            return;
        }
        if (card.Enchantment != null) CardCmd.ClearEnchantment(card);
        var enchantment = ModelDb.Enchantment<WeiCaoCaoFu>().ToMutable();
        ((WeiCaoCaoFu)enchantment).AlternateEntry = alias.Id.Entry;
        CardCmd.Enchant(enchantment, card, 1);
    }

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (HasCard && Card.Owner == player){AlternatePlaysThisTurn = 0;AlternateReturnedThisTurn=false;}
        return Task.CompletedTask;
    }
    public override Task AfterFlush(PlayerChoiceContext context,Player player,IReadOnlyCollection<CardModel> flushed,IReadOnlyCollection<CardModel> retained)
    {
        if(HasCard && Card.Owner==player && retained.Contains(Card) && Resolve(AlternateEntry) is WeiCaoCaoCheZhouSheFu)AlternateRetainedTurns++;
        return Task.CompletedTask;
    }
}

[RegisterPower]
public sealed class WeiCaoCaoWenLan : ModPowerTemplate
{
    private Dictionary<CardPlay, string[]> _pending = [];
    [SavedProperty] public string PreviousNames { get; set; } = "";
    [SavedProperty] public int CardsInGroup { get; set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/wen_lan_buff.png";
    public override string CustomBigIconPath => CustomIconPath;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory.FromEnchantment<WeiCaoCaoFu>();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pending = [];
    }
    internal static string[] UnionIfMatched(IEnumerable<string> previous, IEnumerable<string> current)
    {
        var a = previous.Where(e => !string.IsNullOrEmpty(e)).ToArray();
        var b = current.Where(e => !string.IsNullOrEmpty(e)).ToArray();
        return a.Select(WeiCaoCaoFu.NameKey).Intersect(b.Select(WeiCaoCaoFu.NameKey)).Any()
            ? a.Concat(b).DistinctBy(WeiCaoCaoFu.NameKey).ToArray() : [];
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner?.Creature != Owner || !play.IsFirstInSeries ||
            play.Card is WeiCaoCaoLeCaoZhiFuLe) return Task.CompletedTask;
        // A saved game cannot restore an in-flight CardPlay reference. Discard a
        // stale resolving flag, while preserving a normally saved first card.
        if (CardsInGroup >= 2 && _pending.Count == 0) ResetGroup();
        if (CardsInGroup >= 2) return Task.CompletedTask;
        string[] current = WeiCaoCaoFu.Names(play.Card);
        if (CardsInGroup == 0)
        {
            PreviousNames = string.Join('|', current); // Normal cards occupy a slot with no Fu names.
            CardsInGroup = 1;
        }
        else
        {
            _pending[play] = UnionIfMatched(PreviousNames.Split('|', StringSplitOptions.RemoveEmptyEntries), current);
            CardsInGroup = 2; // Hold the completed pair until its settlement finishes.
        }
        return Task.CompletedTask;
    }

    private void ResetGroup()
    {
        CardsInGroup = 0;
        PreviousNames = "";
        _pending.Clear();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner?.Creature != Owner || !play.IsFirstInSeries ||
            play.Card is WeiCaoCaoLeCaoZhiFuLe || Owner.Player is not { } player ||
            !_pending.TryGetValue(play, out var names)) return;
        try
        {
            if (Owner.IsDead || CombatManager.Instance.IsOverOrEnding) return;
            WeiCaoCaoAudio.PlayVariant("wen_lan", 4);
            if (names.Length > 0)
            {
                foreach (string entry in names)
                {
                    var canonical = WeiCaoCaoFu.Resolve(entry) ?? ModelDb.GetById<CardModel>(new ModelId("CARD", entry));
                    var generated = player.Creature.CombatState!.CreateCard(canonical, player);
                    WeiCaoCaoFu.Assign(generated);
                    await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, player, CardPilePosition.Bottom);
                }
                return;
            }
            var eligible = PileType.Hand.GetPile(player).Cards.Where(WeiCaoCaoFu.CanAssign).ToArray();
            if (eligible.Length == 0) return;
            var chosen = await CardSelectCmd.FromHand(context, player,
                new CardSelectorPrefs(new LocString("powers", "YJCM_POWER_WEI_CAO_CAO_WEN_LAN.select_prompt"), 0, eligible.Length)
                { RequireManualConfirmation = true }, WeiCaoCaoFu.CanAssign, this);
            foreach (var card in chosen.ToArray())
                if (card.Pile?.Type == PileType.Hand) WeiCaoCaoFu.Assign(card);
        }
        finally { ResetGroup(); }
    }
}
