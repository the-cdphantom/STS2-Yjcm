using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

namespace Yjcm;

/// <summary>只编入威董卓测试工程；修复发布工程不引用此目录。</summary>
[RegisterCharacter]
public sealed class WeiDongZhuo : ModCharacterTemplate<WeiDongZhuoCardPool, IroncladRelicPool, IroncladPotionPool>
{
    internal const string RestSiteScenePath = "res://scenes/rest_site/characters/yjcm_character_wei_dong_zhuo_rest_site.tscn";
    public static readonly Color ThemeColor = new("B98A31");
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override Color NameColor => ThemeColor;
    public override int StartingHp => 80;
    public override int StartingGold => 99;
    public override int BaseOrbSlotCount => 5;
    public override bool HideFromVanillaCharacterSelect => false;
    public override bool HideInCardLibraryCompendium => false;
    public override bool RequiresEpochAndTimeline => false;

    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(),
        new(
            Scenes: new(
                VisualsPath: "res://WeiDongZhuo/scenes/creature.tscn",
                MerchantAnimPath: "res://WeiDongZhuo/scenes/merchant.tscn",
                RestSiteAnimPath: RestSiteScenePath
            ),
            Ui: new(
                IconTexturePath: "res://WeiDongZhuo/images/avatar.png",
                IconOutlineTexturePath: "res://WeiDongZhuo/images/avatar.png",
                IconPath: "res://WeiDongZhuo/scenes/icon.tscn",
                CharacterSelectIconPath: "res://WeiDongZhuo/images/select_bottom.png",
                CharacterSelectLockedIconPath: "res://WeiDongZhuo/images/select_bottom.png",
                MapMarkerPath: "res://WeiDongZhuo/images/avatar.png"
            ),
            Multiplayer: new(
                ArmPointingTexturePath: "res://WeiDongZhuo/images/weidongzhuo_hand_point.png",
                ArmRockTexturePath: "res://WeiDongZhuo/images/weidongzhuo_hand_rock.png",
                ArmPaperTexturePath: "res://WeiDongZhuo/images/weidongzhuo_hand_paper.png",
                ArmScissorsTexturePath: "res://WeiDongZhuo/images/weidongzhuo_hand_scissors.png"
            )));

    public override string CustomCharacterSelectBgPath
    {
        get
        {
            bool useTaiShan = SetAssetRoot();
            return useTaiShan ? "res://WeiDongZhuo/scenes/taishan_select_bg.tscn"
                : "res://WeiDongZhuo/scenes/select_bg.tscn";
        }
    }

    // AssetProfile alone does not override the live CharacterModel path for this scene.
    // Use the direct RitsuLib override so rest-site creation never falls back to Ironclad.
    public override string CustomRestSiteAnimPath => RestSiteScenePath;

    public override string CustomMerchantAnimPath
    {
        get
        {
            SetAssetRoot();
            return "res://WeiDongZhuo/scenes/merchant.tscn";
        }
    }

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        SetAssetRoot();
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.Scenes!.VisualsPath!);
    }

    internal static string SkinAssetFolder(bool useTaiShan) =>
        useTaiShan ? "WeiDongZhuoTaiShan" : "WeiDongZhuo";

    internal static void ConfigureVisualSkin(Node visuals, bool useTaiShan)
    {
        string spineRoot = Path.Combine(Path.GetDirectoryName(typeof(WeiDongZhuo).Assembly.Location)!, "Spine");
        if (useTaiShan && !File.Exists(Path.Combine(spineRoot, SkinAssetFolder(true), "Combat", "character.skel")))
            useTaiShan = false;
        visuals.SetMeta("weidongzhuo_asset_root", Path.Combine(spineRoot, SkinAssetFolder(useTaiShan)));
        visuals.SetMeta("weidongzhuo_taishan_skin", useTaiShan);
    }

    private static bool SetAssetRoot()
    {
        bool useTaiShan = WuZhugeliangSettings.UseTaiShanBengShiSkin;
        string root = Path.Combine(Path.GetDirectoryName(typeof(WeiDongZhuo).Assembly.Location)!,
            "Spine", SkinAssetFolder(useTaiShan));
        if (useTaiShan && !File.Exists(Path.Combine(root, "Combat", "character.skel")))
        {
            MainFile.Logger.Warn("泰山崩石皮肤素材缺失，回退威董卓默认皮肤。");
            useTaiShan = false;
            root = Path.Combine(Path.GetDirectoryName(typeof(WeiDongZhuo).Assembly.Location)!,
                "Spine", SkinAssetFolder(false));
        }
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.Root.SetMeta("weidongzhuo_asset_root", root);
            tree.Root.SetMeta("weidongzhuo_taishan_skin", useTaiShan);
        }
        return useTaiShan;
    }

    // 4杀、4闪、据陲、豪首。
    protected override IEnumerable<Type> StartingDeckTypes => new[]
    {
        typeof(WeiDongZhuoSha), typeof(WeiDongZhuoSha), typeof(WeiDongZhuoSha), typeof(WeiDongZhuoSha),
        typeof(WeiDongZhuoShan), typeof(WeiDongZhuoShan), typeof(WeiDongZhuoShan), typeof(WeiDongZhuoShan),
        typeof(WeiDongZhuoJuChui), typeof(WeiDongZhuoHaoShou)
    };
    protected override IEnumerable<Type> StartingRelicTypes => new[] { typeof(WeiDongZhuoGuangYong) };
    public override float CastAnimDelay => 0.25f;
    public override float AttackAnimDelay => 0.15f;

    private static string? _characterSelectVoicePath;
    private static string CharacterSelectVoicePath => _characterSelectVoicePath ??= Path.Combine(
        Path.GetDirectoryName(typeof(WeiDongZhuo).Assembly.Location)!,
        "Voice", "WeiDongZhuo", "character_select.wav");

    public override string CharacterSelectSfx
    {
        get
        {
            VoicePlayer.PlayWav(CharacterSelectVoicePath);
            return ModelDb.Character<Ironclad>().CharacterSelectSfx;
        }
    }

    public override List<string> GetArchitectAttackVfx() => ModelDb.Character<Ironclad>().GetArchitectAttackVfx();
}

/// <summary>
/// Hard fallback for the rest-site model. The conventional scene path also exists in the PCK,
/// so this remains valid even if another asset-override patch runs before or after RitsuLib.
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.RestSiteAnimPath), HarmonyLib.MethodType.Getter)]
internal static class WeiDongZhuoRestSitePathPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is WeiDongZhuo)
            __result = WeiDongZhuo.RestSiteScenePath;
    }
}
