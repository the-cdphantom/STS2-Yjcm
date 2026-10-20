using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Yjcm.Interop;

#nullable enable

namespace Yjcm;

/// <summary>
/// 朱雀羽扇（武器）：每次攻击使敌人获得2层火攻。龙套演员改为给予2层灼烧。
/// 装备球占据武器栏，由 <see cref="EquipCmd.ChannelEquipment"/> 同类替换。
/// 武诸葛与威董卓的羽扇牌都装备这颗球。
/// </summary>
[RegisterOrb]
public sealed class ZhuQueYuShanOrb : EquipmentOrbModel
{
    public override EquipmentSlotType SlotType => EquipmentSlotType.Weapon;

    public override Color DarkenedColor => new("B36A00");

    private const string IconRes = "res://Yjcm/Images/Powers/zhu_que_yu_shan.png";

    public override OrbAssetProfile AssetProfile => new(IconPath: IconRes, VisualsScenePath: null);

    protected override Node2D? TryCreateOrbSprite() => EquipmentOrbSprite.CreateNormalized(IconRes);

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        ZhuQueYuShanInterop.EffectHoverTips(this);

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(choiceContext, dealer, result, props, target, cardSource);
        if (dealer != Owner.Creature) return;
        if (!props.IsPoweredAttack()) return;

        await ZhuQueYuShanInterop.ApplyEffect(
            choiceContext, target, 2, Owner.Creature, cardSource, ZhuQueYuShanInterop.Affects(Owner));
    }
}

/// <summary>
/// 装备球描述不是可重写属性。仅在读取朱雀羽扇说明时补上火攻/灼烧占位符。
/// </summary>
[HarmonyPatch(typeof(OrbModel), "get_Description")]
internal static class ZhuQueYuShanOrbDescriptionPatch
{
    public static void Postfix(OrbModel __instance, ref LocString __result)
    {
        if (__instance is not ZhuQueYuShanOrb orb) return;
        ZhuQueYuShanInterop.AddEffectText(__result, orb);
    }
}
