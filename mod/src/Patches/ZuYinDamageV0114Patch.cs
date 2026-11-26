#if GAME_V0114
#nullable enable
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

namespace Yjcm;

[HarmonyPatch]
internal static class ZuYinDamageV0114Patch
{
    private sealed class DamagePlan(ValueProp props)
    {
        internal ValueProp Props { get; } = props;
        internal ZuYinDamage.Pending[]?[] Hits { get; } = new ZuYinDamage.Pending[3][];
    }

    private static readonly ConditionalWeakTable<DamageResult, ZuYinDamage.Pending[]> PendingHits = new();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CreatureCmd), "GetDamageResults");
        yield return AccessTools.AsyncMoveNext(AccessTools.Method(typeof(CreatureCmd), "ApplyDamageResults"));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase original, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var loseHp = AccessTools.Method(typeof(Creature), nameof(Creature.LoseHpInternal), [typeof(decimal), typeof(bool)]);
        var calls = Enumerable.Range(0, codes.Count).Where(i => codes[i].Calls(loseHp)).ToArray();
        if (calls.Length != 3)
            throw new InvalidOperationException($"Unexpected HP loss calls in {original.DeclaringType}.{original.Name}");

        if (original.Name == "GetDamageResults")
        {
            // Keep the plan local to this damage calculation. Preview reaches the
            // same method but never asks ApplyDamageResults to consume its results.
            var plan = generator.DeclareLocal(typeof(DamagePlan));
            var calculate = AccessTools.Method(typeof(ZuYinDamageV0114Patch), nameof(Calculate));
            var associate = AccessTools.Method(typeof(ZuYinDamageV0114Patch), nameof(Associate));
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)4);
            yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(DamagePlan), [typeof(ValueProp)]));
            yield return new CodeInstruction(OpCodes.Stloc, plan);
            int slot = 0;
            foreach (var instruction in codes)
            {
                if (instruction.Calls(loseHp))
                {
                    yield return InsertBefore(instruction, new CodeInstruction(OpCodes.Ldloc, plan));
                    yield return new CodeInstruction(OpCodes.Ldc_I4, slot++);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = calculate;
                }
                if (instruction.opcode == OpCodes.Ret)
                {
                    yield return InsertBefore(instruction, new CodeInstruction(OpCodes.Ldloc, plan));
                    yield return new CodeInstruction(OpCodes.Call, associate);
                }
                yield return instruction;
            }
            yield break;
        }

        // Preserve the exact DamageResult on the stack while native code reads its
        // damage amount. Only the three real HP writes can commit a pending hit.
        var getDamage = AccessTools.PropertyGetter(typeof(DamageResult), nameof(DamageResult.UnblockedDamage));
        var apply = AccessTools.Method(typeof(ZuYinDamageV0114Patch), nameof(Apply));
        foreach (int i in calls.Reverse())
        {
            if (i < 3 || codes[i - 1].opcode != OpCodes.Ldc_I4_0 || !codes[i - 3].Calls(getDamage))
                throw new InvalidOperationException("Unexpected HP write in ApplyDamageResults");
            codes[i].opcode = OpCodes.Call;
            codes[i].operand = apply;
            codes.Insert(i - 3, InsertBefore(codes[i - 3], new CodeInstruction(OpCodes.Dup)));
        }
        foreach (var instruction in codes) yield return instruction;
    }

    private static CodeInstruction InsertBefore(CodeInstruction original, CodeInstruction inserted)
    {
        inserted.labels.AddRange(original.labels);
        original.labels.Clear();
        inserted.blocks.AddRange(original.blocks);
        original.blocks.Clear();
        return inserted;
    }

    private static HpLossResult Calculate(Creature target, decimal amount, bool dryRun, DamagePlan plan, int slot)
    {
        plan.Hits[slot] = ZuYinDamage.Prepare(target, ref amount, plan.Props);
        return target.LoseHpInternal(amount, dryRun);
    }

    private static DamageResults Associate(DamageResults results, DamagePlan plan)
    {
        Remember(results.preBlockRedirect, plan.Hits[0]);
        Remember(results.unblockedRedirect ?? results.original, plan.Hits[1]);
        if (results.unblockedRedirect != null)
            Remember(results.original, plan.Hits[2]);
        return results;
    }

    private static void Remember(DamageResult? result, ZuYinDamage.Pending[]? pending)
    {
        if (result != null && pending is { Length: > 0 })
            PendingHits.Add(result, pending);
    }

    private static HpLossResult Apply(Creature target, DamageResult result, decimal amount, bool dryRun)
    {
        HpLossResult loss = target.LoseHpInternal(amount, dryRun);
        if (PendingHits.TryGetValue(result, out var pending) && PendingHits.Remove(result))
            ZuYinDamage.Commit(pending, loss.hpLost);
        return loss;
    }
}
#endif
