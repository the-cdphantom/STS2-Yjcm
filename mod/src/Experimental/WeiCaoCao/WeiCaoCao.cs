using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

namespace Yjcm;

[RegisterCharacter]
public sealed class WeiCaoCao : ModCharacterTemplate<WeiCaoCaoCardPool, IroncladRelicPool, IroncladPotionPool>
{
    internal const string RestSiteScenePath = "res://Experimental/WeiCaoCao/rest_site.tscn";
    private const string ImageDirectory = "res://Experimental/WeiCaoCao/images/";
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override Color NameColor => new("668EB9");
    public override int StartingHp => 80;
    public override int StartingGold => 99;
    public override int BaseOrbSlotCount => 5;
    public override bool HideFromVanillaCharacterSelect => false;
    public override bool HideInCardLibraryCompendium => false;
    public override bool RequiresEpochAndTimeline => false;
    public override float CastAnimDelay => 0.25f;
    public override float AttackAnimDelay => 0.15f;
    public override string CharacterSelectSfx => WeiCaoCaoSelectionAudio.SoundId;
    public override List<string> GetArchitectAttackVfx() => ModelDb.Character<Ironclad>().GetArchitectAttackVfx();
    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(), new(Scenes: new(
            VisualsPath: "res://Experimental/WeiCaoCao/creature.tscn",
            MerchantAnimPath: "res://Experimental/WeiCaoCao/merchant.tscn",
            RestSiteAnimPath: RestSiteScenePath),
            Ui: new(
                IconTexturePath: "res://Experimental/WeiCaoCao/images/avatar.png",
                IconOutlineTexturePath: "res://Experimental/WeiCaoCao/images/avatar.png",
                IconPath: "res://Experimental/WeiCaoCao/icon.tscn",
                CharacterSelectIconPath: "res://Experimental/WeiCaoCao/images/select_icon.png",
                CharacterSelectLockedIconPath: "res://Experimental/WeiCaoCao/images/select_icon.png",
                MapMarkerPath: "res://Experimental/WeiCaoCao/images/avatar.png"),
            Multiplayer: new(
                ArmPointingTexturePath: ImageDirectory + "weicaocao_hand_point.png",
                ArmRockTexturePath: ImageDirectory + "weicaocao_hand_rock.png",
                ArmPaperTexturePath: ImageDirectory + "weicaocao_hand_paper.png",
                ArmScissorsTexturePath: ImageDirectory + "weicaocao_hand_scissors.png")));

    public override string CustomRestSiteAnimPath => RestSiteScenePath;
    public override string CustomCharacterSelectBgPath
    {
        get { SetRoot(); return "res://Experimental/WeiCaoCao/select_bg.tscn"; }
    }
    public override string CustomMerchantAnimPath
    {
        get { SetRoot(); return "res://Experimental/WeiCaoCao/merchant.tscn"; }
    }
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        SetRoot();
        var visuals = RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.Scenes!.VisualsPath!);
        if (visuals != null) visuals.SetMeta("weicaocao_asset_root", Root);
        return visuals;
    }
    private static string Root => Path.Combine(Path.GetDirectoryName(typeof(WeiCaoCao).Assembly.Location)!, "Spine", "WeiCaoCao");
    private static void SetRoot()
    {
        if (Engine.GetMainLoop() is SceneTree tree) tree.Root.SetMeta("weicaocao_asset_root", Root);
    }
    protected override IEnumerable<Type> StartingDeckTypes => [
        typeof(WeiCaoCaoSha), typeof(WeiCaoCaoSha), typeof(WeiCaoCaoSha), typeof(WeiCaoCaoSha),
        typeof(WeiCaoCaoShan), typeof(WeiCaoCaoShan), typeof(WeiCaoCaoShan), typeof(WeiCaoCaoShan),
        typeof(WeiCaoCaoHuJia), typeof(WeiCaoCaoDuoYue)];
    protected override IEnumerable<Type> StartingRelicTypes => [typeof(WeiCaoCaoJunHe)];
}

// Keep the native rest-site lookup on the same scene as the RitsuLib asset profile.
[HarmonyLib.HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.RestSiteAnimPath), HarmonyLib.MethodType.Getter)]
internal static class WeiCaoCaoRestSitePathPatch
{
    [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is WeiCaoCao)
            __result = WeiCaoCao.RestSiteScenePath;
    }
}
