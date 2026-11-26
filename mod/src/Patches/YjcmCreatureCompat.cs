using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Yjcm;

internal static class YjcmCreatureCompat
{
    internal static Task LoseMaxHp(PlayerChoiceContext context, Creature creature, decimal amount, CardModel? source)
    {
#if GAME_V0113
        return CreatureCmd.LoseMaxHp(context, creature, amount, source);
#else
        return CreatureCmd.LoseMaxHp(context, creature, amount, isFromCard: source != null);
#endif
    }
}
