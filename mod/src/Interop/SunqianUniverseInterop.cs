using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop;

#nullable enable

namespace Yjcm.Interop;

// Verified against SunqianUniversePublicApi source blob 69d6321b74fcdd43ebb8ef6952fd1e5d4d0aaa25.
// Simple non-throwing stubs are required by RitsuLib's interop rewriting.
[AssemblyInterop("Squ.Api.SunqianUniversePublicApi, sunqian-universe")]
public static class SunqianUniverseInterop
{
    public static bool IsReady => false;
    public static bool IsSupportingActorCharacter(CharacterModel? character) => false;
    public static bool HasSupportingActorInCombat() => false;
    public static bool IsBurningPower(PowerModel? power) => false;
    public static IHoverTip CreateBurningHoverTip() => null!;
    public static Task<PowerModel?> ApplyBurning(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) => Task.FromResult<PowerModel?>(null);
    public static Task<bool> TriggerBurning(PlayerChoiceContext choiceContext, Creature target,
        CardModel? cardSource = null) => Task.FromResult(false);
    public static bool IsScriptPower(PowerModel? power) => false;
    public static PowerModel? GetActiveScript(Creature? creature) => null;
    public static bool HasActiveScript(Creature? creature) => false;
    public static Task ApplyScriptPower(PlayerChoiceContext choiceContext, PowerModel scriptPower,
        Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) => Task.CompletedTask;
    public static Task InvalidateScripts(Creature creature) => Task.CompletedTask;
    public static Task RemoveScript(PowerModel power, bool notifyLift = true) => Task.CompletedTask;
    public static int GetScriptLiftsThisTurn(Player? player) => 0;
    public static bool IsCharacterMechanicBound(CardModel? card) => false;
    public static bool GrantSlightRevision(CardModel card, CardModel target, bool targetUpgraded = false) => false;
    public static bool HasSlightRevision(CardModel? card) => false;
    public static bool RequestSlightRevision(Player player, CardModel card) => false;
}
