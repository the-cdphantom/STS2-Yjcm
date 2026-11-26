using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Yjcm;

/// <summary>在战斗画面中央独立播放连招特效，不替换角色或敌人的动画。</summary>
internal enum WeiDongZhuoComboVfxStyle
{
    JuChui,
    BaGuan,
    JiZhan,
    PoRong,
    ZhuiTao,
    XiaoBen,
#if WEI_CAO_CAO
    KuiPan
#endif
}

internal static class WeiDongZhuoComboVfx
{
    private const float SourceWidth = 6143.98f;
    // 以 1920 宽度实测，3 倍缩放会在左右各留约 220 像素空隙。
    // 保持原始横幅比例，将有效画面放大至恰好覆盖整个可见宽度。
    private const float ImpactScale = 3.9f;
    private const float Duration = 2.34f;
    private static readonly Vector2 SourceCenter = new(-1.59f, -9.295f);
    private static readonly string SpineRoot = Path.Combine(
        Path.GetDirectoryName(typeof(WeiDongZhuoComboVfx).Assembly.Location)!,
        "Spine");
    private static readonly string ComboRoot = Path.Combine(SpineRoot, "WeiDongZhuo", "Combo");
    private static CanvasLayer? _activeLayer;

    public static bool Preflight()
    {
        if (!ClassDB.ClassExists("SpineSprite")) return false;
        foreach (var style in Enum.GetValues<WeiDongZhuoComboVfxStyle>())
        {
            CanvasItem? sprite = null;
            try
            {
                sprite = CreateSprite(Vector2.Zero, 1f, style);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Error($"威董卓连招特效预检失败（{style}）：{ex}");
                return false;
            }
            finally
            {
                sprite?.Free();
            }
        }
        return true;
    }

    public static async Task Play(WeiDongZhuoComboVfxStyle style)
    {
        if (!WuZhugeliangSettings.EnableEffects) return;
        CanvasLayer? layer = null;
        try
        {
            if (!ClassDB.ClassExists("SpineSprite") || NCombatRoom.Instance is not { } room) return;
            Stop();
            Vector2 viewport = room.GetViewport().GetVisibleRect().Size;
            float scale = viewport.X / SourceWidth * ImpactScale;
            layer = new CanvasLayer { Name = "WeiDongZhuoComboOverlay", Layer = 31 };
            room.AddChild(layer);
            var sprite = CreateSprite(viewport * 0.5f - SourceCenter * scale, scale, style);
            sprite.ZIndex = 100;
            layer.AddChild(sprite);
            _activeLayer = layer;
            await layer.ToSignal(layer.GetTree().CreateTimer(Duration), SceneTreeTimer.SignalName.Timeout);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"威董卓连招特效播放失败（{style}）：{ex}");
        }
        finally
        {
            if (layer != null && GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            if (ReferenceEquals(_activeLayer, layer)) _activeLayer = null;
        }
    }

    internal static void HideActive()
    {
        if (_activeLayer != null && GodotObject.IsInstanceValid(_activeLayer)) _activeLayer.Visible = false;
    }

    private static void Stop()
    {
        if (_activeLayer != null && GodotObject.IsInstanceValid(_activeLayer)) _activeLayer.QueueFree();
        _activeLayer = null;
    }

    private static CanvasItem CreateSprite(Vector2 position, float scale, WeiDongZhuoComboVfxStyle style)
    {
        var asset = GetAsset(style);
        string root = ComboRoot;
#if WEI_CAO_CAO
        if (style == WeiDongZhuoComboVfxStyle.KuiPan) root = Path.Combine(SpineRoot, "WeiCaoCao");
#endif
        string assetRoot = Path.Combine(root, asset.Folder);
        string skeletonPath = Path.Combine(assetRoot, asset.Skeleton);
        string atlasPath = Path.Combine(assetRoot, asset.Atlas);
        if (!File.Exists(skeletonPath) || !File.Exists(atlasPath))
            throw new FileNotFoundException($"缺少威董卓连招 Spine 素材：{assetRoot}");
        var skeleton = Instantiate("SpineSkeletonFileResource");
        skeleton.Call("load_from_file", skeletonPath);
        var atlas = Instantiate("SpineAtlasResource");
        atlas.Call("load_from_atlas_file", atlasPath);
        var data = Instantiate("SpineSkeletonDataResource");
        data.Set("skeleton_file_res", skeleton);
        data.Set("atlas_res", atlas);
        var raw = Instantiate("SpineSprite");
        raw.Set("skeleton_data_res", data);
        if (raw is not Node2D sprite) throw new InvalidCastException("SpineSprite 不是 Node2D。");
        sprite.Name = $"WeiDongZhuoComboSpine_{style}";
        sprite.Position = position;
        sprite.Scale = new Vector2(scale, scale);
        var animationState = sprite.Call("get_animation_state").AsGodotObject()
            ?? throw new InvalidOperationException("连招 Spine 没有动画状态。");
        animationState.Call("set_animation", "play", false, 0);
        return sprite;
    }

    private static GodotObject Instantiate(string className) =>
        ClassDB.Instantiate(className).AsGodotObject()
        ?? throw new InvalidOperationException($"无法实例化 {className}。");

    private static (string Folder, string Skeleton, string Atlas) GetAsset(WeiDongZhuoComboVfxStyle style) => style switch
    {
        WeiDongZhuoComboVfxStyle.JuChui => ("", "combo.skel", "combo.atlas"),
        WeiDongZhuoComboVfxStyle.BaGuan => ("BaGuan", "weilvbu_jineng.skel", "weilvbu_jineng.atlas"),
        WeiDongZhuoComboVfxStyle.JiZhan => ("JiZhan", "weimachao_jineng.skel", "weimachao_jineng.atlas"),
        WeiDongZhuoComboVfxStyle.PoRong => ("PoRong", "weizhangliao_jineng.skel", "weizhangliao_jineng.atlas"),
        WeiDongZhuoComboVfxStyle.ZhuiTao => ("ZhuiTao", "weigongsunzan_jineng.skel", "weigongsunzan_jineng.atlas"),
        WeiDongZhuoComboVfxStyle.XiaoBen => ("XiaoBen", "weimateng_jineng.skel", "weimateng_jineng.atlas"),
#if WEI_CAO_CAO
        WeiDongZhuoComboVfxStyle.KuiPan => ("KuiPan", "weicaozhang_jineng.skel", "weicaozhang_jineng.atlas"),
#endif
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "未知的威董卓连招特效。")
    };
}
