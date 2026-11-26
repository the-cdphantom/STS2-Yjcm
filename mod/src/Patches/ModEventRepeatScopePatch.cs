using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Yjcm;

[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.EnsureNextEventIsValid))]
internal static class ModEventRepeatScopePatch
{
    // The native repeat fallback ignores IsAllowed after exhausting unique events.
    // Keep the shuffled order and saved cursor while skipping our ineligible events.
    private static void Postfix(RoomSet __instance, RunState runState)
    {
        int count = __instance.events.Count;
        for (int offset = 0; offset < count; offset++)
        {
            EventModel candidate = __instance.events[(__instance.eventsVisited + offset) % count];
            if (candidate is (QuYuanTouJiang or WoYaoYanPai or ShangFangGuZhiYu or ZiWuGuQiMou)
                && !candidate.IsAllowed(runState))
                continue;

            __instance.eventsVisited += offset;
            return;
        }
    }
}
