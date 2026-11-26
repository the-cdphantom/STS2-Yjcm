using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.CardTargeting;

// Exercise the production hook/target resolver with the native choice synchronizers.
// Replace only engine UI, network transport and the final auto-play dispatch.
internal static class TanBanCases
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static PlayerChoiceSynchronizer Sync = null!;
    private static INetGameService Net = null!;
    private static bool Local, Ending, Valid = true;
    private static int UiCalls, AutoPlays;
    private static Func<CardModel, Task<Creature?>> Ui = null!;
    private static CardModel? Selected;
    private static Creature? PlayedTarget;
    private static Action? OnHandSelection;
    private static Func<CardModel, bool>? LastFilter;
    private static PlayerChoiceContext? LastContext;

    public static async Task Run(Assembly mod, Action<bool, string> check)
    {
        var selector = mod.GetType("Yjcm.TanBanTargetSelection", true)!;
        var tanBanType = mod.GetType("Yjcm.TanBan", true)!;
        var harmony = new Harmony("Yjcm.tests.tanban");
        void Patch(Type type, string method, string prefix) => harmony.Patch(type.GetMethod(method, F)!,
            prefix: new HarmonyMethod(typeof(TanBanCases).GetMethod(prefix, F)!));
        async Task<Creature?> Select(Context context, CardModel card) =>
            await (Task<Creature?>)selector.GetMethod("SelectTargetAsync", F)!.Invoke(null, [context, card])!;
        bool Manual(TargetType type) => (bool)selector.GetMethod("RequiresManualTarget", F)!.Invoke(null, [type])!;

        try
        {
            Patch(typeof(MegaCrit.Sts2.Core.Logging.Logger), "GetIsRunningFromGodotEditor", nameof(NotEditor));
            Patch(typeof(MegaCrit.Sts2.Core.Logging.Logger).Assembly.GetType("MegaCrit.Sts2.Core.Logging.ConsoleLogPrinter", true)!, "Print", nameof(NoLog));
            Patch(typeof(RunManager), "get_PlayerChoiceSynchronizer", nameof(GetSync));
            Patch(typeof(RunManager), "get_NetService", nameof(GetNet));
            harmony.Patch(typeof(LocalContext).GetMethod("IsMe", [typeof(Player)])!,
                prefix: new HarmonyMethod(typeof(TanBanCases).GetMethod(nameof(IsMe), F)!));
            Patch(typeof(CombatManager), "get_IsOverOrEnding", nameof(IsEnding));
            Patch(typeof(CardModel), "IsValidTarget", nameof(IsValid));
            Patch(typeof(CombatState), "IterateHookListeners", nameof(NoOtherHooks));
            Patch(selector, "SelectLocalTargetAsync", nameof(LocalTarget));
            Patch(typeof(CardSelectCmd), "FromHand", nameof(SelectHand));
            Patch(typeof(CardCmd), "AutoPlay", nameof(AutoPlay));
            typeof(CustomTargetType).Assembly.GetType("STS2RitsuLib.Combat.CardTargeting.CustomTargetTypeRegistry", true)!
                .GetMethod("RegisterBuiltIns", F)!.Invoke(null, null);

            var (owner, combat) = Player();
            var target = new Creature((Player)null!, 80, 80) { CombatState = combat };
            combat.AddCreature(target);
            var card = NewCard(typeof(StrikeIronclad), owner);
            Hand(owner).Add(card);
            var players = new TestPlayers(owner);
            var hostNet = DispatchProxy.Create<INetGameService, NetProxy>();
            var remoteNet = DispatchProxy.Create<INetGameService, NetProxy>();
            ((NetProxy)hostNet).Mode = NetGameType.Host;
            ((NetProxy)remoteNet).Mode = NetGameType.Client;
            using var host = new PlayerChoiceSynchronizer(hostNet, players);
            using var remote = new PlayerChoiceSynchronizer(remoteNet, players);
            // Deliver the native serialized choice payload, including the -1 cancellation index.
            host.PlayerChoiceReceived += (p, id, result) => remote.ReceiveReplayChoice(p, id, result);
            Ui = _ => Task.FromResult<Creature?>(target);

            foreach (bool cancel in new[] { false, true })
            {
                UiCalls = 0;
                Ui = _ => Task.FromResult(cancel ? null : (Creature?)target);
                var remoteContext = new Context();
                Sync = remote; Net = remoteNet; Local = false;
                var waiting = Select(remoteContext, card);
                if (waiting.IsFaulted) await waiting;
                check(!waiting.IsCompleted && UiCalls == 0, $"Remote TanBan must wait for the owner without opening local targeting: {waiting.Status}, pile={card.Pile?.Type}, dead={owner.Creature.IsDead}");
                var hostContext = new Context();
                Sync = host; Net = hostNet; Local = true;
                var chosen = await Select(hostContext, card).WaitAsync(TimeSpan.FromSeconds(3));
                var received = await waiting.WaitAsync(TimeSpan.FromSeconds(3));
                check(ReferenceEquals(chosen, received) && ReferenceEquals(chosen, cancel ? null : target), "Peers must resolve the identical target or cancellation");
                check(UiCalls == 1 && host.ChoiceIds.SequenceEqual(remote.ChoiceIds), "Only owner targets and each peer reserves the same choice ID");
                check(hostContext.Begun == 1 && hostContext.Ended == 1 && remoteContext.Begun == 1 && remoteContext.Ended == 1,
                    "Choice contexts must end on both peers, including cancellation");
            }

            // Results can arrive before the remote action reaches target selection.
            Sync = host; Net = hostNet; Local = true;
            Ui = _ => Task.FromResult<Creature?>(target);
            var early = await Select(new Context(), card);
            Sync = remote; Net = remoteNet; Local = false;
            check(ReferenceEquals(early, await Select(new Context(), card).WaitAsync(TimeSpan.FromSeconds(3))), "Early native choice messages must not be lost");

            // Replay of the local owner's choices must not open UI either.
            ((NetProxy)remoteNet).Mode = NetGameType.Replay;
            Local = true;
            remote.ReceiveReplayChoice(owner, remote.ChoiceIds[0], PlayerChoiceResult.FromIndex(1).ToNetData());
            int before = UiCalls;
            check(ReferenceEquals(await Select(new Context(), card), target) && before == UiCalls, "Replay must consume recorded target without UI");
            ((NetProxy)remoteNet).Mode = NetGameType.Client;

            // Use an isolated native synchronizer for local boundary checks.
            using var isolated = new PlayerChoiceSynchronizer(hostNet, players);
            Sync = isolated; Net = hostNet; Local = true;
            foreach (string invalidation in new[] { "invalid", "removed", "ended", "moved", "owner-dead" })
            {
                Valid = true; Ending = false;
                if (!Hand(owner).Contains(card)) Hand(owner).Add(card);
                if (!combat.Creatures.Contains(target)) combat.AddCreature(target);
                var context = new Context();
                Ui = _ =>
                {
                    if (invalidation == "invalid") Valid = false;
                    if (invalidation == "removed")
                        foreach (string field in new[] { "_allies", "_enemies" })
                            ((List<Creature>)typeof(CombatState).GetField(field, F)!.GetValue(combat)!).Remove(target);
                    if (invalidation == "ended") Ending = true;
                    if (invalidation == "moved") Hand(owner).Remove(card);
                    if (invalidation == "owner-dead") owner.Creature.SetCurrentHpInternal(0);
                    return Task.FromResult<Creature?>(target);
                };
                check(await Select(context, card) == null && context.Ended == 1, "Stale target/card/combat must be skipped: " + invalidation);
                owner.Creature.SetCurrentHpInternal(80);
            }
            Valid = true; Ending = false;
            Hand(owner).Clear(); Hand(owner).Add(card);
            if (!combat.Creatures.Contains(target)) combat.AddCreature(target);
            var failed = new Context();
            int cancelled = 99;
            isolated.PlayerChoiceReceived += (_, _, result) => cancelled = PlayerChoiceResult.FromNetData(owner, players, result).AsIndex();
            Ui = _ => Task.FromException<Creature?>(new InvalidOperationException("UI failed"));
            try { await Select(failed, card); throw new Exception("UI exception was lost"); }
            catch (InvalidOperationException e) when (e.Message == "UI failed") { }
            check(failed.Ended == 1 && cancelled == -1, "Even a failed local UI must publish cancellation and end the choice");

            foreach (var type in new[] { TargetType.AnyEnemy, TargetType.AnyAlly, TargetType.AnyPlayer, CustomTargetType.AnyAttackingEnemy })
                check(Manual(type), "Single-target TanBan card must use manual synchronized selection: " + type);
            foreach (var type in new[] { TargetType.Self, TargetType.None, TargetType.AllEnemies })
                check(!Manual(type), "Automatic TanBan target must not open a picker: " + type);

            // Execute the actual enchantment hook, not a copied version of its logic.
            var source = NewCard(typeof(StrikeIronclad), owner);
            var db = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
            var id = ModelDb.GetId(tanBanType);
            if (!db.ContainsKey(id)) db[id] = (AbstractModel)Activator.CreateInstance(tanBanType)!;
            EnchantmentModel Enchant(CardModel c)
            {
                var e = ((EnchantmentModel)db[id]).ToMutable();
                c.EnchantInternal(e, 1);
                return e;
            }
            var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
            typeof(CardPlay).GetProperty("Card")!.SetValue(play, source);
            foreach (string outcome in new[] { "skip", "cancel", "play", "self", "aoe", "stale" })
            {
                Hand(owner).Clear();
                var selectedType = outcome == "self" ? typeof(DefendIronclad) : outcome == "aoe" ? mod.GetType("Yjcm.WeiDongZhuoWanJianQiFa", true)! : typeof(StrikeIronclad);
                Selected = outcome == "skip" ? null : NewCard(selectedType, owner);
                if (Selected != null) Hand(owner).Add(Selected);
                Ui = _ => Task.FromResult(outcome == "cancel" ? null : (Creature?)target);
                AutoPlays = 0; UiCalls = 0;
                var enchantment = Enchant(source);
                var context = new Context();
                OnHandSelection = outcome == "stale" ? () => Hand(owner).Clear() : null;
                await enchantment.AfterCardPlayed(context, play);
                check(source.Enchantment == null, "TanBan must be consumed even on skip/cancel: " + outcome);
                check(AutoPlays == (outcome is "play" or "self" or "aoe" ? 1 : 0), "Wrong auto-play dispatch after " + outcome);
                check(Selected == null || Selected.EnergyCost.GetResolved() == Selected.EnergyCost.Canonical, "TanBan must not leave a cost modifier after " + outcome);
                check(ReferenceEquals(LastContext, context), "Hand choice and auto-play must use the original action context");
                if (outcome is "self" or "aoe") check(UiCalls == 0 && PlayedTarget == null, "Self/AOE must auto-play without manual targeting");
                if (outcome == "play") check(ReferenceEquals(PlayedTarget, target), "TanBan must pass the confirmed enemy to AutoPlay");
            }
            var unplayable = NewCard(typeof(StrikeIronclad), owner);
            unplayable.AddKeyword(CardKeyword.Unplayable);
            check(!LastFilter!(unplayable) && !LastFilter(source), "TanBan must exclude unplayable cards and its source");
            var another = NewCard(typeof(StrikeIronclad), owner);
            Enchant(another);
            check(!LastFilter(another), "TanBan must exclude another TanBan card");
            check(LastFilter(NewCard(typeof(StrikeIronclad), owner)), "Low energy must not exclude an otherwise playable free target");
        }
        finally { harmony.UnpatchAll(harmony.Id); OnHandSelection = null; }
    }

    private static (Player, CombatState) Player()
    {
        var p = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var state = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        typeof(Player).GetProperty("PlayerCombatState")!.SetValue(p, state);
        foreach (var (name, type) in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            typeof(PlayerCombatState).GetField("<" + name + ">k__BackingField", F)!.SetValue(state, new CardPile(type));
        typeof(Player).GetField("<Deck>k__BackingField", F)!.SetValue(p, new CardPile(PileType.Deck));
        var combat = new CombatState();
        var creature = new Creature(p, 80, 80) { CombatState = combat };
        typeof(Player).GetField("<Creature>k__BackingField", F)!.SetValue(p, creature);
        combat.AddCreature(creature);
        return (p, combat);
    }
    private static CardModel NewCard(Type type, Player owner)
    {
        var card = (CardModel)Activator.CreateInstance(type)!;
        typeof(AbstractModel).GetProperty("IsMutable", F)!.SetValue(card, true);
        card.Owner = owner;
        return card;
    }
    private static List<CardModel> Hand(Player p) => (List<CardModel>)typeof(CardPile).GetField("_cards", F)!.GetValue(p.PlayerCombatState!.Hand)!;
    private static bool GetSync(ref PlayerChoiceSynchronizer __result) { __result = Sync; return false; }
    private static bool NotEditor(ref bool __result) { __result = false; return false; }
    private static bool NoLog() => false;
    private static bool GetNet(ref INetGameService __result) { __result = Net; return false; }
    private static bool IsMe(ref bool __result) { __result = Local; return false; }
    private static bool IsEnding(ref bool __result) { __result = Ending; return false; }
    private static bool IsValid(Creature? target, ref bool __result) { __result = target != null && Valid; return false; }
    private static bool NoOtherHooks(ref IEnumerable<AbstractModel> __result) { __result = []; return false; }
    private static bool LocalTarget(CardModel card, ref Task<Creature?> __result) { UiCalls++; __result = Ui(card); return false; }
    private static bool SelectHand(PlayerChoiceContext context, Func<CardModel, bool>? filter, ref Task<IEnumerable<CardModel>> __result)
    {
        LastContext = context; LastFilter = filter; OnHandSelection?.Invoke();
        __result = Task.FromResult<IEnumerable<CardModel>>(Selected == null ? [] : [Selected]); return false;
    }
    private static bool AutoPlay(PlayerChoiceContext choiceContext, Creature? target, ref Task __result)
    {
        LastContext = choiceContext; AutoPlays++; PlayedTarget = target; __result = Task.CompletedTask; return false;
    }
    private sealed class TestPlayers(Player player) : IPlayerCollection
    {
        public IReadOnlyList<Player> Players => [player];
        public int GetPlayerSlotIndex(Player p) => p == player ? 0 : -1;
        public Player? GetPlayer(ulong netId) => player.NetId == netId ? player : null;
    }
    public class NetProxy : DispatchProxy
    {
        public NetGameType Mode;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_Type" => Mode,
            _ when method.ReturnType == typeof(void) => null,
            _ when method.ReturnType.IsValueType => Activator.CreateInstance(method.ReturnType),
            _ => null
        };
    }
    private sealed class Context : PlayerChoiceContext
    {
        public int Begun, Ended;
#if GAME_V0113
        public override GameActionType? GameActionType => null;
#endif
#if GAME_BETA
        public override ulong? OwnerId => 0;
        public override Task SignalPlayerChoiceBegun(Player chooser, PlayerChoiceOptions options)
#else
        public override Task SignalPlayerChoiceBegun(PlayerChoiceOptions options)
#endif
        { Begun++; return Task.CompletedTask; }
        public override Task SignalPlayerChoiceEnded() { Ended++; return Task.CompletedTask; }
    }
}
