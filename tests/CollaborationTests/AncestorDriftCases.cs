using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
#if GAME_V0114
using MegaCrit.Sts2.Core.Runs;
#endif
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

internal static class AncestorDriftCases
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<string> Events = [];
    private static List<CardModel> _hand = [];
    private static Func<Task>? _nested;
    private static object? _context;
    private static Action<bool, string> Check = null!;

    public static async Task Run(Assembly mod, string dependencies, Action<bool, string> check)
    {
        Check = check;
        Type ancestor = mod.GetType("Yjcm.WeiDongZhuoZhuGeJingZuYin", true)!;
        Type drift = mod.GetType("Yjcm.WeiDongZhuoQinYiLuPiaoPing", true)!;
        Type powerType = mod.GetType("Yjcm.PiaoPingPower", true)!;
        var owner = Player();
        var teammate = Player();
        _hand = Hand(owner);
        CardModel Card(Type type, Player? player = null)
        {
            var card = (CardModel)Activator.CreateInstance(type)!;
            typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(card, true);
            card.Owner = player ?? owner;
            return card;
        }
        PowerModel Power()
        {
            var result = (PowerModel)Activator.CreateInstance(powerType)!;
            typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(result, true);
            typeof(PowerModel).GetField("_owner", Flags)!.SetValue(result, owner.Creature);
            typeof(PowerModel).GetField("_amount", Flags)!.SetValue(result, 1);
            return result;
        }
        var a = Card(ancestor);
        var p = Card(drift);
        foreach (var (card, cost, type) in new[] { (a, 2, CardType.Attack), (p, 1, CardType.Power) })
        {
            check(card.Rarity == CardRarity.Uncommon && card.Type == type && card.EnergyCost.Canonical == cost, "New card metadata differs");
            var attribute = card.GetType().GetCustomAttributesData().Single(x => x.AttributeType.Name == "RegisterCardAttribute");
            check(((Type)attribute.ConstructorArguments[0].Value!).Name == "WeiDongZhuoCardPool", "New cards must register in Dongzhuo only");
            var tags = (HashSet<CardTag>)card.GetType().GetProperty("CanonicalTags", Flags)!.GetValue(card)!;
            check(tags.Count == 2, "New cards need exactly WuJiang and Qun tags");
        }
        check(a.CanonicalKeywords.Contains(CardKeyword.Retain) && a.DynamicVars.Damage.BaseValue == 0, "Ancestor base stats/Retain differ");
        var upgraded = Card(ancestor);
        upgraded.UpgradeInternal();
        upgraded.FinalizeUpgradeInternal();
        check(upgraded.DynamicVars.Damage.BaseValue == 10, "Ancestor upgrade must start at ten");
        p.UpgradeInternal();
        p.Owner = null!;
        check(p.EnergyCost.GetResolved() == 0, "Drift upgrade must cost zero");
        p.Owner = owner;

        var harmony = new Harmony("Yjcm.tests.ancestor-drift");
        try
        {
#if GAME_V0114
            harmony.CreateClassProcessor(mod.GetType("Yjcm.ZuYinDamageV0114Patch", true)!).Patch();
#else
            harmony.CreateClassProcessor(mod.GetType("Yjcm.ZuYinIncomingDamagePatch", true)!).Patch();
#endif
            _hand.Add(a);
            int Damage(int amount, ValueProp props = ValueProp.Move) => DamageCreature(owner.Creature, amount, props);
            bool Reducing(CardModel card) => (bool)ancestor.GetProperty("ReduceNext")!.GetValue(card)!;
            check(Damage(9) == 9 && a.DynamicVars.Damage.BaseValue == 9 && Reducing(a), "First actual damage must charge ancestor");
#if GAME_V0114
            int hp = owner.Creature.CurrentHp;
            var preview = CreatureCmd.GetDamageResultsPreview(NullRunState.Instance, null, owner.Creature, 7, ValueProp.Move, null, a).Single();
            check(preview.UnblockedDamage == 3 && owner.Creature.CurrentHp == hp && a.DynamicVars.Damage.BaseValue == 9 && Reducing(a),
                "Damage preview must halve the displayed hit without changing HP or ancestor state");
            check(owner.Creature.LoseHpInternal(1).hpLost == 1 && a.DynamicVars.Damage.BaseValue == 9 && Reducing(a),
                "A direct HP write outside damage settlement must not change ancestor");
#endif
            check(Damage(7) == 3 && a.DynamicVars.Damage.BaseValue == 2 && !Reducing(a), "Second hit must halve the same 7 damage and subtract pre-halving seven");
            check(Damage(0) == 0 && !Reducing(a), "Fully blocked damage must not advance phase");
            check(Damage(1) == 1 && Reducing(a), "Third hit should charge normally");
            check(Damage(1) == 0 && !Reducing(a), "A 1-to-0 reduction still consumes the second phase");
            check(a.DynamicVars.Damage.BaseValue == 2, "Damage accumulation is wrong after odd hits");
            Damage(2);
            check(Damage(30) == 15 && a.DynamicVars.Damage.BaseValue == 0, "Ancestor subtraction must floor at zero");
            check(Damage(4, ValueProp.Unpowered | ValueProp.Unblockable) == 4 && !Reducing(a), "Special raw HP loss must bypass ancestor and not advance it");
            _hand.Clear();
            check(Damage(5) == 5 && a.DynamicVars.Damage.BaseValue == 0 && !Reducing(a), "Cards outside hand must not trigger");
            _hand.Add(upgraded);
            check(Damage(6) == 6 && upgraded.DynamicVars.Damage.BaseValue == 16, "Upgraded damage must remain ten plus received damage");
            check(Damage(20) == 10 && upgraded.DynamicVars.Damage.BaseValue == 0, "Upgraded subtraction must floor at zero");
            _hand.Clear();
            var b = Card(ancestor);
            _hand.Add(a);
            _hand.Add(b);
            check(Damage(8) == 8 && a.DynamicVars.Damage.BaseValue == 8 && b.DynamicVars.Damage.BaseValue == 8, "Two copies must each record the actual hit");
            check(Damage(8) == 2 && a.DynamicVars.Damage.BaseValue == 0 && b.DynamicVars.Damage.BaseValue == 4, "Two reducing copies must resolve in stable hand order");
            var otherCard = Card(ancestor, teammate);
            Hand(teammate).Add(otherCard);
            check(DamageCreature(teammate.Creature, 4, ValueProp.Move) == 4 && otherCard.DynamicVars.Damage.BaseValue == 4 && a.DynamicVars.Damage.BaseValue == 0,
                "Teammate damage must only change their own hand");

            var draw = typeof(CardPileCmd).GetMethods().Single(x => x.Name == "Draw" && x.GetParameters().Length == 4);
            harmony.Patch(draw, prefix: new HarmonyMethod(typeof(AncestorDriftCases).GetMethod(nameof(Draw), Flags)!));
            harmony.Patch(typeof(CardSelectCmd).GetMethod("FromHandForDiscard")!, prefix:
                new HarmonyMethod(typeof(AncestorDriftCases).GetMethod(nameof(SelectDiscard), Flags)!));
            harmony.Patch(typeof(CardCmd).GetMethod("Discard", [typeof(PlayerChoiceContext), typeof(IEnumerable<CardModel>)])!, prefix:
                new HarmonyMethod(typeof(AncestorDriftCases).GetMethod(nameof(Discard), Flags)!));
            var power = Power();
            check(power.StackType.ToString() == "None", "Drift must not stack");
            var count = powerType.GetProperty("NextCount")!;
            var phase = powerType.GetProperty("NextIsDiscard")!;
            int Count() => (int)count.GetValue(power)!;
            bool IsDiscard() => (bool)phase.GetValue(power)!;
            var activation = Play(p);
            powerType.GetMethod("SkipActivationPlay", Flags)!.Invoke(power, [activation]);
            Events.Clear();
            _context = null;
            await power.AfterCardPlayed(null!, activation);
            check(Events.Count == 0 && Count() == 1 && !IsDiscard(), "Initial Drift play must not trigger itself");
            for (int i = 0; i < 12; i++)
            {
                FillHand();
                await power.AfterCardPlayed(null!, Play(Card(typeof(StrikeIronclad))));
                string expected = (i % 2 == 0 ? "draw:" : "discard:") + Math.Min(i + 1, 5);
                check(Events[^1] == expected && Count() == Math.Min(i + 2, 5), "Drift must follow draw1 discard2 draw3 discard4 draw5 discard5");
            }
            int eventCount = Events.Count;
            await power.AfterCardPlayed(null!, Play(Card(typeof(StrikeIronclad), teammate)));
            await power.AfterCardPlayed(null!, Play(p, 1));
            check(Events.Count == eventCount, "Other players and replay subplays must not trigger drift");
            await power.AfterPlayerTurnStart(null!, teammate);
            check(Count() == 5, "Other player turn must not reset count");
            await power.AfterPlayerTurnStart(null!, owner);
            check(Count() == 1 && !IsDiscard(), "Owner turn must reset number only");
            await power.AfterCardPlayed(null!, Play(p));
            check(Events[^1] == "draw:1" && IsDiscard(), "A later Drift copy is a normal next play and must not restart the effect");
            await power.AfterPlayerTurnStart(null!, owner);
            check(Count() == 1 && IsDiscard(), "Discard phase must survive turn start");
            _hand.Clear();
            _hand.Add(Card(typeof(StrikeIronclad)));
            count.SetValue(power, 5);
            await power.AfterCardPlayed(null!, Play(p));
            check(Events[^1] == "discard:1" && !IsDiscard(), "Insufficient hand must discard all available cards exactly once");
            phase.SetValue(power, true);
            _hand.Clear();
            eventCount = Events.Count;
            await power.AfterCardPlayed(null!, Play(p));
            check(Events.Count == eventCount && !IsDiscard(), "Empty hand must still advance the mandatory effect");

            // A sly card auto-played while discarding must consume the next phase,
            // not restart the discard or lose its trigger behind a reentrancy guard.
            phase.SetValue(power, true);
            count.SetValue(power, 2);
            FillHand();
            _nested = () => power.AfterCardPlayed(null!, Play(p));
            await power.AfterCardPlayed(null!, Play(p));
            check(Events.TakeLast(2).SequenceEqual(new[] { "discard:2", "draw:3" }) && Count() == 4 && IsDiscard(), "Nested auto-play must advance with the original context");

            SaveRoundTrips([ancestor, powerType], () => Card(ancestor), Power, power, count, phase, dependencies, check);

            void FillHand()
            {
                _hand.Clear();
                for (int i = 0; i < 7; i++) _hand.Add(Card(typeof(StrikeIronclad)));
            }
        }
        finally { _nested = null; harmony.UnpatchAll(harmony.Id); }
    }

    private static int DamageCreature(Creature target, int amount, ValueProp props)
    {
#if GAME_V0114
        var run = NullRunState.Instance;
        var results = (DamageResults)AccessTools.Method(typeof(CreatureCmd), "GetDamageResults")
            .Invoke(null, [run, null, target, (decimal)amount, props, null, null, null])!;
        int originalHp = target.CurrentHp;
        var apply = (Task)AccessTools.Method(typeof(CreatureCmd), "ApplyDamageResults")
            .Invoke(null, [run, null, results])!;
        apply.GetAwaiter().GetResult();
        int hpLost = originalHp - target.CurrentHp;
        Check(results.original.UnblockedDamage == hpLost, "Damage result must agree with HP actually lost");
        return hpLost;
#else
        return target.LoseHpInternal(amount, props).UnblockedDamage;
#endif
    }

    private static void SaveRoundTrips(Type[] types, Func<CardModel> cardFactory, Func<PowerModel> powerFactory,
        PowerModel power, PropertyInfo count, PropertyInfo phase, string dependencies, Action<bool, string> check)
    {
        var cache = typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") ??
            typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        var inject = cache.GetMethod("InjectTypeIntoCache");
        FieldInfo? gate = null;
        bool old = false;
        foreach (var type in types)
            (inject ?? cache.GetMethod("CacheSavedPropertiesForTypeDebug")!).Invoke(null, [type]);
        if (inject == null)
        {
            gate = cache.GetField("_initialized", Flags)!;
            old = (bool)gate.GetValue(null)!;
            gate.SetValue(null, true);
        }
        var options = new JsonSerializerOptions { IncludeFields = true };
        void RoundTrip(AbstractModel source, AbstractModel target) =>
            JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.FromInternal(source, null), options), options)!.FillInternal(target);
        try
        {
            foreach (bool discard in new[] { false, true })
            foreach (int n in new[] { 1, 3, 5 })
            {
                count.SetValue(power, n);
                phase.SetValue(power, discard);
                var restored = powerFactory();
                RoundTrip(power, restored);
                check((int)count.GetValue(restored)! == n && (bool)phase.GetValue(restored)! == discard, "Save round-trip must retain phase and number");
                var variables = (Dictionary<string, object>)typeof(MegaCrit.Sts2.Core.Localization.LocString).GetField("_variables", Flags)!.GetValue(restored.Description)!;
                var smart = Assembly.LoadFrom(Path.Combine(dependencies, "SmartFormat.dll")).GetType("SmartFormat.Smart", true)!;
                using var loc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../mod/Yjcm/localization/zhs/powers.json"))));
                string template = loc.RootElement.GetProperty("YJCM_POWER_PIAO_PING.active_description").GetString()!;
                string rendered = (string)smart.GetMethod("Format", [typeof(string), typeof(object[])])!.Invoke(null, [template, new object[] { variables }])!;
                check(rendered.Contains($"下一次：{(discard ? "弃" : "摸")}{n}张牌"), "Drift tooltip must show actual next effect");
            }
            foreach (bool upgraded in new[] { false, true })
            foreach (int change in new[] { 0, 15, -10 })
            {
                var card = cardFactory();
                var restored = cardFactory();
                if (upgraded)
                {
                    card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                    restored.UpgradeInternal(); restored.FinalizeUpgradeInternal();
                }
                types[0].GetProperty("DamageChange")!.SetValue(card, change);
                types[0].GetProperty("ReduceNext")!.SetValue(card, true);
                RoundTrip(card, restored);
                check(card.DynamicVars.Damage.BaseValue == restored.DynamicVars.Damage.BaseValue && (bool)types[0].GetProperty("ReduceNext")!.GetValue(restored)!,
                    "Ancestor save must retain accumulated damage and next phase");
                var nativeOrder = cardFactory();
                RoundTrip(card, nativeOrder);
                if (upgraded) { nativeOrder.UpgradeInternal(); nativeOrder.FinalizeUpgradeInternal(); }
                check(card.DynamicVars.Damage.BaseValue == nativeOrder.DynamicVars.Damage.BaseValue,
                    "Native load restores properties before upgrades; negative adjustment must survive");
            }
        }
        finally { gate?.SetValue(null, old); }
    }

    private static Player Player()
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var state = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        typeof(Player).GetProperty("PlayerCombatState")!.SetValue(player, state);
        typeof(PlayerCombatState).GetField("<Hand>k__BackingField", Flags)!.SetValue(state, new CardPile(PileType.Hand));
        typeof(Player).GetField("<Creature>k__BackingField", Flags)!.SetValue(player, new Creature(player, 1000, 1000));
        return player;
    }
    private static List<CardModel> Hand(Player player) => (List<CardModel>)typeof(CardPile).GetField("_cards", Flags)!.GetValue(player.PlayerCombatState!.Hand)!;
    private static CardPlay Play(CardModel card, int index = 0)
    {
        var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
        foreach (var (name, value) in new (string, object?)[] { ("Card", card), ("Target", null), ("ResultPile", PileType.Discard),
            ("Resources", default(ResourceInfo)), ("IsAutoPlay", false), ("PlayIndex", index), ("PlayCount", 2) })
            typeof(CardPlay).GetProperty(name)!.SetValue(play, value);
        typeof(CardPlay).GetProperty("Player")?.SetValue(play, card.Owner);
        return play;
    }
    private static bool Draw(PlayerChoiceContext choiceContext, decimal count, ref Task<IEnumerable<CardModel>> __result)
    {
        Check(ReferenceEquals(choiceContext, _context), "Draw replaced the action context");
        Events.Add("draw:" + count);
        __result = Task.FromResult(Enumerable.Empty<CardModel>());
        return false;
    }
    private static bool SelectDiscard(PlayerChoiceContext context, CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
    {
        Check(ReferenceEquals(context, _context), "Discard selection replaced the action context");
        Check(prefs.MinSelect == prefs.MaxSelect && !prefs.Cancelable && prefs.MinSelect > 0, "Drift discard must be mandatory");
        __result = Task.FromResult<IEnumerable<CardModel>>(_hand.Take(prefs.MinSelect).ToArray());
        return false;
    }
    private static bool Discard(PlayerChoiceContext choiceContext, IEnumerable<CardModel> cards, ref Task __result)
    {
        var selected = cards.ToArray();
        Events.Add("discard:" + selected.Length);
        foreach (var card in selected) _hand.Remove(card);
        var nested = _nested;
        _nested = null;
        __result = nested?.Invoke() ?? Task.CompletedTask;
        return false;
    }
}
