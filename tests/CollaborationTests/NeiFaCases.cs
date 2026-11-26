using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;

internal static class NeiFaCases
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;
    private static int _removed;

    public static async Task Run(Assembly mod, string dependencyRoot, Action<bool, string> check)
    {
        Type powerType = mod.GetType("Yjcm.NeiFaPower", true)!;
        var count = powerType.GetMethod("CountBlockedCards", Flags)!;
        var configure = powerType.GetMethod("Configure")!;
        var remaining = powerType.GetProperty("RemainingFreePlays")!;
        Player Player()
        {
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            typeof(Player).GetField("<Creature>k__BackingField", Flags)!.SetValue(player, new Creature(player, 80, 80));
            return player;
        }
        var owner = Player();
        var teammate = Player();
        CardModel Card(CardModel card, Player? player = null)
        {
            typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(card, true);
            card.Owner = player ?? owner;
            return card;
        }
        PowerModel Power()
        {
            var power = (PowerModel)Activator.CreateInstance(powerType)!;
            typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(power, true);
            typeof(PowerModel).GetField("_owner", Flags)!.SetValue(power, owner.Creature);
            typeof(PowerModel).GetField("_amount", Flags)!.SetValue(power, 1);
            return power;
        }
        int Left(PowerModel power) => (int)remaining.GetValue(power)!;
        CardPlay Play(CardModel card, int index = 0, bool auto = false)
        {
            // Beta adds a required Player member; initialize the actual API shape without two test bodies.
            var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
            foreach (var (name, value) in new (string, object?)[] {
                ("Card", card), ("Target", null), ("ResultPile", PileType.Discard),
                ("Resources", default(ResourceInfo)), ("IsAutoPlay", auto), ("PlayIndex", index), ("PlayCount", 3) })
                typeof(CardPlay).GetProperty(name)!.SetValue(play, value);
            typeof(CardPlay).GetProperty("Player")?.SetValue(play, card.Owner);
            return play;
        }
        var attack = Card(new StrikeIronclad());
        var skill = Card(new DefendIronclad());
        var ability = Card(new Inflame());
        var status = Card(new Dazed());
        var curse = Card(new Regret());
        List<CardModel> hand = [attack, skill, ability, status, curse, Card(new DefendIronclad())];
        int Count(CardType type) => (int)count.Invoke(null, [hand, type])!;
        check(Count(CardType.Skill) == 2, "Only blocked skills count; powers/statuses/curses do not");
        check(Count(CardType.Attack) == 1, "Reverse mode must count only attacks");
        var power = Power();
        configure.Invoke(power, [CardType.Skill, CardType.Attack, Count(CardType.Skill)]);
        hand.Add(Card(new DefendIronclad()));
        check(Left(power) == 2 && Count(CardType.Skill) == 3, "Later draws must not refill the snapshot allowance");
        hand.Remove(skill);
        check(Left(power) == 2, "Later discards must not recompute the allowance");
        check(!power.ShouldPlay(skill, default) && power.ShouldPlay(ability, default), "Original type restriction changed");
        var otherAttack = Card(new StrikeIronclad(), teammate);
        var otherSkill = Card(new DefendIronclad(), teammate);
        check(power.ShouldPlay(otherSkill, default), "NeiFa must not prohibit a teammate's skill");
        for (int i = 0; i < 5; i++)
        {
            check(power.TryModifyEnergyCostInCombat(attack, 2m, out decimal cost) && cost == 0m,
                "Matching attacks should preview as free while charges remain");
            check(Left(power) == 2, "Cost previews must never consume charges");
        }
        check(!power.TryModifyEnergyCostInCombat(otherAttack, 2m, out decimal otherCost) && otherCost == 2m,
            "A teammate must not receive free plays");
        await power.BeforeCardPlayed(Play(otherAttack));
        await power.BeforeCardPlayed(Play(ability));
        check(Left(power) == 2, "Other players/types must not consume charges");
        await power.BeforeCardPlayed(Play(attack));
        check(Left(power) == 1 && power.DisplayAmount == 1, "One actual play must reserve one allowance");
        await power.BeforeCardPlayed(Play(attack, 1));
        await power.BeforeCardPlayed(Play(attack, 2));
        check(Left(power) == 1, "Replay repetitions are not additional paid plays");
        await power.BeforeCardPlayed(Play(attack, auto: true));
        check(Left(power) == 0, "A separate next play, including auto-play, consumes the last allowance");
        check(!power.TryModifyEnergyCostInCombat(attack, 2m, out decimal restored) && restored == 2m,
            "Costs must return to normal at zero");
        check(!power.ShouldPlay(skill, default), "Exhausting free uses must not remove the type prohibition");
        await power.BeforeCardPlayed(Play(attack));
        check(Left(power) == 0, "Charges must not become negative");

        configure.Invoke(power, [CardType.Attack, CardType.Skill, 1]);
        check(!power.ShouldPlay(attack, default) && power.ShouldPlay(skill, default), "Reverse mode restriction failed");
        check(power.TryModifyEnergyCostInCombat(skill, 0m, out decimal zero) && zero == 0m, "Zero-cost matching card is in allowance");
        await power.BeforeCardPlayed(Play(skill));
        check(Left(power) == 0 && !power.ShouldPlay(attack, default), "Reverse mode zero must retain attack prohibition");
        configure.Invoke(power, [CardType.Skill, CardType.Attack, -1]);
        check(Left(power) == 0 && !power.ShouldPlay(skill, default), "No blocked cards means no free uses, not no restriction");

        // Use the native saved-property serializer/deserializer, including the zero state.
        Type savedCache = typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache")
            ?? typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        var inject = savedCache.GetMethod("InjectTypeIntoCache");
        FieldInfo? initialized = null;
        bool oldInitialized = false;
        if (inject != null) inject.Invoke(null, [powerType]);
        else
        {
            // Beta merges saved-property and model-ID caches. Seed only this model using its native
            // test hook; do not bootstrap the full game's models or Godot-dependent logging.
            savedCache.GetMethod("CacheSavedPropertiesForTypeDebug")!.Invoke(null, [powerType]);
            initialized = savedCache.GetField("_initialized", Flags)!;
            oldInitialized = (bool)initialized.GetValue(null)!;
            initialized.SetValue(null, true);
        }
        try
        {
            foreach (var (blocked, free) in new[] { (CardType.Skill, CardType.Attack), (CardType.Attack, CardType.Skill) })
            foreach (int allowance in new[] { 0, 1, 3 })
            {
                configure.Invoke(power, [blocked, free, allowance]);
                var data = SavedProperties.FromInternal(power, null)!;
                var options = new JsonSerializerOptions { IncludeFields = true };
                string json = JsonSerializer.Serialize(data, options);
                var restoredPower = Power();
                JsonSerializer.Deserialize<SavedProperties>(json, options)!.FillInternal(restoredPower);
                check(Left(restoredPower) == allowance, "Save round-trip lost free-use allowance");
                check((int)powerType.GetProperty("BlockedCardType")!.GetValue(restoredPower)! == (int)blocked,
                    "Save round-trip lost prohibited type");
                check((int)powerType.GetProperty("FreeCardType")!.GetValue(restoredPower)! == (int)free,
                    "Save round-trip lost free type");

                var loc = restoredPower.Description;
                var variables = (Dictionary<string, object>)typeof(LocString).GetField("_variables", Flags)!.GetValue(loc)!;
                check((decimal)variables["Remaining"] == allowance, "Tooltip allowance is stale");
                check((bool)variables["BlocksSkills"] == (blocked == CardType.Skill), "Tooltip prohibited type is reversed");
                // The same ConditionalFormatter registered by the game, without booting LocManager/Godot.
                var smart = Assembly.LoadFrom(Path.Combine(dependencyRoot, "SmartFormat.dll")).GetType("SmartFormat.Smart", true)!;
                string powersFile = Path.Combine(AppContext.BaseDirectory, "../../../../../../mod/Yjcm/localization/zhs/powers.json");
                using var localization = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(powersFile)));
                string template = localization.RootElement.GetProperty("YJCM_POWER_NEI_FA.active_description").GetString()!;
                string rendered = (string)smart.GetMethod("Format", [typeof(string), typeof(object[])])!
                    .Invoke(null, [template, new object[] { variables }])!;
                string blockedName = blocked == CardType.Skill ? "技能牌" : "攻击牌";
                string freeName = free == CardType.Attack ? "攻击牌" : "技能牌";
                check(rendered == $"本回合不能使用{blockedName}，接下来的{allowance}张{freeName}免费。", "Tooltip formatting mismatch");
            }
        }
        finally { initialized?.SetValue(null, oldInitialized); }
        var harmony = new Harmony("Yjcm.tests.neifa");
        _removed = 0;
        try
        {
            harmony.Patch(typeof(PowerCmd).GetMethod("Remove", [typeof(PowerModel)])!, prefix:
                new HarmonyMethod(typeof(NeiFaCases).GetMethod(nameof(Remove), Flags)!));
            await power.AfterSideTurnEnd(null!, CombatSide.Enemy, [teammate.Creature]);
            check(_removed == 0, "Another creature's turn end must not remove prohibition");
            await power.AfterSideTurnEnd(null!, CombatSide.Player, [owner.Creature]);
            check(_removed == 1, "The owner's turn end must remove prohibition");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool Remove(PowerModel power, ref Task __result)
    {
        _removed++;
        __result = Task.CompletedTask;
        return false;
    }
}
