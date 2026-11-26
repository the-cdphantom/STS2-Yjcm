using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.CardTargeting;

namespace Yjcm;

internal static class TanBanTargetSelection
{
    internal static bool RequiresManualTarget(TargetType type) =>
        type is TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer ||
        CustomTargetType.IsCustomSingleTargetType(type);

    internal static async Task<Creature?> SelectTargetAsync(PlayerChoiceContext context, CardModel card)
    {
        var player = card.Owner;
        var combat = player.Creature.CombatState;
        if (combat == null || !TanBan.CanAutoPlay(card)) return null;

        var creatures = combat.Creatures.ToArray();
        var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
        // Reserve before pausing the action so every peer advances the same choice ID.
        uint choiceId = synchronizer.ReserveChoiceId(player);
#if GAME_BETA
        await context.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.CancelPlayCardActions);
#else
        await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.CancelPlayCardActions);
#endif
        try
        {
            int index;
            if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay)
            {
                index = -1;
                try
                {
                    var chosen = await SelectLocalTargetAsync(card);
                    index = Array.IndexOf(creatures, chosen);
                }
                finally
                {
                    // Null, a busy target picker and cancellation all publish the same skip result.
                    synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(index));
                }
            }
            else
            {
                index = (await synchronizer.WaitForRemoteChoice(player, choiceId)).AsIndex();
            }

            var target = index >= 0 && index < creatures.Length ? creatures[index] : null;
            return TanBan.CanAutoPlay(card) && player.Creature.CombatState == combat &&
                   target != null && combat.Creatures.Contains(target) && card.IsValidTarget(target)
                ? target : null;
        }
        finally
        {
            await context.SignalPlayerChoiceEnded();
        }
    }

    private static async Task<Creature?> SelectLocalTargetAsync(CardModel card)
    {
        var manager = NTargetManager.Instance;
        if (manager == null || manager.IsInSelection || !TanBan.CanAutoPlay(card)) return null;

        manager.StartTargeting(card.TargetType, Vector2.Zero, TargetMode.ClickMouseToTarget,
            () => !TanBan.CanAutoPlay(card), null);
        var selected = await manager.SelectionFinished();
        return selected switch
        {
            NCreature creature => creature.Entity,
            NMultiplayerPlayerState state => state.Player.Creature,
            _ => null
        };
    }
}
