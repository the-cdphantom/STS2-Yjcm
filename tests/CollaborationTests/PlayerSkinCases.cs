using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.RunData;

// Uses the real RitsuLib per-player contribution, staging, save export and import code.
// No sockets, native Godot initialization or live user settings are involved.
internal static class PlayerSkinCases
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;

    public static void Run(Assembly mod, Action<bool, string> check)
    {
        Type api = mod.GetType("Yjcm.PlayerSkins", true)!;
        Type dataType = mod.GetType("Yjcm.PlayerSkinSelection", true)!;
        Type wu = mod.GetType("Yjcm.WuZhugeliang", true)!;
        Type dong = mod.GetType("Yjcm.WeiDongZhuo", true)!;
        object access = api.GetField("Selections", Flags)!.GetValue(null)!;
        object slot = access.GetType().GetField("_slot", Flags)!.GetValue(access)!;
        Type lib = typeof(RunSavedDataOptions);
        Type sessionType = lib.Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataLobbySession", true)!;
        Type documentType = lib.Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataDocument", true)!;
        object key = slot.GetType().GetProperty("SlotKey")!.GetValue(slot)!;
        object NewDocument() => Activator.CreateInstance(documentType, true)!;
        object NewSession() => Activator.CreateInstance(sessionType, true)!;
        RunState NewRun() => (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        object Invoke(object target, string method, params object?[] values) =>
            target.GetType().GetMethod(method, Flags)!.Invoke(target, values)!;
        object Data(string w, string d)
        {
            object value = Activator.CreateInstance(dataType)!;
            dataType.GetProperty("WuZhugeliang")!.SetValue(value, w);
            dataType.GetProperty("WeiDongZhuo")!.SetValue(value, d);
            return value;
        }
        string Resolve(Type type, object? data, bool local) =>
            (string)api.GetMethod("Resolve", Flags)!.Invoke(null, [type, data, local])!;
        string Json(object document) => Invoke(document, "ToRootObject").ToString()!;
        object RoundTrip(object document) => documentType.GetMethod("FromJson", Flags)!.Invoke(null, [Json(document)])!;
        object Read(RunState run, ulong id)
        {
            object?[] values = [run, id, null];
            MethodInfo getter = access.GetType().GetMethods().Single(m => m.Name == "TryGet" && m.GetParameters().Length == 3);
            check((bool)getter.Invoke(access, values)!, "Player skin disappeared in transport/save");
            return values[2]!;
        }

        var expected = new[] {
            (Id: 11UL, Wu: "mou_zhugeliang", Dong: "classic"),
            (Id: 22UL, Wu: "classic", Dong: "tai_shan_beng_shi"),
            (Id: 33UL, Wu: "mou_zhugeliang", Dong: "tai_shan_beng_shi"),
            (Id: 44UL, Wu: "classic", Dong: "classic") };
        object host = NewSession();
        foreach (var item in expected.Reverse())
        {
            object client = NewSession();
            Invoke(client, "SetPlayer", key, item.Id, Data(item.Wu, item.Dong));
            // A client cannot contribute another player's selection through the per-player slot.
            Invoke(client, "SetPlayer", key, 999UL, Data("mou_zhugeliang", "tai_shan_beng_shi"));
            object contribution = NewDocument();
            check((bool)Invoke(slot, "TryExportLobbyContribution", client, item.Id, false, contribution),
                "Client must contribute its own skin");
            check(!Json(contribution).Contains("999"), "Contribution must not include another player");
            object?[] raw = ["Yjcm", "character_skin", null];
            check((bool)documentType.GetMethod("TryGetRaw")!.Invoke(contribution, raw)!, "Skin contribution key mismatch");
            Invoke(slot, "ImportLobbyContribution", host, item.Id, false, raw[2]);
        }
        object snapshot = NewDocument();
        check((bool)Invoke(slot, "TryExportLobbyStaging", host, snapshot), "Host must commit merged player skins");
        var run = NewRun();
        Invoke(slot, "Import", run, RoundTrip(snapshot));
        var committedRun = NewRun();
        Invoke(slot, "CommitLobbyStaging", host, committedRun);
        foreach (RunState state in new[] { run, committedRun })
        foreach (var item in expected)
        {
            object value = Read(state, item.Id);
            foreach (bool local in new[] { false, true })
            {
                check(Resolve(wu, value, local) == item.Wu, "Wu skin must follow owner, not observer/order");
                check(Resolve(dong, value, local) == item.Dong, "Dong skin must follow owner, not observer/order");
            }
        }
        object save = NewDocument();
        Invoke(slot, "Export", run, save);
        var rejoined = NewRun();
        Invoke(slot, "Import", rejoined, RoundTrip(save));
        foreach (var item in expected)
        {
            object value = Read(rejoined, item.Id);
            check(Resolve(wu, value, false) == item.Wu, "Save/rejoin lost Wu selection");
            check(Resolve(dong, value, false) == item.Dong, "Save/rejoin lost Dong selection");
        }
        string before = Json(save);
        for (int i = 0; i < 10; i++)
            foreach (var item in expected) _ = Resolve(wu, Read(run, item.Id), false);
        object after = NewDocument();
        Invoke(slot, "Export", run, after);
        check(before == Json(after), "Visual reads must not mutate the saved selection");
        check(Resolve(wu, Data("unknown", "unknown"), false) == "classic", "Unknown Wu skin must fallback");
        check(Resolve(dong, Data("unknown", "unknown"), false) == "classic", "Unknown Dong skin must fallback");
        check(Resolve(typeof(Ironclad), Data("mou_zhugeliang", "tai_shan_beng_shi"), true) == "classic",
            "Skin mapping must not affect vanilla characters");

        Type settings = mod.GetType("Yjcm.WuZhugeliangSettings", true)!;
        settings.GetMethod("SetWuZhugeliangSkin", Flags)!.Invoke(null, [true]);
        settings.GetMethod("SetWeiDongZhuoSkin", Flags)!.Invoke(null, [true]);
        try
        {
            check(Resolve(wu, null, false) == "classic", "Unknown remote Wu must not inherit observer's skin");
            check(Resolve(dong, null, false) == "classic", "Unknown remote Dong must not inherit observer's skin");
            check(Resolve(wu, null, true) == "mou_zhugeliang", "Legacy local Wu preference must remain usable");
            check(Resolve(dong, null, true) == "tai_shan_beng_shi", "Legacy local Dong preference must remain usable");
            check(Resolve(wu, Data("classic", "classic"), true) == "classic", "Saved classic beats local preference");
            check(Resolve(dong, Data("classic", "classic"), true) == "classic", "Saved Dong classic beats local preference");
        }
        finally
        {
            settings.GetMethod("SetWuZhugeliangSkin", Flags)!.Invoke(null, [false]);
            settings.GetMethod("SetWeiDongZhuoSkin", Flags)!.Invoke(null, [false]);
        }
        var ready = mod.GetType("Yjcm.PlayerSkinReadyPatch", true)!.GetMethod("Prefix", Flags)!;
        check(ready.GetCustomAttribute<HarmonyPriority>()!.info.priority == Priority.First,
            "Selection must be staged before the ready trailer is flushed");
        check(!((RunSavedDataOptions)slot.GetType().GetProperty("Options")!.GetValue(slot)!).SyncLobbyOnChange,
            "Staging callbacks must not recursively broadcast themselves");
    }
}
