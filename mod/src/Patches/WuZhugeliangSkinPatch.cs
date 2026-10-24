using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

#nullable enable

namespace Yjcm;

/// <summary>从发布包的松散资源创建谋诸葛亮大厅与纯人物战斗 Spine。</summary>
internal static class WuZhugeliangSkinVisuals
{
    private static readonly string AssetRoot = Path.Combine(
        Path.GetDirectoryName(typeof(WuZhugeliangSkinVisuals).Assembly.Location)!,
        "Spine", "MouZhugeliang");

    // 分层包围盒：不能再用“人物特效 + 背景粒子”的总包围盒缩放，否则火焰粒子
    // 会把真正的大厅背景压缩到屏幕中间，四周出现空黑区域。
    private static readonly Rect2 CharacterPreviewBounds = new(-1557.0503f, -654.5573f, 3740.0413f, 1533.3053f);
    private static readonly Rect2 BackgroundPreviewBounds = new(-1225.0508f, -1284.6689f, 2523.2845f, 1853.6267f);
    private static readonly Vector2 CharacterPreviewOffset = new(260f, 125f);

    public static Control CreateCharacterSelectPreview()
    {
        var root = new Control
        {
            Name = "MouZhugeliangSelectPreview",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        Node2D background = CreateSprite(
            "Background", "zhujiemian_mouzhugeliang2", "BackgroundSpine", Vector2.Zero, 1f, "play");
        Node2D character = CreateSprite(
            "Character", "zhujiemian_mouzhugeliang1", "CharacterSpine", Vector2.Zero, 1f, "play");
        var backdrop = new ColorRect
        {
            Name = "BackdropFill",
            Color = new Color(0.015f, 0.02f, 0.03f, 1f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);
        // 保持预览根节点在原来的 AnimatedBg 层级；人物不能使用高于根节点的 ZIndex，
        // 否则会越过选人页的 InfoPanel 和底部按钮。相同 ZIndex 下按节点顺序绘制，
        // character 仍然会显示在 background 上方，而界面兄弟节点会覆盖它。
        background.ZIndex = 0;
        character.ZIndex = 0;
        root.AddChild(background);
        root.AddChild(character);

        void Layout()
        {
            Vector2 size = root.Size;
            if (size.X <= 1f || size.Y <= 1f)
                size = root.GetViewportRect().Size;
            Vector2 viewportCenter = size * 0.5f;

            // 背景单独按 cover 缩放，保证整个选人页没有未填充区域。
            // 参考原三国杀大厅构图：以底部火焰贴齐屏幕底边为基准放大背景，
            // 而不是按中心摆放，避免顶部出现大块空黑区域。
            float backgroundScale = MathF.Max(
                size.X / BackgroundPreviewBounds.Size.X,
                size.Y / BackgroundPreviewBounds.Size.Y) * 1.16f;
            background.Scale = Vector2.One * backgroundScale;
            background.Position = new Vector2(
                viewportCenter.X - BackgroundPreviewBounds.GetCenter().X * backgroundScale,
                size.Y - BackgroundPreviewBounds.End.Y * backgroundScale);

            // 人物独立于背景放大，仍交给选人页自身的 UI 覆盖。
            float characterScale = MathF.Min(
                size.X / CharacterPreviewBounds.Size.X,
                size.Y / CharacterPreviewBounds.Size.Y) * 1.38f;
            character.Scale = Vector2.One * characterScale;
            character.Position = viewportCenter - CharacterPreviewBounds.GetCenter() * characterScale + CharacterPreviewOffset;
        }

        root.Resized += Layout;
        Callable.From(Layout).CallDeferred();
        return root;
    }

    public static Node2D CreateCombatCharacter() => CreateSprite(
        "Combat", "character", "MouZhugeliangCombatSpine", new Vector2(45f, -20f), 0.68f, "play");

    private static Node2D CreateSprite(
        string folder,
        string fileName,
        string nodeName,
        Vector2 position,
        float scale,
        string animation)
    {
        string folderPath = Path.Combine(AssetRoot, folder);
        string skeletonPath = Path.Combine(folderPath, fileName + ".skel");
        string atlasPath = Path.Combine(folderPath, fileName + ".atlas");
        if (!File.Exists(skeletonPath) || !File.Exists(atlasPath))
            throw new FileNotFoundException($"缺少谋诸葛亮 Spine 素材：{folderPath}");

        GodotObject skeletonFile = Instantiate("SpineSkeletonFileResource");
        skeletonFile.Call("load_from_file", skeletonPath);
        GodotObject atlas = Instantiate("SpineAtlasResource");
        atlas.Call("load_from_atlas_file", atlasPath);
        GodotObject data = Instantiate("SpineSkeletonDataResource");
        data.Set("skeleton_file_res", skeletonFile);
        data.Set("atlas_res", atlas);

        GodotObject rawSprite = Instantiate("SpineSprite");
        rawSprite.Set("skeleton_data_res", data);
        if (rawSprite is not Node2D sprite)
            throw new InvalidCastException("SpineSprite 不是 Node2D。");

        sprite.Name = nodeName;
        sprite.Position = position;
        sprite.Scale = Vector2.One * scale;
        GodotObject? state = sprite.Call("get_animation_state").AsGodotObject();
        if (state == null)
            throw new InvalidOperationException($"{nodeName} 没有 SpineAnimationState。");
        state.Call("set_animation", animation, true, 0);
        return sprite;
    }

    private static GodotObject Instantiate(string className) =>
        ClassDB.Instantiate(className).AsGodotObject()
        ?? throw new InvalidOperationException($"无法实例化 {className}。");
}

/// <summary>在人物选择页显示可操作的皮肤切换栏，并即时替换预览。</summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class WuZhugeliangSkinSelectPatch
{
    private const string SelectorName = "WuZhugeliangSkinSelector";
    private static readonly FieldInfo BgContainerField = AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer")
        ?? throw new MissingFieldException(typeof(NCharacterSelectScreen).FullName, "_bgContainer");

    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        bool isWuZhugeliang = characterModel is WuZhugeliang;
        PanelContainer? selector = __instance.GetNodeOrNull<PanelContainer>(SelectorName);
        if (!isWuZhugeliang)
        {
            if (selector != null) selector.Visible = false;
            return;
        }

        selector ??= CreateSelector(__instance);
        selector.Visible = true;
        UpdateSelectorText(selector);
#if GAME_BETA
        if (WuZhugeliangSettings.UseMouZhugeliangSkin) ReplacePreview(__instance, true);
#else
        if (WuZhugeliangSettings.UseMouZhugeliangSkin)
            ReplacePreview(__instance, useMouSkin: true);
#endif
    }

    private static PanelContainer CreateSelector(NCharacterSelectScreen screen)
    {
        var panel = new PanelContainer
        {
            Name = SelectorName,
            ZIndex = 200,
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -475f,
            OffsetRight = -28f,
            OffsetTop = 92f,
            OffsetBottom = 162f
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.045f, 0.055f, 0.075f, 0.92f),
            BorderColor = new Color(0.55f, 0.72f, 0.92f, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10f,
            ContentMarginRight = 10f,
            ContentMarginTop = 7f,
            ContentMarginBottom = 7f
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var row = new HBoxContainer
        {
            Name = "SkinControls",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        var previous = CreateArrowButton("Previous", "◀");
        var label = new Label
        {
            Name = "SkinName",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(350f, 45f)
        };
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", new Color(0.93f, 0.96f, 1f));
        var next = CreateArrowButton("Next", "▶");
        row.AddChild(previous);
        row.AddChild(label);
        row.AddChild(next);
        panel.AddChild(row);
        screen.AddChild(panel);

        void ToggleSkin()
        {
            bool useMouSkin = !WuZhugeliangSettings.UseMouZhugeliangSkin;
            WuZhugeliangSettings.SetWuZhugeliangSkin(useMouSkin);
            UpdateSelectorText(panel);
            ReplacePreview(screen, useMouSkin);
            PlayerSkins.PublishLocalChange();
            WuZhugeliang.PlayCurrentSkinSelectVoice();
            MainFile.Logger.Info($"武诸葛亮人物选择皮肤已切换为：{(useMouSkin ? "谋诸葛亮" : "武诸葛亮")}");
        }

        previous.Pressed += ToggleSkin;
        next.Pressed += ToggleSkin;
        return panel;
    }

    private static Button CreateArrowButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(62f, 45f),
            FocusMode = Control.FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        return button;
    }

#if GAME_BETA
    private static void UpdateSelectorText(PanelContainer panel) => panel.GetNode<Label>("SkinControls/SkinName").Text = WuZhugeliangSettings.UseMouZhugeliangSkin ? "皮肤：谋诸葛亮" : "皮肤：武诸葛亮";
#else
    private static void UpdateSelectorText(PanelContainer panel)
    {
        Label label = panel.GetNode<Label>("SkinControls/SkinName");
        label.Text = WuZhugeliangSettings.UseMouZhugeliangSkin
            ? "皮肤：谋诸葛亮"
            : "皮肤：武诸葛亮";
    }
#endif

    private static void ReplacePreview(NCharacterSelectScreen screen, bool useMouSkin)
    {
        try
        {
            var backgroundContainer = (Control?)BgContainerField.GetValue(screen)
                ?? throw new InvalidOperationException("人物选择背景容器尚未初始化。");
            foreach (Node child in backgroundContainer.GetChildren())
            {
                backgroundContainer.RemoveChild(child);
                child.QueueFree();
            }

            Control preview;
#if GAME_BETA
            if (useMouSkin) preview = WuZhugeliangSkinVisuals.CreateCharacterSelectPreview();
#else
            if (useMouSkin)
            {
                preview = WuZhugeliangSkinVisuals.CreateCharacterSelectPreview();
            }
#endif
            else
            {
                CharacterModel model = ModelDb.Character<WuZhugeliang>();
                preview = PreloadManager.Cache.GetScene(model.CharacterSelectBg)
                    .Instantiate<Control>(PackedScene.GenEditState.Disabled);
                preview.Name = model.Id.Entry + "_bg";
            }
            backgroundContainer.AddChild(preview);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"武诸葛亮皮肤预览切换失败：{ex}");
        }
    }
}
