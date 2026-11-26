using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Yjcm;

internal static class WeiCaoCaoTurnNotice
{
    internal static void Show(PowerModel power)
    {
        // Presentation only: no turn, choice or combat RNG changes.
        var container = power.Owner.GetVfxContainer();
        if (container == null) return;
        var text = new LocString("powers", power.Id.Entry + ".extra_turn_notice").GetFormattedText();
        container.AddChildSafely(NThoughtBubbleVfx.Create(text, power.Owner, 2.0));
    }
}
