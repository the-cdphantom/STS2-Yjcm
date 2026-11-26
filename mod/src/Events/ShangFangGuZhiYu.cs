using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Yjcm.Interop;

#nullable enable

namespace Yjcm;

[RegisterSharedEvent]
public sealed class ShangFangGuZhiYu : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://Yjcm/Images/events/shang_fang_gu_zhi_yu.png");

    public override bool IsAllowed(IRunState runState) =>
        IsAllowedForScope(runState, WuZhugeliangSettings.ModEventScope);

    internal static bool IsAllowedForScope(IRunState runState, string scope) =>
        WuZhugeliangSettings.MatchesAnyPlayerScope(runState, scope)
        && (ShangFangGuPartners.SunqianCard != null || ShangFangGuPartners.NewsanguoCard != null);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            new(this, Dream, InitialOptionKey("DREAM"),
                HoverTipFactory.FromCardWithCardHoverTips<YiZhenQingMeng>())
        };
        if (ShangFangGuPartners.SunqianCard is { } sigh)
            options.Add(CardOption("SIGH", sigh));
        if (ShangFangGuPartners.NewsanguoCard is { } fate)
            options.Add(CardOption("FATE", fate));
        options.Add(new EventOption(this, History, InitialOptionKey("HISTORY"),
            HoverTipFactory.FromRelic<QiXingDeng>()));
        return options;
    }

    private EventOption CardOption(string key, CardModel canonicalCard) =>
        new(this, () => ObtainCard(canonicalCard, key), InitialOptionKey(key),
            new[] { HoverTipFactory.FromCard(canonicalCard) }.Concat(canonicalCard.HoverTips));

    private Task Dream() => ObtainCard(ModelDb.Card<YiZhenQingMeng>(), "DREAM");

    private async Task ObtainCard(CardModel canonicalCard, string page)
    {
        var card = Owner!.RunState.CreateCard(canonicalCard, Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.{page}.description"));
    }

    private async Task History()
    {
        await RelicCmd.Obtain<QiXingDeng>(Owner!);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.HISTORY.description"));
    }
}
