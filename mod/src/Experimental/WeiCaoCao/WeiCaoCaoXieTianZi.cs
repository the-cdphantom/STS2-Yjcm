using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool), FullPublicEntry = "YJCM_CARD_WEI_CAO_CAO_XIE_TIAN_ZI")]
public sealed class WeiCaoCaoXieTianZi() : WeiCaoCaoCard(3, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/xie_tian_zi.png";
    public override bool CanBeGeneratedInCombat => false;
    public override bool GainsBlock => true;
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var enemies = CombatState.HittableEnemies.ToArray();
        var attacks = new List<(Creature Enemy, decimal Damage, int Hits)>();
        var buffs = new List<WeiCaoCaoMonsterIntentSupport.Buff>();
        decimal block = 0;
        // Take a single deterministic snapshot before any redirected hits change the battle.
        foreach (var enemy in enemies)
        {
            if (enemy.Monster?.NextMove is not { } move) continue;
            if (move.Intents.Any(intent => intent is DefendIntent or BuffIntent))
                block += WeiCaoCaoMonsterIntentSupport.Read(enemy.Monster, move, enemies, buffs);

            foreach (var intent in move.Intents.OfType<AttackIntent>())
            {
                if (intent.DamageCalc is not { } calculate) continue;
                // AttackIntent.GetSingleDamage takes the local player's stats. Use the card owner
                // explicitly so all clients calculate the same intent damage in multiplayer.
                decimal damage = Hook.ModifyDamage(Owner.RunState, CombatState, Owner.Creature, enemy,
                    calculate(), ValueProp.Move, null,
#if GAME_BETA
                    null,
#endif
                    ModifyDamageHookType.All, CardPreviewMode.None, out _);
                if (damage >= 1) attacks.Add((enemy, decimal.Truncate(damage), Math.Max(1, intent.Repeats)));
            }
        }

        foreach (var (enemy, damage, hits) in attacks)
        {
            // Earlier hits can kill some of the targets. Each original attack can hit the
            // remaining members of the original group, never newly summoned monsters.
            for (int hit = 0; hit < hits && !CombatManager.Instance.IsOverOrEnding && Owner.Creature.IsAlive; hit++)
            {
                var targets = enemies.Where(target => target != enemy && target.IsAlive && target.IsHittable).ToArray();
                if (targets.Length == 0) break;
                // Damage rejects dead dealers. A snapshotted attack still resolves when
                // another enemy defeated its source earlier in this same card effect.
                var dealer = enemy.IsAlive ? enemy : null;
#if GAME_V0113
                await CreatureCmd.Damage(context, targets, damage, ValueProp.Unpowered, dealer, this, play, ModifyDamageHookType.Cap);
#else
                await CreatureCmd.Damage(context, targets, damage, ValueProp.Unpowered, dealer, this, play);
#endif
            }
        }

        if (!WeiCaoCaoDesignRules.Alive(this)) return;
        if (block > 0) await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, play);
        foreach (var buff in buffs)
        {
            if (!WeiCaoCaoDesignRules.Alive(this)) break;
            await PowerCmd.Apply(context, ModelDb.GetById<PowerModel>(ModelDb.GetId(buff.Type)).ToMutable(),
                Owner.Creature, buff.Amount, Owner.Creature, this);
        }
    }
}
