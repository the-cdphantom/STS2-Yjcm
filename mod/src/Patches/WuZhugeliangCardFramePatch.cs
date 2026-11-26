using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Yjcm;

/// <summary>Use the approved v5 artwork without replacing the native card UI.</summary>
[HarmonyPatch]
internal static class WuZhugeliangCardFramePatch
{
    private const string Root = "res://Yjcm/Images/CardFrames/SevenStars/";
    private const string BadgeName = "YjcmSevenStarsShuBadge";
    private static bool _reportedMissingArt;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Reload runs after model replacement/pool reuse; UpdateVisuals also
        // refreshes the portrait and frame when hovering or previewing upgrades.
        yield return AccessTools.Method(typeof(NCard), "Reload");
        yield return AccessTools.Method(typeof(NCard), nameof(NCard.UpdateVisuals));
    }

    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() || __instance.Model == null || __instance.Body == null) return;
        var badge = __instance.Body.GetNodeOrNull<TextureRect>(BadgeName);
        var model = __instance.Model;
        // Ancient cards have their own full-art, glass and animated border.
        // Other characters keep their original appearance even when NCard is reused.
        if (model.VisualCardPool is not WuZhugeliangCardPool || model.Rarity == CardRarity.Ancient)
        {
            badge?.Hide();
            return;
        }

        var kind = model.Type switch
        {
            CardType.Attack => "attack",
            CardType.Skill => "skill",
            CardType.Power => "power",
            _ => null
        };
        if (kind == null)
        {
            badge?.Hide();
            return;
        }

        var frame = __instance.GetNodeOrNull<TextureRect>("%Frame");
        var texture = GD.Load<Texture2D>(Root + $"card_frame_{kind}_qixing_s.png");
        var badgeTexture = GD.Load<Texture2D>(Root + "shu_badge.png");
        if (frame == null || texture == null || badgeTexture == null)
        {
            badge?.Hide();
            if (!_reportedMissingArt)
            {
                _reportedMissingArt = true;
                MainFile.Logger.Warn("七星卡框素材加载失败，保留原生卡框。");
            }
            return;
        }

        // The v5 598x844 textures already contain the approved red palette.
        // Applying the pool's HSV material again would tint the gold and stars.
        frame.Texture = texture;
        frame.Material = null;
        if (badge == null)
        {
            badge = new TextureRect
            {
                Name = BadgeName,
                Texture = badgeTexture,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(96, -216),
                Size = new Vector2(56, 56)
            };
            __instance.Body.AddChild(badge);
        }
        // Godot ZIndex is shared across sibling card/overlay subtrees. Raising
        // the badge lets library cards draw over the inspection screen's dimmer.
        // Keep it in the normal card layer and order it locally after the banner.
        badge.ZIndex = 0;
        var banner = __instance.GetNodeOrNull<Control>("%TitleBanner");
        if (banner != null && badge.GetIndex() != banner.GetIndex() + 1)
            __instance.Body.MoveChild(badge, banner.GetIndex() + 1);
        badge.Show();
    }
}
