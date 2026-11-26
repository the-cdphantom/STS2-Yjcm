using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

internal static class MonsterIntentCases
{
    private static readonly Creature Target = new(null!, 100, 100);
    private static int _blockCalls;
    private static int _intentRefreshes;
    private static bool RefreshIntent(MonsterModel __instance, MoveState state, bool forceTransition)
    {
        if (!forceTransition) throw new Exception("DunXi must refresh the selected intent");
        AccessTools.Property(typeof(MonsterModel),"NextMove").SetValue(__instance,state);
        _intentRefreshes++;
        return false;
    }
    private static bool CreatureGetter(MonsterModel __instance, ref Creature __result)
    {
        if (__instance is not InfestedPrism) return true;
        __result = Target;
        return false;
    }
    // Keep the production amount lookup and dispatch; isolate combat-wide hooks.
    private static bool ModifyBlock(decimal __2, ref decimal __result)
    {
        _blockCalls++;
        __result = __2 * 2;
        return false;
    }

    internal static void Run(Assembly mod, bool beta)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var support = mod.GetType("Yjcm.WeiCaoCaoMonsterIntentSupport", true)!;
        var buffs = Activator.CreateInstance(typeof(List<>).MakeGenericType(support.GetNestedType("Buff", BindingFlags.NonPublic)!))!;
        var prism = (InfestedPrism)RuntimeHelpers.GetUninitializedObject(typeof(InfestedPrism));
        var harmony = new Harmony("Yjcm.tests.infested-prism");
        int checks = 0;
        try
        {
            harmony.Patch(AccessTools.PropertyGetter(typeof(MonsterModel), "Creature"),
                prefix: new HarmonyMethod(typeof(MonsterIntentCases).GetMethod(nameof(CreatureGetter), flags)!));
            harmony.Patch(AccessTools.Method(typeof(Hook), "ModifyBlock"),
                prefix: new HarmonyMethod(typeof(MonsterIntentCases).GetMethod(nameof(ModifyBlock), flags)!));
            foreach (var (moveId, amountName) in new[] { ("PULSATE_MOVE", "PulsateBlock"), ("RADIATE_MOVE", "RadiateBlock") })
            {
                var move = new MoveState(moveId, _ => throw new InvalidOperationException("Must not execute monster action"), new DefendIntent());
                _blockCalls = 0;
                var result = (decimal)support.GetMethod("Read", flags)!.Invoke(null, [prism, move, Array.Empty<Creature>(), buffs])!;
                decimal expected = beta ? 0 : Convert.ToDecimal(AccessTools.Property(typeof(InfestedPrism), amountName)!.GetValue(prism)) * 2;
                if (result != expected || _blockCalls != (beta ? 0 : 1) || (!beta && expected <= 0))
                    throw new InvalidOperationException($"InfestedPrism {moveId}: result={result}, expected={expected}, hooks={_blockCalls}");
                checks++;
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine($"CAOCAO_INFESTED_PRISM_OK assertions={checks} beta={beta} nativeAmounts=true blockHooks=true");
        CheckOpeningReroll(mod);
    }

    private static void CheckOpeningReroll(Assembly mod)
    {
        var opening=new MoveState("OPENING",_=>throw new Exception("Must not perform monster actions"),new BuffIntent());
        var next=new MoveState("NEXT",_=>throw new Exception("Must not perform monster actions"),new DefendIntent());
        opening.FollowUpState=next;next.FollowUpState=opening;
        var machine=new MonsterMoveStateMachine([opening,next],opening);
        var monster=(KinFollower)RuntimeHelpers.GetUninitializedObject(typeof(KinFollower));
        AccessTools.Property(typeof(AbstractModel),"IsMutable").SetValue(monster,true);
        AccessTools.Field(typeof(MonsterModel),"_creature").SetValue(monster,new Creature(null!,100,100){CombatState=new CombatState()});
        AccessTools.Field(typeof(MonsterModel),"_moveStateMachine").SetValue(monster,machine);
        AccessTools.Field(typeof(MonsterModel),"_runRng").SetValue(monster,new RunRngSet(new Rng(42)));
        AccessTools.Property(typeof(MonsterModel),"NextMove").SetValue(monster,opening);
        var reroll=AccessTools.Method(mod.GetType("Yjcm.WeiCaoCaoIntentRules",true),"Reroll");
        var harmony=new Harmony("Yjcm.tests.dunxi-opening");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(MonsterModel),"SetMoveImmediate"),prefix:
                new HarmonyMethod(AccessTools.Method(typeof(MonsterIntentCases),nameof(RefreshIntent))));
            _intentRefreshes=0;
            ((Task)reroll.Invoke(null,[new ThrowingPlayerChoiceContext(),monster])!).GetAwaiter().GetResult();
            if(monster.NextMove!=next||_intentRefreshes!=1)throw new Exception("DunXi must change an opening intent before any enemy action");
            ((Task)reroll.Invoke(null,[new ThrowingPlayerChoiceContext(),monster])!).GetAwaiter().GetResult();
            if(monster.NextMove!=opening||_intentRefreshes!=2)throw new Exception("DunXi must work repeatedly during an extra player turn");
            opening.MustPerformOnceBeforeTransitioning=true;
            ((Task)reroll.Invoke(null,[new ThrowingPlayerChoiceContext(),monster])!).GetAwaiter().GetResult();
            if(monster.NextMove!=opening||_intentRefreshes!=2)throw new Exception("DunXi must preserve mandatory monster moves");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine("CAOCAO_DUNXI_OPENING_OK assertions=3 nativeStateMachine=true noMonsterActions=true");
    }
}
