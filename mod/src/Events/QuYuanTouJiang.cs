using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>普通共享随机事件“屈原投江”；是屈原-离骚的唯一取得来源。</summary>
[RegisterSharedEvent]
public sealed class QuYuanTouJiang : ModEventTemplate
{
    private const string PortraitPath =
        "res://Yjcm/Images/events/qu_yuan_tou_jiang.png";

    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: PortraitPath
    );

    public override bool IsAllowed(IRunState runState) =>
        IsAllowedForScope(runState, WuZhugeliangSettings.ModEventScope);

    internal static bool IsAllowedForScope(IRunState runState, string scope) =>
        WuZhugeliangSettings.MatchesAnyPlayerScope(runState, scope);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, Approach, InitialOptionKey("APPROACH"),
            HoverTipFactory.FromCardWithCardHoverTips<WeiDongZhuoQuYuanLiSao>()),
        new EventOption(this, Leave, InitialOptionKey("LEAVE"))
    ];

    private async Task Approach()
    {
        await AddToDeck<WeiDongZhuoQuYuanLiSao>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.APPROACH.description"));
    }

    private Task Leave()
    {
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LEAVE.description"));
        return Task.CompletedTask;
    }

    private async Task AddToDeck<T>() where T : CardModel
    {
        var card = Owner!.RunState.CreateCard<T>(Owner!);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
    }
}
