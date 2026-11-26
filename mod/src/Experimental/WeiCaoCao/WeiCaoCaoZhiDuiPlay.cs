using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

#nullable enable
namespace Yjcm;

internal static class WeiCaoCaoZhiDuiPlay
{
    private static readonly AsyncLocal<CardModel?> Resolving = new();

    private static bool Matches(CardModel card) => card is not WeiCaoCaoZhongYuZhiDui &&
        !card.IsCanonical && card.Owner?.Creature is { IsAlive: true } owner &&
        owner.Powers.OfType<WeiCaoCaoZhiDuiPower>().Any(power => power.Matches(card));

    internal static bool CanCoverCost(CardModel card, UnplayableReason reason) =>
        reason != UnplayableReason.None &&
        (reason & ~(UnplayableReason.EnergyCostTooHigh | UnplayableReason.StarCostTooHigh)) == 0 &&
        Matches(card);

    internal static bool CanPlayFace(CardModel card) =>
        card.CanPlay(out var reason, out _) || CanCoverCost(card, reason);

    internal static bool CanCoverHandCard(CardModel card, UnplayableReason reason) =>
        card.Pile?.Type == PileType.Hand && CanCoverCost(card, reason) &&
        RunManager.Instance.ActionExecutor?.CurrentlyRunningAction is null or PlayCardAction;

    internal static bool ShouldDefer(CardModel card) =>
        !ReferenceEquals(Resolving.Value, card) && card.Pile?.Type == PileType.Hand && Matches(card) &&
        RunManager.Instance.ActionExecutor?.CurrentlyRunningAction is PlayCardAction action &&
        ReferenceEquals(action.NetCombatCard.ToCardModelOrNull(), card);

    internal static bool ShouldResolve(PlayerChoiceContext context, CardModel card, bool isAutoPlay) =>
        !isAutoPlay && !ReferenceEquals(Resolving.Value, card) &&
        context is GameActionPlayerChoiceContext { Action: PlayCardAction } && Matches(card);

    internal static async Task Play(PlayerChoiceContext context, CardModel card, Creature? target, bool skipVisuals)
    {
        var previous = Resolving.Value;
        Resolving.Value = card;
        try
        {
            bool free = false;
            bool draw = false;
            if (Matches(card))
            {
                var owner = card.Owner;
                var state = card.CombatState ?? owner.Creature.CombatState!;
                if (owner.PlayerCombatState!.HasEnoughResourcesFor(card, out _))
                {
                    CardModel[] options = [state.CreateCard<WeiCaoCaoChoiceDraw>(owner),
                        state.CreateCard<WeiCaoCaoChoiceFreePlay>(owner)];
                    var choice = (await WeiCaoCaoDesignRules.Choose(context, card, options, 1, 1)).FirstOrDefault();
                    draw = choice is WeiCaoCaoChoiceDraw;
                }
                // When only the free option is affordable the play can proceed directly.
                free = !draw;
            }

            ResourceInfo resources = default;
            if (free)
            {
                if (card.EnergyCost.CostsX) card.EnergyCost.CapturedXValue = 0;
                card.LastStarsSpent = 0;
                await Hook.AfterEnergySpent(card.CombatState ?? card.Owner.Creature.CombatState!, card, 0);
            }
            else
            {
                var (energy, stars) = await card.SpendResources();
                resources = new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars };
            }

            await card.OnPlayWrapper(context, target, false, resources, skipVisuals);
            if (draw) await WeiCaoCaoDesignRules.Draw(context, card, 2);
        }
        finally { Resolving.Value = previous; }
    }
}
