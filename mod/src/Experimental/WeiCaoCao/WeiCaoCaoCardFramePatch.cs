using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Yjcm;

[HarmonyPatch]
internal static class WeiCaoCaoCardFramePatch
{
    private const string ArtPath = "res://Experimental/WeiCaoCao/card_frames/";
    private const string BadgeName = "YjcmCaoCaoWeiBadge";

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCard), "Reload");
        yield return AccessTools.Method(typeof(NCard), nameof(NCard.UpdateVisuals));
    }

    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() || __instance.Model == null || __instance.Body == null) return;

        var body = __instance.Body;
        var badge = body.GetNodeOrNull<TextureRect>(BadgeName);
        var model = __instance.Model;
        if (model.VisualCardPool is not WeiCaoCaoCardPool || model.Rarity == CardRarity.Ancient)
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
        var frameTexture = GD.Load<Texture2D>(ArtPath + $"card_frame_{kind}_weicaocao_s.png");
        var badgeTexture = GD.Load<Texture2D>(ArtPath + "wei_badge.png");
        if (frame == null || frameTexture == null || badgeTexture == null)
        {
            badge?.Hide();
            return;
        }

        // The finished texture already contains the steel and silver colors.
        frame.Texture = frameTexture;
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
            body.AddChild(badge);
        }

        // Keep the badge on the card's own layer so inspection dimming covers it.
        var banner = __instance.GetNodeOrNull<Control>("%TitleBanner");
        if (banner != null && badge.GetIndex() != banner.GetIndex() + 1)
            body.MoveChild(badge, banner.GetIndex() + 1);
        badge.Show();
    }
}
