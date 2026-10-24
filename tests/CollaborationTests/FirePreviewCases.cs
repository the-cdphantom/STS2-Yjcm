using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

// Exercise actual variables/tags; replace only global hooks and the optional partner boundary.
internal static class FirePreviewCases
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance;
    private static Action<bool, string> _check = null!;
    private static PowerModel _fire = null!, _burning = null!, _lastPower = null!;
    private static bool _ready, _actor;
    private static int _calls;
    private static decimal _bonus;
    private static CardTag _burningTag;

    public static void Run(Assembly assembly, Action<bool, string> check)
    {
        _check = check;
        _fire = (PowerModel)Activator.CreateInstance(assembly.GetType("Yjcm.HuoGongPower", true)!)!;
        _burning = new WeakPower(); // Mock partner's CANONICAL model; never a live target power.
        var models = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", Flags)!.GetValue(null)!;
        models[ModelDb.GetId(_fire.GetType())] = _fire;
        var api = assembly.GetType("Yjcm.Interop.SunqianUniverseInterop", true)!;
        var interop = assembly.GetType("Yjcm.Interop.YiZhenQingMengInterop", true)!;
        var harmony = new Harmony("Yjcm.tests.fire-preview");
        try
        {
            void Patch(MethodInfo method, string prefix) => harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(FirePreviewCases).GetMethod(prefix, Flags)!));
            Patch(typeof(Hook).GetMethod("ModifyPowerAmountGiven")!, nameof(Preview));
            Patch(api.GetProperty("IsReady")!.GetMethod!, nameof(Ready));
            Patch(api.GetMethod("IsSupportingActorCharacter")!, nameof(Actor));
            Patch(interop.GetMethod("CanonicalBurningPower", Flags)!, nameof(Burning));
            Patch(api.GetProperty("BurningCardTag")!.GetMethod!, nameof(Tag));
            _burningTag = (CardTag)0x456789;

            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            var creature = new Creature(player, 100, 100);
            typeof(Player).GetField("<Creature>k__BackingField", Flags)!.SetValue(player, creature);
            var combat = (CombatState)RuntimeHelpers.GetUninitializedObject(typeof(CombatState));
            creature.CombatState = combat;
            // Use native CardModel ownership/preview methods, with no Godot initialization or UI.
            var pileGetter = typeof(CardModel).GetProperty("Pile")!.GetMethod!;
            Patch(pileGetter, nameof(HandPile));
            var target = new Creature(null!, 100, 100);
            foreach (var (name, key, initial, upgrade) in new[] {
                ("HuoGong", "Stacks", 6m, 9m),
                ("WeiDongZhuoHuoGong", "Stacks", 6m, 9m),
                ("JieDongFeng", "HuoGong", 7m, 7m),
                ("MouZhugeliangHuoJi", "HuoGong", 10m, 15m),
                ("YiZhenQingMeng", "Stacks", 7m, 7m) })
            {
                var card = (CardModel)Activator.CreateInstance(assembly.GetType("Yjcm." + name, true)!)!;
                typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(card, true);
                card.Owner = player;
                foreach (bool upgraded in new[] { false, true })
                {
                    if (upgraded) card.GetType().GetMethod("OnUpgrade", Flags)!.Invoke(card, null);
                    var variable = card.DynamicVars[key];
                    decimal printed = upgraded ? upgrade : initial;
                    foreach (bool ready in new[] { false, true })
                    foreach (bool actor in new[] { false, true })
                    foreach (decimal bonus in new[] { 0m, 3m, -2m })
                    {
                        _ready = ready; _actor = actor; _bonus = bonus; _calls = 0;
                        variable.UpdateCardPreview(card, default, target, true);
                        check(variable.PreviewValue == printed + bonus, name + " must preview giving modifiers");
                        check(variable.BaseValue == printed && variable.EnchantedValue == printed,
                            name + " preview must not change combat/save/upgrade base values");
                        check(_calls == 1, name + " must apply preview hooks exactly once");
                        check(ReferenceEquals(_lastPower, name == "YiZhenQingMeng" && ready && actor ? _burning : _fire),
                            name + " must preview the actual holder's effect");
                        variable.UpdateCardPreview(card, default, null, false);
                        check(variable.PreviewValue == printed && _calls == 1,
                            name + " must clear stale preview outside hand/global hooks");
                    }
                }
                if (name != "YiZhenQingMeng") continue;
                foreach (bool ready in new[] { false, true })
                foreach (bool actor in new[] { false, true })
                {
                    _ready = ready; _actor = actor;
                    check(card.Tags.Contains(_burningTag) == (ready && actor),
                        "Only an actual actor-held dream is a burning card (including cached early reads)");
                    check(card.Tags.Count(t => t == _burningTag) <= 1, "Burning tag must not accumulate on preview");
                }
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool Preview(PowerModel power, decimal amount, CardModel cardSource,
        out IEnumerable<AbstractModel> modifiers, ref decimal __result)
    {
        _check(power.IsCanonical, "Preview must never bind/consume a live power");
        _calls++;
        _lastPower = power;
        modifiers = Array.Empty<AbstractModel>();
        __result = amount + _bonus;
        return false;
    }
    private static bool Ready(ref bool __result) { __result = _ready; return false; }
    private static bool Actor(ref bool __result) { __result = _actor; return false; }
    private static bool Burning(ref PowerModel? __result) { __result = _burning; return false; }
    private static bool Tag(ref CardTag __result) { __result = _burningTag; return false; }
    private static bool HandPile(ref CardPile? __result) { __result = new CardPile(PileType.Hand); return false; }
}
