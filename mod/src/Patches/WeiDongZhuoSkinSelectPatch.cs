using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

#nullable enable

namespace Yjcm;

#if GAME_V0113
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter), [typeof(NCharacterSelectButton)])]
#else
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter), [typeof(NCharacterSelectButton), typeof(CharacterModel)])]
#endif
internal static class WeiDongZhuoSkinSelectPatch
{
    private const string SelectorName = "WeiDongZhuoSkinSelector";
    private static readonly FieldInfo BackgroundField = AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer")
        ?? throw new MissingFieldException(typeof(NCharacterSelectScreen).FullName, "_bgContainer");

    internal static bool IsWeiDongZhuo(Type? type) => type == typeof(WeiDongZhuo);

#if GAME_V0113
    private static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton __0)
    {
        CharacterModel? characterModel = __0.IsRandom || __0.IsLocked ? null : __0.Character;
#else
    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel __1)
    {
        CharacterModel? characterModel = __1;
#endif
        PanelContainer? panel = __instance.GetNodeOrNull<PanelContainer>(SelectorName);
        if (!IsWeiDongZhuo(characterModel?.GetType()))
        {
            if (panel != null) panel.Visible = false;
            return;
        }
        panel ??= CreateSelector(__instance);
        panel.Visible = true;
        UpdateText(panel);
        SkinSelectDescription.Refresh(__instance, characterModel!, WuZhugeliangSettings.UseTaiShanBengShiSkin,
            "YJCM_CHARACTER_WEI_DONG_ZHUO.taishan_description");
    }

    private static void UpdateText(PanelContainer panel) =>
        panel.GetNode<Label>("Controls/Name").Text = WuZhugeliangSettings.UseTaiShanBengShiSkin
            ? "皮肤：泰山崩石" : "皮肤：威董卓";

    private static PanelContainer CreateSelector(NCharacterSelectScreen screen)
    {
        var panel = new PanelContainer
        {
            Name = SelectorName, ZIndex = 200, MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -475, OffsetRight = -28,
            OffsetTop = 92, OffsetBottom = 162
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.075f, 0.055f, 0.025f, 0.94f),
            BorderColor = WeiDongZhuo.ThemeColor,
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 7
        });
        var row = new HBoxContainer { Name = "Controls", Alignment = BoxContainer.AlignmentMode.Center };
        var previous = Arrow("◀");
        var next = Arrow("▶");
        var label = new Label
        {
            Name = "Name", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(300, 45)
        };
        label.AddThemeFontSizeOverride("font_size", 26);
        row.AddChild(previous); row.AddChild(label); row.AddChild(next);
        panel.AddChild(row); screen.AddChild(panel);
        void Toggle()
        {
            bool oldValue = WuZhugeliangSettings.UseTaiShanBengShiSkin;
            WuZhugeliangSettings.SetWeiDongZhuoSkin(!oldValue);
            if (!ReplacePreview(screen)) WuZhugeliangSettings.SetWeiDongZhuoSkin(oldValue);
            SkinSelectDescription.Refresh(screen, ModelDb.Character<WeiDongZhuo>(),
                WuZhugeliangSettings.UseTaiShanBengShiSkin, "YJCM_CHARACTER_WEI_DONG_ZHUO.taishan_description");
            PlayerSkins.PublishLocalChange();
            UpdateText(panel);
        }
        previous.Pressed += Toggle; next.Pressed += Toggle;
        return panel;
    }

    private static Button Arrow(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(62, 45), FocusMode = Control.FocusModeEnum.All };
        button.AddThemeFontSizeOverride("font_size", 24);
        return button;
    }

    private static bool ReplacePreview(NCharacterSelectScreen screen)
    {
        Control? preview = null;
        try
        {
            var container = (Control?)BackgroundField.GetValue(screen)
                ?? throw new InvalidOperationException("人物选择背景尚未初始化。");
            string path = ModelDb.Character<WeiDongZhuo>().CharacterSelectBg;
            preview = ResourceLoader.Load<PackedScene>(path).Instantiate<Control>();
            // Construct the replacement before removing the working background.
            foreach (Node child in container.GetChildren())
            {
                container.RemoveChild(child); child.QueueFree();
            }
            container.AddChild(preview);
            return true;
        }
        catch (Exception error)
        {
            if (preview != null && preview.GetParent() == null) preview.QueueFree();
            MainFile.Logger.Error($"威董卓皮肤预览切换失败：{error.Message}");
            return false;
        }
    }
}

internal static class SkinSelectDescription
{
    internal static void Refresh(NCharacterSelectScreen screen, CharacterModel character, bool useSkin, string skinKey)
    {
        if (screen.GetNodeOrNull<MegaRichTextLabel>("InfoPanel/VBoxContainer/DescriptionLabel") is { } label)
            label.Text = new LocString("characters", useSkin ? skinKey : character.CharacterSelectDesc).GetFormattedText();
    }
}
