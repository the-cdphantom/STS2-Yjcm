using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class ModEventRepeatScopeCases
{
    public static void Run(Assembly mod, Action<bool, string> check)
    {
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        AccessTools.Field(typeof(Player), "<Character>k__BackingField").SetValue(player, new Ironclad());
        AccessTools.Field(typeof(RunState), "_players").SetValue(run, new List<Player> { player });
        AccessTools.Field(typeof(RunState), "_visitedEventIds").SetValue(run, new HashSet<ModelId>());
        var restricted = (EventModel)Activator.CreateInstance(mod.GetType("Yjcm.ZiWuGuQiMou", true)!)!;
        var ordinary = new BattlewornDummy();
        var rooms = new RoomSet();
        rooms.events.AddRange([restricted, ordinary]);

        var harmony = new Harmony("Yjcm.tests.event-repeat-scope");
        try
        {
            // The fallback logs through Godot, which is not initialized in this test runner.
            harmony.Patch(AccessTools.Method(typeof(Log), nameof(Log.Warn)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModEventRepeatScopeCases), nameof(SkipNativeLog))));
            harmony.CreateClassProcessor(mod.GetType("Yjcm.ModEventRepeatScopePatch", true)!).Patch();
            rooms.EnsureNextEventIsValid(run);
            check(ReferenceEquals(rooms.NextEvent, ordinary), "Unique event selection must keep the native eligibility check");

            run.AddVisitedEvent(ordinary);
            rooms.eventsVisited = 0;
            rooms.EnsureNextEventIsValid(run);
            check(ReferenceEquals(rooms.NextEvent, ordinary), "Repeat fallback must skip an ineligible Yjcm event");
            check(rooms.eventsVisited == 3 && rooms.events.SequenceEqual([restricted, ordinary]),
                "Repeat fallback must preserve the shuffled pool and advance the saved cursor");

            rooms.eventsVisited = 1;
            rooms.EnsureNextEventIsValid(run);
            check(ReferenceEquals(rooms.NextEvent, ordinary) && rooms.eventsVisited == 3,
                "Repeat fallback must leave an ordinary candidate in place");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool SkipNativeLog() => false;
}
