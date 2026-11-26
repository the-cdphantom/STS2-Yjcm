using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

public abstract class QingShiRelic : ModRelicTemplate
{
    private readonly record struct Option(CardModel Model, int Index);
    private int _usedOptions;
    private bool _disabledThisTurn;
    private bool _choosing;
    private HashSet<string>? _usedNames;
    private HashSet<CardPlay>? _doubleAttacks;
    private int _pendingDamage;
    private DoubleDamagePower? _nativeDoubleDamage;

    protected abstract bool IsRefined { get; }
    protected abstract string TriggerVoicePath { get; }

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => "res://Yjcm/Images/Relics/wuzhugeliang-wu_zhugeliang_qing_shi.png";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => "res://Yjcm/Images/Relics/Big/wuzhugeliang-wu_zhugeliang_qing_shi.png";

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        _usedNames = new HashSet<string>(StringComparer.Ordinal);
        _doubleAttacks = [];
        _pendingDamage = 0;
        _nativeDoubleDamage = null;
        _usedOptions = 0;
        _disabledThisTurn = false;
        _choosing = false;

        if (Owner is not { } player) return;
        var context = new ThrowingPlayerChoiceContext();
        await EquipCmd.ChannelEquipment(context, player, (EquipmentOrbModel)ModelDb.Orb<EmptyWeaponSlotOrb>().ToMutable(), false);
        await EquipCmd.ChannelEquipment(context, player, (EquipmentOrbModel)ModelDb.Orb<EmptyArmorSlotOrb>().ToMutable(), false);
        await EquipCmd.ChannelEquipment(context, player, (EquipmentOrbModel)ModelDb.Orb<EmptyDefensiveHorseSlotOrb>().ToMutable(), false);
        await EquipCmd.ChannelEquipment(context, player, (EquipmentOrbModel)ModelDb.Orb<EmptyOffensiveHorseSlotOrb>().ToMutable(), false);
        await EquipCmd.ChannelEquipment(context, player, (EquipmentOrbModel)ModelDb.Orb<EmptyTreasureSlotOrb>().ToMutable(), false);
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature))
        {
            _usedOptions = 0;
            _disabledThisTurn = false;
            _usedNames?.Clear();
            _doubleAttacks?.Clear();
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (_disabledThisTurn || _choosing || play.PlayIndex != 0 || play.Card.Owner != Owner ||
            Owner.Creature.IsDead || QingShiCardPlayContextPatch.Current.Value is not { } context)
            return;

        string name = NameKey(play.Card);
        if (IsRefined ? _usedNames?.Contains(name) == true : _usedOptions == 0b111)
            return;

        if (!PileType.Hand.GetPile(Owner).Cards.Any(card => card != play.Card && NameKey(card) == name))
            return;

        _choosing = true;
        context.PushModel(this);
        try
        {
            var options = CreateOptions();
            var chosen = await CardSelectCmd.FromChooseACardScreen(context, options.Select(option => option.Model).ToArray(), Owner);
            if (chosen == null || Owner.Creature.IsDead)
                return;

            int index = options.First(option => ReferenceEquals(option.Model, chosen)).Index;
            if (IsRefined)
                (_usedNames ??= new(StringComparer.Ordinal)).Add(name);
            else
                _usedOptions |= 1 << index;

            VoicePlayer.PlayWav(TriggerVoicePath);
            switch (index)
            {
                case 0:
                    if (IsRefined)
                        await UpgradeAttackInHand(context);
                    ArmDoubleDamage(play);
                    break;
                case 1:
                    await PlayerCmd.GainEnergy(IsRefined ? 3 : 1, Owner);
                    if (IsRefined)
                    {
                        await PowerCmd.Apply<StrengthPower>(context, Owner.Creature, 1, Owner.Creature, null, false);
                        await PowerCmd.Apply<DexterityPower>(context, Owner.Creature, 1, Owner.Creature, null, false);
                    }
                    break;
                case 2:
                    _disabledThisTurn = true;
                    await CardPileCmd.Draw(context, IsRefined ? 7 : 3, Owner, false);
                    break;
            }
        }
        finally
        {
            context.PopModel(this);
            _choosing = false;
        }
    }

    // Different character copies have the same base name. Use model IDs rather
    // than localized titles so upgrades and different client languages agree.
    internal static string NameKey(CardModel card)
    {
        string entry = card.Id.Entry;
        const string prefix = "YJCM_CARD_";
        foreach (string copy in new[] { "WEI_DONG_ZHUO_", "WEI_CAO_CAO_" })
        {
            if (entry.StartsWith(prefix + copy, StringComparison.Ordinal))
                return prefix + entry[(prefix.Length + copy.Length)..];
        }
        return entry;
    }

    private List<Option> CreateOptions()
    {
        var options = new List<Option>(3);
        Add<QingShiDamageOption>(0);
        Add<QingShiEnergyOption>(1);
        Add<QingShiDrawOption>(2);
        return options;

        void Add<T>(int index) where T : CardModel
        {
            if (!IsRefined && (_usedOptions & (1 << index)) != 0)
                return;
            var card = ModelDb.Card<T>().ToMutable();
            card.Owner = Owner;
            if (IsRefined) card.UpgradeInternal();
            options.Add(new Option(card, index));
        }
    }

    private async Task UpgradeAttackInHand(PlayerChoiceContext context)
    {
        static bool CanUpgrade(CardModel card) => card.Type == CardType.Attack && card.IsUpgradable;
        if (!PileType.Hand.GetPile(Owner).Cards.Any(CanUpgrade))
            return;
        var chosen = (await CardSelectCmd.FromHand(context, Owner,
            new CardSelectorPrefs(new("cards", "qing_shi.upgrade_attack_prompt"), 1, 1),
            CanUpgrade, this)).FirstOrDefault();
        if (chosen?.Pile?.Type == PileType.Hand)
            CardCmd.Upgrade(chosen);
    }

    private void ArmDoubleDamage(CardPlay play)
    {
        if (_nativeDoubleDamage == null)
        {
            _nativeDoubleDamage = (DoubleDamagePower)ModelDb.Power<DoubleDamagePower>().ToMutable();
            SetPowerOwner(_nativeDoubleDamage, Owner.Creature);
        }
        if (play.Card.Type == CardType.Attack)
            (_doubleAttacks ??= []).Add(play);
        else
            _pendingDamage++;
    }

    // Borrow the native power's modifier without installing its turn-long effect
    // or changing a separate DoubleDamagePower already on the creature.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_Owner")]
    private static extern void SetPowerOwner(PowerModel power, Creature owner);

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play)
    {
        if (_nativeDoubleDamage == null || source is not { IsMutable: true } || source.Owner != Owner)
            return 1m;
        bool immediate = _doubleAttacks?.Any(attack => attack.Card == source && (play == null || attack == play)) == true;
        return immediate || _pendingDamage > 0
            ? _nativeDoubleDamage.ModifyDamageMultiplicative(target, amount, props, dealer, source, play)
            : 1m;
    }

    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? source)
    {
        if (_pendingDamage > 0 && source is { IsMutable: true } && source.Owner == Owner &&
            _doubleAttacks?.Any(attack => attack.Card == source) != true &&
            _nativeDoubleDamage?.ModifyDamageMultiplicative(target, 0m, props, dealer, source, null) == 2m)
            _pendingDamage--;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        _doubleAttacks?.Remove(play);
        return Task.CompletedTask;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class QingShiCardPlayContextPatch
{
    internal static readonly AsyncLocal<PlayerChoiceContext?> Current = new();

    // BeforeCardPlayed is awaited but does not receive the enclosing choice context.
    // Capture it for the async play, then immediately restore the caller's context.
    private static void Prefix(PlayerChoiceContext choiceContext, out PlayerChoiceContext? __state)
    {
        __state = Current.Value;
        Current.Value = choiceContext;
    }

    private static void Finalizer(PlayerChoiceContext? __state) => Current.Value = __state;
}
