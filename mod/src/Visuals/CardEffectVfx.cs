using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Yjcm;

/// <summary>Cosmetic only: never awaits, alters card results, or consumes gameplay RNG.</summary>
internal static class CardEffectVfx
{
    private static readonly string Root = Path.Combine(
        Path.GetDirectoryName(typeof(CardEffectVfx).Assembly.Location)!, "Spine", "CardEffects");
    private static readonly Dictionary<string, string> CardFolders = new(StringComparer.Ordinal)
    {
        ["ChenShiQingBei"] = "陈式",
        ["HuangWudieShuangRui"] = "黄舞蝶", ["HuangWudieShouXing"] = "黄舞蝶", ["HuangWudieShaXue"] = "黄舞蝶",
        ["PangFengYiYiTong"] = "庞凤衣", ["PangFengYiPeiNiang"] = "庞凤衣",
        ["ShenHuangZhongZhanJue"] = "神黄忠", ["ShenHuangZhongLieCang"] = "神黄忠",
        ["ShenJiangWeiTianRen"] = "神姜维", ["ShenJiangWeiPingXiang"] = "神姜维",
        ["ShenPangTongFengLiaoYang"] = "神庞统", ["ShenPangTongFengLiaoYin"] = "神庞统",
        ["ShenZhangFeiXunShi"] = "神张飞", ["ShenZhangFeiShenCai"] = "神张飞",
        ["ShenZhaoYunLongHun"] = "神赵云", ["ShenMaChaoShouLi"] = "神马超",
        ["ShangYu"] = "许靖", ["CaiXia"] = "许靖",
        ["XingYuanShaoZongShi"] = "星袁绍", ["XingYuanShaoXiaoYan"] = "星袁绍", ["XingYuanShaoJiaoWang"] = "星袁绍",
        ["XuRongXiongHuo"] = "徐荣凶镬", ["XuRongShaJue"] = "徐荣-杀绝",
        ["MouJiaXuFuMouYang"] = "谋贾诩阳和肃身", ["MouJiaXuSuShen"] = "谋贾诩阳和肃身",
        ["MouJiaXuFuMouYin"] = "谋贾诩阴和入世", ["MouJiaXuRuShi"] = "谋贾诩阴和入世",
        ["LeCaiWenJiShuangQie"] = "乐蔡文姬", ["LeDiaoChanTanBan"] = "乐貂蝉",
        ["ZhuGeJingZuYin"] = "诸葛京"
    };
    private static readonly Dictionary<string, Dictionary<string, Asset>> Catalogs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GodotObject> DataCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Failed = new(StringComparer.Ordinal);
    private static CanvasLayer? _active;

    // Exact model mapping; option cards and cards from other mods are never matched by title.
    internal static (string Folder, string Animation, string Root)? Resolve(string typeName, CardType type)
    {
        if (typeName.StartsWith("WeiDongZhuo", StringComparison.Ordinal)) typeName = typeName[11..];
        return CardFolders.TryGetValue(typeName, out var folder)
            ? ResolveClip(folder, type, Root) : null;
    }

    internal static (string Folder, string Animation, string Root)? ResolveClip(string folder, CardType type, string root) => type switch
    {
        CardType.Attack => (folder, "gongji", root),
        CardType.Skill or CardType.Power => (folder, "jineng", root),
        _ => null
    };

    internal static float GetScale(string folder, float viewportWidth, float viewportHeight, float width, float height)
    {
        // JiaXu includes a scene plate: cover the viewport without stretching its aspect ratio.
        // Ordinary character effects retain the existing 72% framing.
        return folder is "谋贾诩阳和肃身" or "谋贾诩阴和入世" or "谋郭嘉阳" or "谋郭嘉阴"
            ? Math.Max(viewportWidth / width, viewportHeight / height)
            : Math.Min(viewportWidth * 0.72f / width, viewportHeight * 0.72f / height);
    }

    internal static void Play(CardModel card)
    {
        if (!WuZhugeliangSettings.EnableEffects) return;
        // Include auto-play and derivative cards, but not replays of the same card execution.
        if (card.CurrentPlayIndex != 0 || card.GetType().Assembly != typeof(CardEffectVfx).Assembly) return;
        var resolved = Resolve(card.GetType().Name, card.Type);
        if (resolved == null) return;
        var (folder, animation, root) = resolved.Value;
        var bundlePath = Path.Combine(root, folder);
        if (Failed.Contains(bundlePath)) return;
        CanvasLayer? layer = null;
        Node2D? sprite = null;
        try
        {
            if (NCombatRoom.Instance is not { } room || !ClassDB.ClassExists("SpineSprite")) return;
            if (!Catalogs.TryGetValue(root, out var catalog))
            {
                var index = Path.Combine(root, "index.json.txt");
                if (!File.Exists(index)) index = Path.Combine(root, "index.json"); // Legacy local asset trees.
                catalog = JsonSerializer.Deserialize<Asset[]>(File.ReadAllText(index),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.ToDictionary(a => a.Folder, StringComparer.Ordinal);
                Catalogs.Add(root, catalog);
            }
            var clip = catalog[folder].Animations.Single(a => a.Name == animation);
            var data = LoadData(bundlePath);
            var raw = Instantiate("SpineSprite");
            if (raw is not Node2D node) { raw.Dispose(); throw new InvalidCastException("SpineSprite is not Node2D"); }
            sprite = node;
            sprite.Set("skeleton_data_res", data);
            layer = new CanvasLayer { Name = "YjcmCardEffect", Layer = 31 };
            room.AddChild(layer);
            layer.AddChild(sprite);
            void Layout()
            {
                if (!GodotObject.IsInstanceValid(sprite) || sprite.IsQueuedForDeletion()) return;
                var viewport = room.GetViewport().GetVisibleRect().Size;
                var b = clip.Focus;
                float scale = GetScale(folder, viewport.X, viewport.Y, b.Width, b.Height);
                sprite.Scale = new Vector2(scale, scale);
                // Spine Y is up; Godot Canvas Y is down.
                sprite.Position = viewport * 0.5f - new Vector2(b.CenterX, -b.CenterY) * scale
                    + new Vector2(0f, viewport.Y * clip.VerticalViewportOffset);
            }
            Layout();
            var viewportNode = room.GetViewport();
            viewportNode.SizeChanged += Layout;
            var capturedLayer = layer;
            layer.TreeExiting += () =>
            {
                if (GodotObject.IsInstanceValid(viewportNode)) viewportNode.SizeChanged -= Layout;
                if (ReferenceEquals(_active, capturedLayer)) _active = null;
            };
            var state = sprite.Call("get_animation_state").AsGodotObject()
                ?? throw new InvalidOperationException("Missing Spine animation state");
            state.Call("set_animation", animation, false, 0);
            if (_active != null && GodotObject.IsInstanceValid(_active) && !_active.IsQueuedForDeletion()) _active.QueueFree();
            _active = layer;
            // Do not wait in the combat action: card choice/network pipelines stay untouched.
            room.GetTree().CreateTimer(clip.Duration + 0.05f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(capturedLayer) && !capturedLayer.IsQueuedForDeletion()) capturedLayer.QueueFree();
            };
        }
        catch (Exception ex)
        {
            Failed.Add(bundlePath); // One diagnostic per missing/invalid bundle, never a per-card log storm.
            if (layer != null && GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            else if (sprite != null && GodotObject.IsInstanceValid(sprite)) sprite.QueueFree();
            MainFile.Logger.Error($"卡牌特效播放失败（{resolved.Value.Folder}）：{ex}");
        }
    }

    internal static void HideActive()
    {
        if (_active != null && GodotObject.IsInstanceValid(_active)) _active.Visible = false;
    }

    private static GodotObject LoadData(string bundlePath)
    {
        if (DataCache.TryGetValue(bundlePath, out var cached)) return cached;
        var skeleton = Instantiate("SpineSkeletonFileResource");
        var atlas = Instantiate("SpineAtlasResource");
        skeleton.Call("load_from_file", Path.Combine(bundlePath, "chuchang2.skel"));
        atlas.Call("load_from_atlas_file", Path.Combine(bundlePath, "chuchang2.atlas"));
        var data = Instantiate("SpineSkeletonDataResource");
        data.Set("skeleton_file_res", skeleton);
        data.Set("atlas_res", atlas);
        DataCache.Add(bundlePath, data);
        return data;
    }

    private static GodotObject Instantiate(string name) => ClassDB.Instantiate(name).AsGodotObject()
        ?? throw new InvalidOperationException($"Cannot instantiate {name}");
    private sealed class Asset
    {
        public Asset() { }
        public string Folder { get; set; } = "";
        public Clip[] Animations { get; set; } = [];
    }
    private sealed class Clip
    {
        public Clip() { }
        public string Name { get; set; } = "";
        public float Duration { get; set; }
        public Bounds Focus { get; set; } = new();
        public float VerticalViewportOffset { get; set; }
    }
    private sealed class Bounds
    {
        public Bounds() { }
        public float Width { get; set; }
        public float Height { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class CardEffectVfxPatch
{
    private static void Postfix(CardPlay cardPlay) => CardEffectVfx.Play(cardPlay.Card);
}
