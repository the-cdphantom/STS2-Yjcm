using MegaCrit.Sts2.Core.Entities.Cards;
using Yjcm.Interop;

namespace Yjcm;

/// <summary>
/// Adds Sunqian Universe's optional "character mechanic bound" marker without
/// introducing a hard dependency when that mod is absent.
/// </summary>
internal static class SunqianMechanicBoundTag
{
    internal static IEnumerable<CardTag> With(IEnumerable<CardTag> original) =>
        SunqianUniverseInterop.IsReady
            ? original.Append(SunqianUniverseInterop.CharacterMechanicBoundCardTag).Distinct()
            : original;
}
