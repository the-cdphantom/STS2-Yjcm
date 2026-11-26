using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

internal static class SharedTreasureRelicCases
{
    private static string _scope = "own_mod_characters";
    private static IReadOnlySet<Type> _sharedTypes = new HashSet<Type>();

    public static void Run(Assembly mod, Action<bool, string> check)
    {
        T Model<T>(string type) => (T)Activator.CreateInstance(mod.GetType("Yjcm." + type, true)!)!;
        var regent = MakePlayer(new Regent());
        var caoCao = MakePlayer(Model<CharacterModel>("WeiCaoCao"));
        var restricted = Model<RelicModel>("SunHaoYuPao");
        var ordinary = new HappyFlower();
        _sharedTypes = new HashSet<Type> { restricted.GetType() };
        var harmony = new Harmony("Yjcm.tests.shared-treasure-relic");
        try
        {
            harmony.Patch(AccessTools.PropertyGetter(mod.GetType("Yjcm.WuZhugeliangSettings", true), "SharedRelicScope"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SharedTreasureRelicCases), nameof(ReadScope))));
            harmony.Patch(AccessTools.Method(mod.GetType("Yjcm.SharedRelicScopePatch", true), "GetSharedRelicTypes"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SharedTreasureRelicCases), nameof(ReadSharedTypes))));
            harmony.CreateClassProcessor(mod.GetType("Yjcm.SharedTreasureRelicScopePatch", true)!).Patch();

            RelicModel? Pull(Player[] players, bool shared = true, Func<RelicModel, bool>? filter = null)
            {
                var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
                var chest = new RelicGrabBag();
                var bag = shared ? chest : new RelicGrabBag();
                AccessTools.Field(typeof(RunState), "_players").SetValue(run, players.ToList());
                AccessTools.Field(typeof(RunState), "_mapPointHistory").SetValue(run, new List<List<MapPointHistoryEntry>>());
                AccessTools.Property(typeof(RunState), nameof(RunState.SharedRelicGrabBag)).SetValue(run, chest);
                var deques = (Dictionary<RelicRarity, List<RelicModel>>)
                    AccessTools.Field(typeof(RelicGrabBag), "_deques").GetValue(bag)!;
                deques.Add(RelicRarity.Common, [restricted, ordinary]);
                return filter == null
                    ? bag.PullFromFront(RelicRarity.Common, run)
                    : bag.PullFromFront(RelicRarity.Common, filter, run);
            }

            _scope = "own_mod_characters";
            check(Pull([regent]) == ordinary, "Regent chest must skip restricted Yjcm relics");
            check(Pull([caoCao]) == restricted, "An eligible solo character must keep access to shared relics");
            check(Pull([caoCao, regent]) == ordinary, "A mixed chest must not offer an ineligible teammate a Yjcm relic");
            check(Pull([caoCao, regent], shared: false) == restricted, "Chest rules must not modify personal bags");
            check(Pull([regent], filter: relic => relic != ordinary) == null, "Chest scope must preserve the caller's filter");

            _scope = "no_characters";
            check(Pull([caoCao]) == ordinary, "Closed scope must apply even to an eligible Mod character");
            _scope = "all_characters";
            check(Pull([caoCao, regent]) == restricted, "All characters scope must preserve the original mixed chest");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static Player MakePlayer(CharacterModel character)
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        AccessTools.Field(typeof(Player), "<Character>k__BackingField").SetValue(player, character);
        return player;
    }

    private static bool ReadScope(ref string __result)
    {
        __result = _scope;
        return false;
    }

    private static bool ReadSharedTypes(ref IReadOnlySet<Type> __result)
    {
        __result = _sharedTypes;
        return false;
    }
}
