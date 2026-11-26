using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

internal static class QingShiDamageCases
{
    public static async Task Run(Assembly mod, Action<bool, string> check)
    {
        var owner = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var dealer = new Creature(owner, 80, 80);
        var target = new Creature(null!, 40, 40);
        AccessTools.Field(typeof(Player), "<Creature>k__BackingField").SetValue(owner, dealer);
        var relic = (RelicModel)Activator.CreateInstance(mod.GetType("Yjcm.YjcmQingShi", true)!)!;
        var isMutable = AccessTools.Property(typeof(AbstractModel), nameof(AbstractModel.IsMutable));
        isMutable.SetValue(relic, true);
        relic.Owner = owner;

        CardPlay Play(CardModel card)
        {
            isMutable.SetValue(card, true);
            card.Owner = owner;
            var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
            typeof(CardPlay).GetProperty("Card")!.SetValue(play, card);
            return play;
        }

        var hit = Play(new StrikeIronclad());
        var later = Play(new StrikeIronclad());
        var skill = Play(new DefendIronclad());
        var arm = AccessTools.Method(relic.GetType().BaseType, "ArmDoubleDamage");
        var nativeType = typeof(DoubleDamagePower);
        bool hasNativeModel = ModelDb.Contains(nativeType);
        var context = new ThrowingPlayerChoiceContext();
        var harmony = new Harmony("Yjcm.tests.qing-shi-damage");
        try
        {
            ModelDb.Inject(nativeType);
            var playWrapper = AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper));
            harmony.CreateClassProcessor(mod.GetType("Yjcm.QingShiCardPlayContextPatch", true)!).Patch();
            check(Harmony.GetPatchInfo(playWrapper)?.Owners.Contains(harmony.Id) == true,
                "QingShi must capture the native play wrapper's choice context");

            decimal Damage(CardPlay play, ValueProp props = ValueProp.Move) =>
                relic.ModifyDamageMultiplicative(target, 5m, props, dealer, play.Card, play);

            arm.Invoke(relic, [hit]);
            check(Damage(hit) == 2m && Damage(hit) == 2m, "All qualifying hits from the current attack play must double");
            check(Damage(later) == 1m, "The immediate doubling must not leak to a different physical copy");
            await relic.AfterCardPlayed(context, hit);
            check(Damage(hit) == 1m, "The current attack's doubling must end with that play");

            arm.Invoke(relic, [skill]);
            check(Damage(later, ValueProp.Move | ValueProp.Unpowered) == 1m, "Unpowered damage must not double");
            check(Damage(later) == 2m && Damage(later) == 2m, "Previews must not consume pending doubling");
            await relic.AfterDamageGiven(context, dealer, new DamageResult(target, ValueProp.Move), ValueProp.Move, target, later.Card);
            check(Damage(later) == 1m, "One resolved qualifying damage must consume pending doubling");
            check(dealer.Powers.Count == 0, "The calculation must not install a turn-long native buff");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            if (!hasNativeModel)
                ModelDb.Remove(nativeType);
        }
    }
}
