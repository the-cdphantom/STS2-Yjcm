using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>
/// 贯石斧（武器）：伤害被完全格挡后，可弃置2张牌造成等量不可格挡的伤害。
/// 装备球占据武器栏，由 <see cref="EquipCmd.ChannelEquipment"/> 同类替换。
/// </summary>
[RegisterOrb]
public sealed class GuanShiFuOrb : EquipmentOrbModel
{
    public override EquipmentSlotType SlotType => EquipmentSlotType.Weapon;

    public override Color DarkenedColor => new("B36A00");

    private const string IconRes = "res://Yjcm/Images/Powers/guan_shi_fu.png";

    public override OrbAssetProfile AssetProfile => new(IconPath: IconRes, VisualsScenePath: null);

    protected override Node2D? TryCreateOrbSprite() => EquipmentOrbSprite.CreateNormalized(IconRes);

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(choiceContext, dealer, result, props, target, cardSource);
        if (dealer != Owner.Creature) return;
        // 伤害被完全格挡
        if (result.BlockedDamage <= 0 || result.TotalDamage != result.BlockedDamage) return;

        var blockedAmount = result.BlockedDamage;
        if (blockedAmount <= 0) return;

        try
        {
            // 选择弃置2张牌
            var selected = await CardSelectCmd.FromHandForDiscard(
                choiceContext, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 2, 2),
                null, cardSource);

            if (!selected.Any() || selected.Count() < 2) return;

            foreach (var card in selected)
                await CardCmd.Discard(choiceContext, card);

            await CreatureCmd.Damage(choiceContext, target, blockedAmount,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        }
        catch (NotImplementedException)
        {
            // 非交互上下文（如遗物触发），跳过选牌
        }
    }
}
