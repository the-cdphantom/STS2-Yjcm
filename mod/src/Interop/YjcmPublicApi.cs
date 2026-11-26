using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

#nullable enable

namespace Yjcm.Api;

/// <summary>Optional gameplay API; public signatures contain no Yjcm-specific types.
/// Mutation methods must be awaited inside the caller's existing synchronized combat action.</summary>
public static class YjcmPublicApi
{
    public static bool IsReady => PublicApi.IsReady;
    public static int ApiVersion => 1;
    public static string[] GetCapabilities() => ["fire_attack", "equipment_slots"];

    public static bool IsYjcmCharacter(CharacterModel? character) => character is WuZhugeliang
#if WEI_DONG_ZHUO
        or WeiDongZhuo
#endif
#if WEI_CAO_CAO
        or WeiCaoCao
#endif
        ;
    public static bool IsHuoGongPower(PowerModel? power) => power is HuoGongPower;
    public static PowerModel? GetHuoGongPower(Creature? creature) => creature?.Powers.OfType<HuoGongPower>().FirstOrDefault();
    public static int GetHuoGongAmount(Creature? creature) => GetHuoGongPower(creature)?.Amount ?? 0;
    public static IHoverTip CreateHuoGongHoverTip() => HoverTipFactory.FromPower<HuoGongPower>();

    public static async Task<PowerModel?> ApplyHuoGong(
        PlayerChoiceContext choiceContext, Creature target, decimal amount,
        Creature? applier, CardModel? cardSource, bool silent = false)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(target);
        if (!IsReady) return null;
        return await PowerCmd.Apply<HuoGongPower>(choiceContext, target, amount, applier, cardSource, silent);
    }

    // Stable slot numbers: 0 weapon, 1 armor, 2 defensive horse, 3 offensive horse, 4 treasure.
    public static int[] GetEquipmentSlotTypes() => [0, 1, 2, 3, 4];
    public static bool IsEquipmentOrb(OrbModel? orb) => orb is EquipmentOrbModel;
    public static int GetEquipmentSlotType(OrbModel? orb) => orb is EquipmentOrbModel equipment ? (int)equipment.SlotType : -1;
    public static bool IsEmptyEquipmentSlot(OrbModel? orb) => orb is EmptyWeaponSlotOrb or EmptyArmorSlotOrb
        or EmptyDefensiveHorseSlotOrb or EmptyOffensiveHorseSlotOrb or EmptyTreasureSlotOrb;

    public static bool HasEquipmentBar(Player? player) => player?.PlayerCombatState?.OrbQueue.Orbs
        .Any(orb => orb is EquipmentOrbModel) == true;

    public static OrbModel? GetEquipmentInSlot(Player? player, int slotType)
    {
        ValidateSlot(slotType);
        if (!HasEquipmentBar(player)) return null;
        return player!.PlayerCombatState.OrbQueue.Orbs.FirstOrDefault(orb =>
            orb is EquipmentOrbModel equipment ? (int)equipment.SlotType == slotType : slotType == 4);
    }

    public static bool IsEquipmentSlotAbolished(Player? player, int slotType) =>
        GetEquipmentInSlot(player, slotType) is AbolishSlotOrb;
    public static int CountAbolishedEquipmentSlots(Player? player) => HasEquipmentBar(player)
        ? EquipCmd.CountAbolishedSlots(player!) : 0;
    public static int[] GetAvailableEquipmentSlots(Player? player) => GetEquipmentSlotTypes()
        .Where(slot => GetEquipmentInSlot(player, slot) is { } orb && orb is not AbolishSlotOrb).ToArray();

    /// <summary>Returns independent mutable instances. Empty/abolished placeholders are excluded.</summary>
    public static OrbModel[] GetEquipmentCatalog() => ModelDb.Orbs.OfType<EquipmentOrbModel>()
        .Where(orb => !IsEmptyEquipmentSlot(orb) && orb is not AbolishSlotOrb)
        .OrderBy(orb => orb.Id.ToString(), StringComparer.Ordinal).Select(orb => orb.ToMutable()).ToArray();

    public static async Task<bool> ChannelEquipment(
        PlayerChoiceContext choiceContext, Player player, OrbModel orb, bool playSfx = true)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(orb);
        if (!IsReady || orb is not EquipmentOrbModel equipment || orb is AbolishSlotOrb) return false;
        int slot = (int)equipment.SlotType;
        if (GetEquipmentInSlot(player, slot) is not { } current || current is AbolishSlotOrb) return false;
        // Never transfer the caller's live orb or share a mutable model between players.
        var instance = (EquipmentOrbModel)(orb.IsCanonical ? orb.ToMutable() : orb.MutableClone());
        await EquipCmd.ChannelEquipment(choiceContext, player, instance, playSfx);
        return ReferenceEquals(GetEquipmentInSlot(player, slot), instance);
    }

    public static async Task<bool> AbolishEquipmentSlot(PlayerChoiceContext choiceContext, Player player, int slotType)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(player);
        if (!IsReady || GetEquipmentInSlot(player, slotType) is not { } current || current is AbolishSlotOrb) return false;
        await EquipCmd.AbolishSlot(choiceContext, player, (EquipmentSlotType)slotType);
        return IsEquipmentSlotAbolished(player, slotType);
    }

    public static async Task<bool> RestoreEquipmentSlot(PlayerChoiceContext choiceContext, Player player, int slotType)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(player);
        if (!IsReady || !IsEquipmentSlotAbolished(player, slotType)) return false;
        await EquipCmd.RestoreSlot(choiceContext, player, (EquipmentSlotType)slotType);
        return !IsEquipmentSlotAbolished(player, slotType);
    }

    public static async Task<bool> ClearEquipmentSlot(PlayerChoiceContext choiceContext, Player player, int slotType)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(player);
        if (!IsReady || GetEquipmentInSlot(player, slotType) is not { } current || current is AbolishSlotOrb || IsEmptyEquipmentSlot(current)) return false;
        await EquipCmd.ClearSlot(choiceContext, player, (EquipmentSlotType)slotType);
        return IsEmptyEquipmentSlot(GetEquipmentInSlot(player, slotType));
    }

    private static void ValidateSlot(int slotType)
    {
        if (slotType is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(slotType));
    }
}
