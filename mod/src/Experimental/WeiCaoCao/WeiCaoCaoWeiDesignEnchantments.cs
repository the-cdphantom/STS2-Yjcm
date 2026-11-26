using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

internal static class TuoYuEnchantmentArt
{
    internal const string IconPath = "res://Experimental/WeiCaoCao/images/tuo_yu_enchantment.png";
}

[RegisterEnchantment]
public sealed class WeiCaoCaoFengTian:YjcmMusicEnchantment
{
    protected override string EnchantmentIconPath => TuoYuEnchantmentArt.IconPath;
    public override bool CanEnchantCardType(CardType type)=>type==CardType.Attack;
    public override decimal EnchantDamageMultiplicative(decimal damage,ValueProp props)=>props.HasFlag(ValueProp.Unpowered)?1:2;
}
[RegisterEnchantment]
public sealed class WeiCaoCaoQingQu:YjcmMusicEnchantment
{
    protected override string EnchantmentIconPath => TuoYuEnchantmentArt.IconPath;
    public override bool CanEnchantCardType(CardType type)=>type==CardType.Attack;
    protected override void OnEnchant(){base.OnEnchant();Card.EnergyCost.SetThisCombat(0);}
}
[RegisterEnchantment]
public sealed class WeiCaoCaoJunShan:ModEnchantmentTemplate
{
    public override EnchantmentAssetProfile AssetProfile => new(TuoYuEnchantmentArt.IconPath);
    public override bool CanEnchantCardType(CardType type)=>type==CardType.Attack;
}
[RegisterEnchantment]
public sealed class WeiCaoCaoSiShi:ModEnchantmentTemplate
{
    public override EnchantmentAssetProfile AssetProfile => new("res://Experimental/WeiCaoCao/images/si_shi_enchantment.png");
    public override bool CanEnchantCardType(CardType type)=>type is CardType.Attack or CardType.Skill or CardType.Power;
}

[HarmonyPatch]
internal static class WeiCaoCaoJunShanPierce
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(CreatureCmd).GetMethods(BindingFlags.Static|BindingFlags.Public)
        .Where(m=>m.Name==nameof(CreatureCmd.Damage) && m.GetParameters().Any(p=>p.Name=="props" && p.ParameterType==typeof(ValueProp)) &&
            m.GetParameters().Any(p=>p.Name=="cardSource" && p.ParameterType==typeof(CardModel)));
    private static void Prefix(CardModel? cardSource,ref ValueProp props)
    {if(cardSource?.Enchantment is WeiCaoCaoJunShan)props|=ValueProp.Unblockable;}
}
