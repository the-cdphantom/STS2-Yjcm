using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;

internal static class ModEventScopeCases
{
    private const BindingFlags Flags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // A character from another mod assembly: not one of Yjcm's own characters,
    // but also not a vanilla game character.
    private sealed class OtherModCharacter : CharacterModel
    {
        public override Color NameColor => default;
        public override CharacterGender Gender => default;
        protected override CharacterModel? UnlocksAfterRunAs => null;
        public override int StartingHp => 1;
        public override int StartingGold => 0;
        public override CardPoolModel CardPool => null!;
        public override RelicPoolModel RelicPool => null!;
        public override PotionPoolModel PotionPool => null!;
        public override IEnumerable<CardModel> StartingDeck => [];
        public override IReadOnlyList<RelicModel> StartingRelics => [];
        public override float AttackAnimDelay => 0;
        public override float CastAnimDelay => 0;
        public override List<string> GetArchitectAttackVfx() => [];
    }

    public static void Run(Assembly mod, Action<bool, string> check)
    {
        var wuType = mod.GetType("Yjcm.WuZhugeliang", true)!;
        var vanilla = RunWith(typeof(Ironclad));
        var own = RunWith(wuType);
        var mixed = RunWith(typeof(Ironclad), wuType);
        var other = RunWith(typeof(OtherModCharacter));
        var otherWithWu = RunWith(typeof(OtherModCharacter), wuType);

        foreach (string eventName in new[] { "QuYuanTouJiang", "WoYaoYanPai" })
        {
            check(Allowed(mod, eventName, vanilla, "all_characters"),
                $"{eventName}: all characters must allow vanilla");
            check(!Allowed(mod, eventName, vanilla, "own_mod_characters"),
                $"{eventName}: own-mod scope must reject vanilla");
            check(!Allowed(mod, eventName, vanilla, "mod_characters"),
                $"{eventName}: mod scope must reject vanilla");
            check(!Allowed(mod, eventName, vanilla, "no_characters"),
                $"{eventName}: closed scope must reject vanilla");
            check(Allowed(mod, eventName, own, "own_mod_characters"),
                $"{eventName}: own-mod scope must allow WuZhugeliang");
            check(Allowed(mod, eventName, mixed, "own_mod_characters"),
                $"{eventName}: mixed party must allow when one player matches");
            check(Allowed(mod, eventName, other, "mod_characters"),
                $"{eventName}: mod scope must allow another mod character");
            check(!Allowed(mod, eventName, other, "own_mod_characters"),
                $"{eventName}: own-mod scope must reject another mod character");
            check(!Allowed(mod, eventName, mixed, "no_characters"),
                $"{eventName}: closed scope must reject every party");
        }

        check(!Allowed(mod, "ZiWuGuQiMou", vanilla, "all_characters"),
            "ZiWuGuQiMou: still requires WuZhugeliang");
        check(Allowed(mod, "ZiWuGuQiMou", own, "all_characters"),
            "ZiWuGuQiMou: WuZhugeliang must allow the event");
        check(Allowed(mod, "ZiWuGuQiMou", own, "own_mod_characters"),
            "ZiWuGuQiMou: own-mod scope must allow WuZhugeliang");
        check(Allowed(mod, "ZiWuGuQiMou", own, "mod_characters"),
            "ZiWuGuQiMou: mod scope must allow WuZhugeliang");
        check(!Allowed(mod, "ZiWuGuQiMou", own, "no_characters"),
            "ZiWuGuQiMou: closed scope must reject WuZhugeliang");
        check(!Allowed(mod, "ZiWuGuQiMou", other, "mod_characters"),
            "ZiWuGuQiMou: another mod character alone must not satisfy the WuZhugeliang condition");
        check(Allowed(mod, "ZiWuGuQiMou", otherWithWu, "mod_characters"),
            "ZiWuGuQiMou: another mod character plus WuZhugeliang must allow");
        check(!Allowed(mod, "ZiWuGuQiMou", otherWithWu, "no_characters"),
            "ZiWuGuQiMou: closed scope must reject a party containing WuZhugeliang");

        check(!Allowed(mod, "ShangFangGuZhiYu", vanilla, "all_characters"),
            "ShangFangGuZhiYu: must keep the linked-card precondition");
        check(!Allowed(mod, "ShangFangGuZhiYu", own, "all_characters"),
            "ShangFangGuZhiYu: a Yjcm character alone must not satisfy the linked-card precondition");
        check(!Allowed(mod, "ShangFangGuZhiYu", own, "no_characters"),
            "ShangFangGuZhiYu: closed scope must reject the event");

        var partners = mod.GetType("Yjcm.Interop.ShangFangGuPartners", true)!;
        var resolve = partners.GetMethods(Flags)
            .Single(method => method.Name == "Resolve" && method.GetParameters().Length == 4);
        var probe = new StrikeIronclad();
        var found = resolve.Invoke(null,
        [
            "sunqian-universe", "Squ.Cards.ShangfangguSigh",
            (Func<string, bool>)(id => id == "sunqian-universe"),
            (Func<string, string, CardModel?>)((_, _) => probe)
        ]);
        check(ReferenceEquals(found, probe),
            "ShangFangGuZhiYu: linked-card resolution must remain available");
        var missing = resolve.Invoke(null,
        [
            "sunqian-universe", "Squ.Cards.ShangfangguSigh",
            (Func<string, bool>)(_ => false),
            (Func<string, string, CardModel?>)((_, _) => probe)
        ]);
        check(missing is null,
            "ShangFangGuZhiYu: unloaded partner must not satisfy the precondition");

        var settings = mod.GetType("Yjcm.WuZhugeliangSettings", true)!;
        var dataType = mod.GetType("Yjcm.WuZhugeliangSettingsData", true)!;
        var data = Activator.CreateInstance(dataType)!;
        var dataScope = dataType.GetProperty("ModEventScope", Flags)!;
        check((string)dataScope.GetValue(data)! == "all_characters",
            "ModEventScope: new installs must default to all characters");
        dataScope.SetValue(data, "no_characters");
        check((string)dataScope.GetValue(data)! == "no_characters",
            "ModEventScope: the saved value must remain readable");

        var property = settings.GetProperty("ModEventScope", Flags)!;
        check((string)property.GetValue(null)! == "all_characters",
            "ModEventScope: unregistered settings must fall back to all characters");
        var matches = settings.GetMethod("MatchesAnyPlayerScope", Flags)!;
        check((bool)matches.Invoke(null, [vanilla, ""])!,
            "ModEventScope: a missing old value must normalize to all characters");
        check(!(bool)matches.Invoke(null, [vanilla, "no_characters"])!,
            "ModEventScope: closed scope must reject a vanilla-only party");
        var direct = settings.GetMethod("MatchesCharacterScope", Flags)!;
        check(!(bool)direct.Invoke(null, ["own_mod_characters", typeof(OtherModCharacter)])!,
            "ModEventScope: own-mod scope must reject another mod assembly");
        check((bool)direct.Invoke(null, ["mod_characters", typeof(OtherModCharacter)])!,
            "ModEventScope: mod scope must accept another mod assembly");
    }

    private static bool Allowed(Assembly mod, string eventName, RunState run, string scope) =>
        (bool)mod.GetType("Yjcm." + eventName, true)!
            .GetMethod("IsAllowedForScope", Flags)!
            .Invoke(null, [run, scope])!;

    private static RunState RunWith(params Type[] characterTypes)
    {
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        var players = new List<Player>();
        foreach (var characterType in characterTypes)
        {
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            var character = (CharacterModel)RuntimeHelpers.GetUninitializedObject(characterType);
            AccessTools.Field(typeof(Player), "<Character>k__BackingField")
                .SetValue(player, character);
            players.Add(player);
        }
        AccessTools.Field(typeof(RunState), "_players").SetValue(run, players);
        return run;
    }
}
