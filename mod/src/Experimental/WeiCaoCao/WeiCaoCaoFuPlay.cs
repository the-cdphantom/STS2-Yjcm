using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.Combat.CardTargeting;
using System.Runtime.CompilerServices;

#nullable enable
namespace Yjcm;

// Resolve the choice within the existing synchronized PlayCardAction, before any
// resource is spent. Only this developer-only enchantment enters this path.
internal static class WeiCaoCaoFuPlay
{
    private static readonly ConditionalWeakTable<CardModel,CardModel> PhysicalAliases=new();
    private static readonly ConditionalWeakTable<CardModel,Action> FreeUntilPlayed=new();
    internal static CardModel Physical(CardModel card)=>PhysicalAliases.TryGetValue(card,out var source)?source:card;
    private static readonly AsyncLocal<HashSet<CardModel>?> Executing = new();
    internal static bool IsChooser(CardModel card) => !card.IsCanonical &&
        card.Enchantment is WeiCaoCaoFu { Status: EnchantmentStatus.Normal } fu &&
        !string.IsNullOrEmpty(fu.AlternateEntry) && !(Executing.Value?.Contains(card) ?? false) &&
        WeiCaoCaoFu.Resolve(fu.AlternateEntry) != null;

    private sealed class Scope : IDisposable
    {
        private readonly HashSet<CardModel>? _previous;
        public Scope(CardModel card)
        {
            _previous = Executing.Value;
            Executing.Value = _previous == null ? [card] : new(_previous) { card };
        }
        public void Dispose() => Executing.Value = _previous;
    }

    internal static void MakeFreeUntilPlayed(CardModel card)
    {
        if (!FreeUntilPlayed.TryGetValue(card, out _))
        {
            Action onPlayed = () => ConsumeFreePlay(card);
            FreeUntilPlayed.Add(card, onPlayed);
            card.Played += onPlayed;
        }
        card.EnergyCost.SetUntilPlayed(0);
        card.InvokeEnergyCostChanged();
    }

    private static void ConsumeFreePlay(CardModel card)
    {
        if (!FreeUntilPlayed.TryGetValue(card, out var onPlayed)) return;
        card.Played -= onPlayed;
        FreeUntilPlayed.Remove(card);
    }

    private static void ApplyFreePlayToAlias(CardModel source, CardModel alias)
    {
        if (FreeUntilPlayed.TryGetValue(source, out _)) alias.EnergyCost.SetUntilPlayed(0);
    }

    internal static CardModel AliasPreview(CardModel source)
    {
        var fu = (WeiCaoCaoFu)source.Enchantment!;
        var alias = WeiCaoCaoFu.Resolve(fu.AlternateEntry)!.ToMutable();
        alias.Owner = source.Owner;
        alias.UpgradePreviewType = CardUpgradePreviewType.Combat;
        MatchSourceUpgrade(alias, source);
        ApplyFreePlayToAlias(source, alias);
        if (alias is WeiCaoCaoDuoYue duo) duo.PlaysThisTurn = fu.AlternatePlaysThisTurn;
        if (alias is WeiCaoCaoCheZhouSheFu shefu){shefu.RetainedTurns=fu.AlternateRetainedTurns;shefu.Returned=fu.AlternateReturnedThisTurn;}
        return alias;
    }

    internal static void MatchSourceUpgrade(CardModel alias, CardModel source)
    {
        // Derive the alternate face from the physical card every time, so an
        // upgrade after enchanting, a refresh or loading a save all agree. Cards
        // without upgrades (e.g. FuLe) remain valid base-level choices.
        int level = Math.Min(source.CurrentUpgradeLevel, alias.MaxUpgradeLevel);
        while (alias.CurrentUpgradeLevel < level)
        {
            alias.UpgradeInternal();
            alias.FinalizeUpgradeInternal();
        }
    }

    internal static bool CanPlayEither(CardModel card)
    {
        using var scope = new Scope(card);
        return WeiCaoCaoZhiDuiPlay.CanPlayFace(card) || WeiCaoCaoZhiDuiPlay.CanPlayFace(AliasPreview(card));
    }

    internal static async Task Resolve(PlayerChoiceContext context, CardModel source, Creature? originalTarget,
        bool autoPlay, ResourceInfo autoResources, bool skipVisuals)
    {
        try
        {
            await ResolveCore(context, source, originalTarget, autoPlay, autoResources, skipVisuals);
        }
        finally
        {
            // The native action already lifted this card out of its hand holder
            // before our pre-play choices. A cancelled target is a successful
            // action return, so native CancelAction will not put its node back.
            // Reconcile only cards which really remain in Hand; never move a
            // played/discarded/exhausted card or override another mod's Play pile.
            if (!autoPlay && source.Pile?.Type == PileType.Hand && !source.Owner.Creature.IsDead &&
                !CombatManager.Instance.IsOverOrEnding)
                RestoreHandVisual(source);
        }
    }

    private static void RestoreHandVisual(CardModel source)
    {
        if (TestMode.IsOn || !LocalContext.IsMe(source.Owner)) return;
        var hand = NPlayerHand.Instance;
        var node = NCard.FindOnTable(source, PileType.Hand);
        if (hand == null || node == null || !GodotObject.IsInstanceValid(node)) return;
        var queue = NCardPlayQueue.Instance;
        // Relinquish the stale queue entry without generating another action or
        // another hand holder. The underlying Hand pile is already correct.
        if (queue?.GetCardNode(source) == node) queue.RemoveCardFromQueueForExecution(source);
        hand.TryCancelCardPlay(source);
        if (hand.GetCard(source) != null) return;
        node.PlayPileTween?.Kill();
        node.PlayPileTween = null;
        node.SetPreviewTarget(null);
        node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        hand.Add(node, source.Pile!.Cards.IndexOf(source));
        Log.Info($"[Yjcm.Fu] Returned unplayed/returned card visual to hand: {source.Id.Entry}");
    }

    private static readonly AsyncLocal<(CardModel Card, int Count)?> ExtraReplays = new();

    internal static async Task PlayWithExtraReplays(PlayerChoiceContext context, CardModel card, Creature? target, int count)
    {
        var previous = ExtraReplays.Value;
        ExtraReplays.Value = (card, count);
        card.BaseReplayCount += count;
        try { await CardCmd.AutoPlay(context, card, target); }
        finally
        {
            card.BaseReplayCount -= count;
            ExtraReplays.Value = previous;
        }
    }

    private static async Task ResolveCore(PlayerChoiceContext context, CardModel source, Creature? originalTarget,
        bool autoPlay, ResourceInfo autoResources, bool skipVisuals)
    {
        var fu = (WeiCaoCaoFu)source.Enchantment!;
        using var scope = new Scope(source);
        var originalPreview = (CardModel)source.ClonePreservingMutability();
        originalPreview.ClearEnchantmentInternal();
        originalPreview.UpgradePreviewType = CardUpgradePreviewType.Combat;
        var alternatePreview = AliasPreview(source);
        var options = new[] { originalPreview, alternatePreview }
            .Where(c => autoPlay || WeiCaoCaoZhiDuiPlay.CanPlayFace(c)).ToArray();
        if (options.Length == 0 || source.Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        var picked = (await CardSelectCmd.FromSimpleGrid(context, options, source.Owner,
            new CardSelectorPrefs(new LocString("enchantments", "YJCM_ENCHANTMENT_WEI_CAO_CAO_FU.select_prompt"), 1, 1)
            { Cancelable = false, RequireManualConfirmation = true })).FirstOrDefault();
        if (picked == null || source.Pile == null || source.Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        bool useAlias = ReferenceEquals(picked, alternatePreview);
        CardModel selected = useAlias ? alternatePreview : source;
        var (confirmed, target) = await SelectTarget(context, selected, originalTarget);
        if (!confirmed || (!autoPlay && !WeiCaoCaoZhiDuiPlay.CanPlayFace(selected)) || !selected.IsValidTarget(target)) return;

        // Resource payment happens exactly once, using the chosen branch. Original
        // effects keep their actual card instance, including upgrades and counters.
        if (!useAlias)
        {
            if (autoPlay) await source.OnPlayWrapper(context, target, true, autoResources, skipVisuals);
            else await WeiCaoCaoZhiDuiPlay.Play(context, source, target, skipVisuals);
            return;
        }

        var state = source.Owner.Creature.CombatState!;
        var alias = state.CreateCard(WeiCaoCaoFu.Resolve(fu.AlternateEntry)!, source.Owner);
        if (ExtraReplays.Value is { } replays && ReferenceEquals(replays.Card, source))
            alias.BaseReplayCount += replays.Count;
        PhysicalAliases.Add(alias,Physical(source));
        MatchSourceUpgrade(alias, source);
        ApplyFreePlayToAlias(source, alias);
        if (alias is WeiCaoCaoDuoYue duo) duo.PlaysThisTurn = fu.AlternatePlaysThisTurn;
        if (alias is WeiCaoCaoCheZhouSheFu shefu){shefu.RetainedTurns=fu.AlternateRetainedTurns;shefu.Returned=fu.AlternateReturnedThisTurn;}
        var reciprocal = (WeiCaoCaoFu)ModelDb.Enchantment<WeiCaoCaoFu>().ToMutable();
        reciprocal.AlternateEntry = source.Id.Entry;
        CardCmd.Enchant(reciprocal, alias, 1);
        using var aliasScope = new Scope(alias);
        var originalPile = source.Pile;
        int originalIndex = originalPile.Cards.IndexOf(source);
        var node = TestMode.IsOn ? null : NCard.FindOnTable(source);
        // This is one physical hand card with two playable faces. Swap the face
        // silently, so draw/discard/exhaust hooks run only for the actual play.
        source.RemoveFromCurrentPile(true);
        originalPile.AddInternal(alias, originalIndex, true);
        if (node != null) node.Model = alias;
        bool enteredPlay = false;
        try
        {
            enteredPlay = true;
            if (autoPlay) await alias.OnPlayWrapper(context, target, true, autoResources, skipVisuals);
            else await WeiCaoCaoZhiDuiPlay.Play(context, alias, target, skipVisuals);
        }
        finally
        {
            if (alias is WeiCaoCaoDuoYue playedDuo) fu.AlternatePlaysThisTurn = playedDuo.PlaysThisTurn;
            if (alias is WeiCaoCaoCheZhouSheFu playedSheFu){fu.AlternateRetainedTurns=playedSheFu.RetainedTurns;fu.AlternateReturnedThisTurn=playedSheFu.Returned;}
            var destination = alias.Pile;
            var destinationNode = TestMode.IsOn ? null : NCard.FindOnTable(alias);
            if (destination != null)
            {
                int index = destination.Cards.IndexOf(alias);
                alias.RemoveFromCurrentPile(true);
                destination.AddInternal(source, index, true);
                if (destinationNode != null) { destinationNode.Model = source; destinationNode.UpdateVisuals(destination.Type, CardPreviewMode.Normal); }
            }
            else if (!enteredPlay && !CombatManager.Instance.IsOverOrEnding)
                originalPile.AddInternal(source, Math.Min(originalIndex, originalPile.Cards.Count), true);
            else source.RemoveFromState();
            // A played power's None destination also removes the original face.
            alias.RemoveFromState();
            if (enteredPlay)
            {
                ConsumeFreePlay(source);
                if (source.EnergyCost.AfterCardPlayedCleanup()) source.InvokeEnergyCostChanged();
            }
        }
    }

    internal static bool NeedsTarget(TargetType type) => type is TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer ||
        CustomTargetType.IsCustomSingleTargetType(type);

    internal static async Task<(bool, Creature?)> SelectTarget(PlayerChoiceContext context, CardModel card, Creature? fallback)
    {
        if (!NeedsTarget(card.TargetType)) return (true, null);
        if (TestMode.IsOn) return (fallback != null && card.IsValidTarget(fallback), fallback);
        var player = card.Owner;
        var creatures = player.Creature.CombatState!.Creatures.ToArray();
        uint id = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player);
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
                var manager = NTargetManager.Instance;
                if (manager.IsInSelection) index = -1;
                else
                {
                    manager.StartTargeting(card.TargetType, Vector2.Zero, TargetMode.ClickMouseToTarget,
                        () => CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead, null);
                    var chosen = await manager.SelectionFinished();
                    var target = chosen switch
                    {
                        NCreature creature => creature.Entity,
                        NMultiplayerPlayerState state => state.Player.Creature,
                        _ => null
                    };
                    index = Array.IndexOf(creatures, target);
                }
                RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(player, id, PlayerChoiceResult.FromIndex(index));
            }
            else index = (await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(player, id)).AsIndex();
            var selected = index >= 0 && index < creatures.Length ? creatures[index] : null;
            return (selected != null && card.IsValidTarget(selected), selected);
        }
        finally { await context.SignalPlayerChoiceEnded(); }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
internal static class WeiCaoCaoFuDeferCost
{
    private static bool Prefix(CardModel __instance, ref Task<(int, int)> __result)
    {
        if (!WeiCaoCaoFuPlay.IsChooser(__instance) && !WeiCaoCaoZhiDuiPlay.ShouldDefer(__instance)) return true;
        __result = Task.FromResult((0, 0));
        return false;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class WeiCaoCaoFuChoosePlay
{
    private static bool Prefix(CardModel __instance, PlayerChoiceContext choiceContext, Creature? target,
        bool isAutoPlay, ResourceInfo resources, bool skipCardPileVisuals, ref Task __result)
    {
        if (WeiCaoCaoFuPlay.IsChooser(__instance))
            __result = WeiCaoCaoFuPlay.Resolve(choiceContext, __instance, target, isAutoPlay, resources, skipCardPileVisuals);
        else if (WeiCaoCaoZhiDuiPlay.ShouldResolve(choiceContext, __instance, isAutoPlay))
            __result = WeiCaoCaoZhiDuiPlay.Play(choiceContext, __instance, target, skipCardPileVisuals);
        else return true;
        return false;
    }
}

[HarmonyPatch]
internal static class WeiCaoCaoFuPlayable
{
    private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(CardModel), nameof(CardModel.CanPlay),
        [typeof(UnplayableReason).MakeByRefType(), typeof(AbstractModel).MakeByRefType()]);
    private static void Postfix(CardModel __instance, ref bool __result, ref UnplayableReason reason, ref AbstractModel? preventer)
    {
        if (!__result && (WeiCaoCaoZhiDuiPlay.CanCoverHandCard(__instance, reason) ||
            (WeiCaoCaoFuPlay.IsChooser(__instance) && WeiCaoCaoFuPlay.CanPlayEither(__instance))))
        { __result = true; reason = UnplayableReason.None; preventer = null; }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.TargetType), MethodType.Getter)]
internal static class WeiCaoCaoFuChooserTarget
{
    private static void Postfix(CardModel __instance, ref TargetType __result)
    {
        if (__instance.Pile?.Type == PileType.Hand && WeiCaoCaoFuPlay.IsChooser(__instance)) __result = TargetType.Self;
    }
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.DynamicExtraCardText), MethodType.Getter)]
internal static class WeiCaoCaoFuCardText
{
    private static void Postfix(EnchantmentModel __instance, ref LocString? __result)
    {
        if (__instance is not WeiCaoCaoFu fu || fu.IsCanonical || __result == null) return;
        __result.Add("AlternateName", fu.HasCard && WeiCaoCaoFu.Resolve(fu.AlternateEntry) != null
            ? WeiCaoCaoFuPlay.AliasPreview(fu.Card).Title : "？");
    }
}
