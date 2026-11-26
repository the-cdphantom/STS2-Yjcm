using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

#nullable enable
namespace Yjcm;

internal static class WeiCaoCaoMonsterIntentSupport
{
    internal readonly record struct Buff(Type Type, decimal Amount);

    // DefendIntent and BuffIntent have no amounts. This catalog reads the same
    // amounts from the current monster instance without invoking its action.
    internal static decimal Read(MonsterModel monster, MoveState move, IReadOnlyList<Creature> enemies, List<Buff> buffs)
    {
        if (monster.GetType().Assembly != typeof(MonsterModel).Assembly) return 0;
        decimal Number(string name) => ReadNumber(monster, name);
        decimal Block(decimal amount, Creature? recipient = null, ValueProp props = ValueProp.Move) =>
            Hook.ModifyBlock(monster.Creature.CombatState!, recipient ?? monster.Creature, amount, props, null, null, out _);
        void Give<T>(decimal amount) where T : PowerModel
        {
            if (amount > 0) buffs.Add(new(typeof(T), amount));
        }
        decimal Strength(decimal amount)
        {
            Give<StrengthPower>(amount);
            return 0;
        }
        decimal StrengthAndBlock(decimal strength, decimal block)
        {
            Give<StrengthPower>(strength);
            return Block(block);
        }

        switch (monster.GetType().Name, move.Id)
        {
            case ("Aeonglass", "EBB_MOVE"): return Block(Number("EbbBlock"));
            case ("Aeonglass", "INCREASING_INTENSITY_MOVE"): return Strength(Number("IncreasingIntensityTotalStrength"));
            case ("AxeRubyRaider", "SWING_1" or "SWING_2"): return Block(Number("SwingBlock"));
            case ("Axebot", "BOOT_UP_MOVE"):
#if GAME_V0113
                return StrengthAndBlock(Number("BootUpStrGain") * Number("RespawnCount"), Number("BootUpBlock"));
#else
                return StrengthAndBlock(Number("BootUpStrGain") * (2 - Number("StockAmount")), Number("BootUpBlock"));
#endif
            case ("BowlbugEgg", "BITE_MOVE"): return Block(Number("ProtectBlock"));
            case ("BowlbugNectar", "BUFF_MOVE"): return Strength(Number("BuffStrengthGain"));
            case ("BruteRubyRaider", "ROAR_MOVE"): return Strength(3);
            case ("BygoneEffigy", "WAKE_MOVE"): return Strength(10);
            case ("CalcifiedCultist" or "DampCultist", "INCANTATION_MOVE"):
                Give<RitualPower>(Number("IncantationAmount"));
                return 0;
            case ("CeremonialBeast", "CRUSH_MOVE"): return Strength(Number("CrushStrength"));
            case ("CeremonialBeast", "PLOW_MOVE"):
#if GAME_V0113
                if (!monster.Creature.HasPower<PlowPower>()) return 0;
#endif
                return Strength(Number("PlowStrength"));
            case ("CrossbowRubyRaider", "RELOAD_MOVE"): return Block(3);
            case ("Crusher", "ADAPT_MOVE"): return Strength(Number("AdaptStrengthGain"));
            case ("Crusher", "GUARDED_STRIKE_MOVE"): return Block(18);
            case ("CubexConstruct", "CHARGE_UP_MOVE" or "REPEATER_BLAST_MOVE" or "REPEATER_BLAST_MOVE_2"):
            case ("Exoskeleton" or "FakeMerchantMonster", "ENRAGE_MOVE"):
            case ("GlobeHead", "GALVANIC_BURST"):
            case ("GremlinMerc", "HEHE_MOVE"):
            case ("ScrollOfBiting", "MORE_TEETH"):
            case ("SlumberingBeetle", "ROLL_OUT_MOVE"):
            case ("SnappingJaxfruit", "ENERGY_ORB_MOVE"):
            case ("TheAdversaryMkOne", "BARRAGE_MOVE"):
            case ("Queen", "ENRAGE_MOVE"):
                return Strength(2);
            case ("DecimillipedeSegment", "BULK_MOVE"): return Strength(Number("BulkStrength"));
            case ("DevotedSculptor", "FORBIDDEN_INCANTATION_MOVE"):
                Give<RitualPower>(9);
                return 0;
            case ("Entomancer", "PHEROMONE_SPIT_MOVE"):
                // The other branch adds Personal Hive, which requires a player attacker with a deck.
                return Strength(monster.Creature.GetPower<PersonalHivePower>() is { Amount: < 3 } ? 1 : 2);
            case ("FlailKnight", "WAR_CHANT"):
            case ("SlimedBerserker", "LEECHING_HUG_MOVE"):
            case ("SludgeSpinner", "RAGE_MOVE"):
            case ("TheAdversaryMkTwo", "BARRAGE_MOVE"):
                return Strength(3);
            case ("Fogmog", "SWIPE_MOVE" or "SWIPE_RANDOM_MOVE"):
            case ("TurretOperator", "RELOAD_MOVE"):
                return Strength(1);
            case ("FrogKnight", "FOR_THE_QUEEN"): return Strength(5);
            case ("FuzzyWurmCrawler", "INHALE"): return Strength(7);
            case ("Guardbot", "GUARD_MOVE"):
                return enemies.Where(enemy => enemy.Monster is Fabricator).Sum(enemy => Block(15, enemy, ValueProp.Unpowered));
#if !GAME_V0113
            case ("InfestedPrism", "PULSATE_MOVE"): return Block(Number("PulsateBlock"));
            case ("InfestedPrism", "RADIATE_MOVE"): return Block(Number("RadiateBlock"));
#endif
            case ("KinFollower", "POWER_DANCE_MOVE"): return Strength(Number("DanceStrength"));
            case ("KinPriest", "RITUAL_MOVE"): return Strength(Number("RitualStrength"));
            case ("KnowledgeDemon", "PONDER_MOVE"): return Strength(Number("PonderStrength"));
            case ("LagavulinMatriarch", "SLASH2_MOVE"): return Block(Number("Slash2Block"));
            case ("LagavulinMatriarch", "SOUL_SIPHON_MOVE"): return Strength(2);
            case ("LivingShield", "SMASH_MOVE"): return Strength(Number("EnrageStr"));
            case ("LouseProgenitor", "CURL_AND_GROW_MOVE"):
#if GAME_V0113
                return StrengthAndBlock(Number("GrowStrength"), Number("CurlBlock"));
#else
                return StrengthAndBlock(5, Number("CurlBlock"));
#endif
            case ("MagiKnight", "POWER_SHIELD_MOVE" or "PREP_MOVE"): return Block(Number("PowerShieldBlock"));
            case ("MechaKnight", "WINDUP_MOVE"): return StrengthAndBlock(5, 15);
            case ("Myte", "SUCK_MOVE"): return Strength(Number("SuckStrength"));
            case ("Nibbit", "HISS_MOVE"): return Strength(Number("HissStrengthGain"));
            case ("Nibbit", "SLICE_MOVE"): return Block(Number("SliceBlock"));
            case ("Ovicopter", "NUTRITIONAL_PASTE_MOVE"): return Strength(Number("NutritionalPasteStrengthAmount"));
            case ("OwlMagistrate", "JUDICIAL_FLIGHT"):
                Give<SoarPower>(1);
                return 0;
            case ("PhantasmalGardener", "ENLARGE_MOVE"): return Strength(Number("EnlargeStr"));
            case ("PunchConstruct", "READY_MOVE"): return Block(10);
            case ("Queen", "BURN_BRIGHT_FOR_ME_MOVE"):
#if GAME_V0113
                int strength = MegaCrit.Sts2.Core.Helpers.AscensionHelper.IsDread() ? 2 : 1;
#else
                int strength = 1;
#endif
                Give<StrengthPower>(strength * enemies.Count(enemy => enemy != monster.Creature));
                return Block(20);
            case ("Rocket", "CHARGE_UP_MOVE"): return Strength(Number("ChargeUpStrengthGain"));
            case ("Seapunk", "BUBBLE_BURP_MOVE"): return StrengthAndBlock(Number("BubbleStr"), Number("BubbleBlock"));
            case ("SewerClam", "PRESSURIZE_MOVE"):
            case ("TheAdversaryMkThree", "BARRAGE_MOVE"):
                return Strength(4);
            case ("SkulkingColony", "INERTIA_MOVE"): return Strength(Number("InertiaStrengthGain"));
            case ("SlitheringStrangler", "THWACK"): return Block(5);
            case ("SoulFysh", "FADE_MOVE"):
                Give<IntangiblePower>(2);
                return 0;
            case ("SpinyToad", "PROTRUDING_SPIKES_MOVE"):
                Give<ThornsPower>(5);
                return 0;
            case ("TerrorEel", "THRASH_MOVE"):
                Give<VigorPower>(6);
                return 0;
            case ("TestSubject", "BURNING_GROWL_MOVE"): return Strength(Number("BurningGrowlStrengthGain"));
            case ("TestSubject", "RESPAWN_MOVE"):
                if (Number("Respawns") == 1) Give<NemesisPower>(1);
                return 0;
            case ("TheForgotten", "MIASMA"):
                Give<DexterityPower>(Number("DebilitatingSmogDexStealAmount"));
                return Block(8);
            case ("TheInsatiable", "SALIVATE_MOVE"): return Strength(Number("SalivateStrength"));
            case ("TheLost", "DEBILITATING_SMOG"): return Strength(Number("DebilitatingSmogStrengthStealAmount"));
            case ("TheObscura", "HARDENING_STRIKE_MOVE"): return Block(Number("HardeningStrikeBlock"));
            case ("TheObscura", "SAIL_MOVE"): return Strength(3 * enemies.Count(enemy => enemy != monster.Creature));
            case ("Toadpole", "SPIKEN_MOVE"):
                Give<ThornsPower>(Number("SpikenAmount"));
                return 0;
            case ("Tunneler", "BURROW_MOVE"):
                Give<BurrowedPower>(1);
                return Block(Number("BlockGain"));
            case ("Vantom", "PREPARE_MOVE"):
#if GAME_V0113
                return Strength(Number("PrepareStrength"));
#else
                return Strength(2);
#endif
            case ("Wriggler", "WRIGGLE_MOVE"): return Strength(2);
            // Plow is a debuff, and these other powers assume a specific monster phase,
            // access a monster-only controller, or affect all player decks.
            case ("CeremonialBeast", "STAMP_MOVE"):
#if GAME_V0113
            case ("InfestedPrism", "PULSATE_MOVE"):
#endif
            case ("TheInsatiable", "LIQUIFY_GROUND_MOVE"):
            case ("ThievingHopper", "FLUTTER_MOVE"):
            case ("WaterfallGiant", _):
            default:
                return 0;
        }
    }

    private static decimal ReadNumber(MonsterModel monster, string name)
    {
        var type = monster.GetType();
        var value = AccessTools.Property(type, name)?.GetValue(monster)
            ?? AccessTools.Field(type, name)?.GetValue(monster)
            ?? throw new MissingMemberException(type.FullName, name);
        return Convert.ToDecimal(value);
    }
}
