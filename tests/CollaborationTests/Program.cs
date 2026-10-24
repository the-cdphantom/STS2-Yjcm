using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(Path.GetFullPath(args[1]), name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
int checks = 0;
void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
    checks++;
}
PowerModel Power(Type type, Creature owner)
{
    var power = (PowerModel)Activator.CreateInstance(type)!;
    typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(power, true);
    typeof(PowerModel).GetField("_owner", Flags)!.SetValue(power, owner);
    typeof(PowerModel).GetField("_amount", Flags)!.SetValue(power, 3);
    return power;
}
decimal Damage(PowerModel power, Creature target, Creature dealer, ValueProp props)
{
    var method = power.GetType().GetMethod("ModifyDamageMultiplicative")!;
    object?[] values = method.GetParameters().Length == 6
        ? [target, 100m, props, dealer, null, null]
        : [target, 100m, props, dealer, null];
    return (decimal)method.Invoke(power, values)!;
}
var owner = new Creature(null!, 100, 100);
var other = new Creature(null!, 100, 100);
var weak = Power(typeof(WeakPower), owner);
var vulnerable = Power(typeof(VulnerablePower), owner);
var frail = (FrailPower)Power(typeof(FrailPower), owner);
var card = new StrikeIronclad();
var move = ValueProp.Move;
var unpowered = move | ValueProp.Unpowered;
var harmony = new Harmony("Yjcm.tests.collaboration");
foreach (string suffix in new[] { "Weak", "Vulnerable", "Frail" })
    harmony.CreateClassProcessor(assembly.GetType($"Yjcm.ShuiHuoJianRong{suffix}Patch", true)!).Patch();

Check(Damage(weak, other, owner, move) == 0.75m, "Weak changed without ability");
Check(Damage(vulnerable, owner, other, move) == 1.5m, "Vulnerable changed without ability");
Check(frail.ModifyBlockMultiplicative(owner, 100, move, card, null) == 0.75m, "Frail changed without ability");
var reversal = Power(assembly.GetType("Yjcm.ShuiHuoJianRongPower", true)!, owner);
var powers = (List<PowerModel>)typeof(Creature).GetField("_powers", Flags)!.GetValue(owner)!;
powers.Add(reversal);
for (int i = 0; i < 2; i++)
{
    Check(Damage(weak, other, owner, move) == 1.25m, "Weak reversal must be 1.25");
    Check(Damage(vulnerable, owner, other, move) == 0.5m, "Vulnerable reversal must be 0.5");
    Check(frail.ModifyBlockMultiplicative(owner, 100, move, card, null) == 1.25m, "Frail reversal must be 1.25");
    foreach (var power in new[] { weak, vulnerable, frail })
        Check(power.Amount == 3, "Reversal changed duration/stacks");
}
Check(Damage(weak, owner, other, move) == 1m, "Weak leaked to another dealer");
Check(Damage(vulnerable, other, owner, move) == 1m, "Vulnerable leaked to another target");
Check(frail.ModifyBlockMultiplicative(other, 100, move, card, null) == 1m, "Frail leaked to another target");
Check(Damage(weak, other, owner, unpowered) == 1m, "Weak changed unpowered damage");
Check(Damage(vulnerable, owner, other, unpowered) == 1m, "Vulnerable changed unpowered damage");
Check(frail.ModifyBlockMultiplicative(owner, 100, unpowered, card, null) == 1m, "Frail changed unpowered Block");
Check(frail.ModifyBlockMultiplicative(owner, 100, move, null, null) == 0.75m, "Frail reversal must only affect card Block");
var futureWeak = Power(typeof(WeakPower), owner);
Check(Damage(futureWeak, other, owner, move) == 1.25m, "Later debuffs must also reverse");
powers.Remove(reversal);
Check(Damage(futureWeak, other, owner, move) == 0.75m, "Removing ability must restore native behavior");

var healthDamage = assembly.GetType("Yjcm.SunQianShuiHuoWuDi", true)!.GetMethod("HealthDamage", Flags)!;
foreach (var (unblocked, blocked, overkill, expected) in new[] {
    (7, 0, 0, 7), (3, 4, 0, 3), (0, 7, 0, 0), (11, 0, 8, 3), (0, 0, 0, 0) })
{
    var result = new DamageResult(other, move) {
        UnblockedDamage = unblocked, BlockedDamage = blocked, OverkillDamage = overkill };
    Check((int)healthDamage.Invoke(null, [result])! == expected, "HP damage must exclude Block and overkill");
}
await SunqianCases.Run(assembly, Path.GetDirectoryName(Path.GetFullPath(args[0]))!, Check);
FirePreviewCases.Run(assembly, Check);
await LampEnergyCases.Run(Check);
PlayerSkinCases.Run(assembly, Check);
Console.WriteLine($"COLLABORATION_HARMONY_OK runtime={Environment.Version} assertions={checks}");
