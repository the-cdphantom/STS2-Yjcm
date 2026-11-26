using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

[RegisterRelic(typeof(SharedRelicPool))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "STS001")]
public sealed class YjcmQingShi : QingShiRelic
{
    public static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(YjcmQingShi).Assembly.Location)!,
        "Voice", "Bank", "Assets", "qing_shi.wav");

    protected override bool IsRefined => false;
    protected override string TriggerVoicePath => VoicePath;

    /// <summary>
    /// 正常摸牌完成后，将带有赏誉关键词的牌从抽牌堆或弃牌堆加入手牌。
    /// 使用已被战斗流程等待的回合钩子，避免异步补丁与正常摸牌交错导致联机状态分歧。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStart(choiceContext, player);
        if (player != Owner)
            return;

        var drawPile = PileTypeExtensions.GetPile(PileType.Draw, player);
        var discardPile = PileTypeExtensions.GetPile(PileType.Discard, player);
        var shangYuCards = new List<CardModel>();

        if (drawPile?.Cards != null)
            shangYuCards.AddRange(drawPile.Cards.Where(c => c.Keywords.Contains(YjcmKeywords.ShangYu)));
        if (discardPile?.Cards != null)
            shangYuCards.AddRange(discardPile.Cards.Where(c => c.Keywords.Contains(YjcmKeywords.ShangYu)));

        foreach (var card in shangYuCards)
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom);
    }
}
