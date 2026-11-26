using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterPower]
public sealed class WeiCaoCaoBiZuoPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/bi_zuo_buff.png";
    [SavedProperty]public bool InExtraTurn{get;set;}
    [SavedProperty]public bool StripOnStart{get;set;}
    [SavedProperty]public bool ExtraTurnUsed{get;set;}
    public override bool ShouldTakeExtraTurn(Player player)=>player.Creature==Owner && !ExtraTurnUsed && !InExtraTurn && !Owner.IsDead &&
        Owner.CombatState!.Creatures.Where(c=>c.IsAlive).All(c=>Owner.CurrentHp<=c.CurrentHp);
    public override Task AfterTakingExtraTurn(Player player)
    {
        if(player.Creature==Owner)
        {
            StripOnStart=ShouldTakeExtraTurn(player);
            if(StripOnStart)ExtraTurnUsed=true;
            InExtraTurn=true;
        }
        return Task.CompletedTask;
    }
    private readonly List<PowerModel> _suspended = [];

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState state)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return Task.CompletedTask;
        InExtraTurn = StripOnStart || CombatManager.Instance.PlayersTakingExtraTurn.Contains(Owner.Player!);
        if (StripOnStart)
        {
            StripOnStart = false;
            WeiCaoCaoTurnNotice.Show(this);
            SuspendEnemyBuffs(state);
        }
        return Task.CompletedTask;
    }

    internal void SuspendEnemyBuffs(ICombatState state)
    {
        foreach (var enemy in state.Enemies.Where(enemy => enemy.IsAlive))
        foreach (var power in enemy.Powers.Where(CanSuspend).ToArray())
        {
            // Detach without applying/removing gameplay effects or losing per-instance state.
            enemy.RemovePowerInternal(power);
            _suspended.Add(power);
        }
    }

    internal static bool CanSuspend(PowerModel power)
    {
        if (!power.IsVisible || power.Amount <= 0 || power.TypeForCurrentAmount != PowerType.Buff)
            return false;

        return power.GetType().Name is not ("AdaptablePower" or "BattlewornDummyTimeLimitPower"
            or "SandpitPower" or "SlumberPower" or "AsleepPower" or "MinionPower"
            or "BackAttackLeftPower" or "BackAttackRightPower");
    }

    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return Task.CompletedTask;

        // InExtraTurn remains true until the next turn starts so this round is not a new trigger.
        foreach (var power in _suspended)
        {
            var enemy = power.Owner;
            if (!enemy.IsAlive || Owner.CombatState?.Enemies.Contains(enemy) != true) continue;
            var replacement = power.InstanceType == PowerInstanceType.None
                ? enemy.Powers.FirstOrDefault(current => current.GetType() == power.GetType())
                : null;
            if (replacement != null)
                enemy.RemovePowerInternal(replacement);
            enemy.ApplyPowerInternal(power);
            if (replacement != null && power.StackType != PowerStackType.None)
                power.SetAmount(power.Amount + replacement.Amount, silent: true);
        }
        _suspended.Clear();
        return Task.CompletedTask;
    }
}

[RegisterPower]
public sealed class WeiCaoCaoWuMeiPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/wu_mei_buff.png";
    [SavedProperty]public bool Pending{get;set;}=true;
    [SavedProperty]public bool Active{get;set;}
    [SavedProperty]public string Snapshot{get;set;}="";
    public override bool ShouldTakeExtraTurn(Player p)=>p.Creature==Owner && Pending && !Active && !Owner.IsDead;
    internal static string CaptureHp(Creature owner, ICombatState combat) =>
        string.Join('|', combat.Enemies
            .Where(enemy => enemy.IsAlive && enemy.CombatId.HasValue)
            .Select(enemy => $"{enemy.CombatId}:{enemy.CurrentHp}")
            .Prepend($"self:{owner.CurrentHp}"));

    internal void BeginDream(string snapshot)
    {
        if (!Pending || Active) return;
        Snapshot = snapshot;
        Active = true;
    }

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (!ShouldTakeExtraTurn(player)) return;
        Pending = false;
        await PowerCmd.Remove(this);
    }
    public override async Task AfterSideTurnEndLate(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(!Active||side!=CombatSide.Player||!participants.Contains(Owner))return;
        Active=false;
        string snapshot=Snapshot;
        Snapshot="";
        foreach(var entry in snapshot.Split('|',StringSplitOptions.RemoveEmptyEntries))
        {
            var pair=entry.Split(':');
            if(pair.Length!=2 || !int.TryParse(pair[1],out int hp) || hp<=0)continue;
            var target=pair[0]=="self" ? Owner : uint.TryParse(pair[0],out uint id)
                ? Owner.CombatState?.Enemies.FirstOrDefault(enemy=>enemy.CombatId==id) : null;
            if(target is not {IsAlive:true})continue;
            int restoredHp=Math.Min(hp,target.MaxHp);
            if(target.CurrentHp!=restoredHp)await CreatureCmd.SetCurrentHp(target,restoredHp);
        }
        // End-of-side hooks run before the native extra-turn query. Keep the
        // fulfilled power long enough to request the immediate follow-up turn.
        if (!Pending) await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class WeiCaoCaoNoBlockPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/shi_zha_buff.png";
    public override PowerType Type=>PowerType.Debuff;
    public override decimal ModifyBlockMultiplicative(Creature target,decimal block,ValueProp props,CardModel? source,CardPlay? play)=>target==Owner?0:1;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c,CombatSide side,IEnumerable<Creature> participants)
    {if(side==CombatSide.Player)await PowerCmd.Remove(this);}
}

[RegisterPower]
public sealed class WeiCaoCaoMingFaPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/ming_fa_buff.png";
    public override PowerStackType StackType=>PowerStackType.Counter;
    [SavedProperty]public string PendingCards{get;set;}="";
    internal void Add(CardModel card,int count)=>PendingCards+=$"{card.Id.Entry}~{card.CurrentUpgradeLevel}~{count}|";
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context,Player player)
    {
        if(player.Creature!=Owner||PendingCards.Length==0)return;
        string entries=PendingCards;PendingCards="";
        await PowerCmd.Remove(this);
        foreach(var line in entries.Split('|',StringSplitOptions.RemoveEmptyEntries))
        {
            if(Owner.IsDead||CombatManager.Instance.IsOverOrEnding)break;
            var p=line.Split('~');
            if(p.Length!=3||!int.TryParse(p[1],out int upgrades)||!int.TryParse(p[2],out int count)||count<=0)continue;
            var canonical=ModelDb.GetById<CardModel>(new ModelId("CARD",p[0]));
            var card=Owner.CombatState!.CreateCard(canonical,player);
            for(int i=0;i<Math.Min(upgrades,card.MaxUpgradeLevel);i++){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            int replayBonus=count-1;
            card.BaseReplayCount+=replayBonus;
            try { await WeiCaoCaoDesignRules.Play(context,card); }
            finally { card.BaseReplayCount-=replayBonus; }
        }
    }
}

[RegisterPower]
public sealed class WeiCaoCaoBoXuanPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/bo_xuan_buff.png";
    private bool _busy;
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(play.Card.Owner.Creature!=Owner||!play.IsFirstInSeries||play.Card is WeiCaoCaoXiaHouXuanBoXuan||_busy||Owner.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        _busy=true;
        try
        {
            var player = Owner.Player!;
            var drawPile = PileType.Draw.GetPile(player);
            var bottom = drawPile.Cards.TakeLast(Math.Clamp(Amount, 2, 3)).ToArray();
            if (bottom.Length == 0) return;

            var playedCard = play.Card;
            var playedCost = playedCard.EnergyCost;
            int resolvedCost = playedCost.GetResolved();
            int nameLength = WeiCaoCaoDesignRules.NameLength(playedCard);
            bool sameType = bottom.Any(card => card.Type == playedCard.Type);
            bool sameCost = bottom.Any(card => card.EnergyCost.CostsX == playedCost.CostsX &&
                (playedCost.CostsX || card.EnergyCost.GetResolved() == resolvedCost));
            bool sameNameLength = nameLength > 0 && bottom.Any(card => WeiCaoCaoDesignRules.NameLength(card) == nameLength);

            var source = ChoiceSource;
            int count = sameCost ? 1 : 0;
            string prompt = sameCost ? "bottom_optional_play" : "bottom";
            var prefs = new CardSelectorPrefs(new LocString("cards", "YJCM_CARD_WEI_CAO_CAO_DESIGN." + prompt), 0, count)
            {
                RequireManualConfirmation = true
            };
            // The combat-pile selector sends card identities, independent of grid order on each client.
            var chosen = (await CardSelectCmd.FromCombatPile(context, drawPile, player, prefs, bottom.Contains)).FirstOrDefault();

            if (sameType) await WeiCaoCaoDesignRules.Draw(context, source, 1);
            if (chosen?.Pile?.Type is PileType.Draw or PileType.Hand)
                await WeiCaoCaoDesignRules.Play(context, chosen);
            if (sameNameLength && WeiCaoCaoDesignRules.Alive(source))
                await PlayerCmd.GainEnergy(1, Owner.Player!);
        }
        finally{_busy=false;}
    }
    protected override void DeepCloneFields(){base.DeepCloneFields();_busy=false;}
}

[RegisterPower]
public sealed class WeiCaoCaoKangLiPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/kang_li_buff.png";
    private List<CardModel> _pending=[];
    private bool _busy;
    public override async Task AfterDamageGiven(PlayerChoiceContext context,Creature? dealer,DamageResult result,ValueProp props,Creature target,CardModel? source)
    {
        if(dealer!=Owner||target==Owner||result.TotalDamage<=0||_busy)return;
        _busy=true;
        try
        {
            var old=_pending.ToArray();_pending.Clear();
            foreach(var card in old)if(card.Pile?.Type==PileType.Hand)await CardCmd.Discard(context,card);
        }
        finally{_busy=false;}
        await DrawAndTrack(context);
    }
    public override Task AfterDamageReceived(PlayerChoiceContext c,Creature target,DamageResult result,ValueProp props,Creature? dealer,CardModel? source)=>
        target==Owner && result.TotalDamage>0?DrawAndTrack(c):Task.CompletedTask;
    private async Task DrawAndTrack(PlayerChoiceContext context)
    {
        if(_busy||Owner.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        _busy=true;
        try
        {
            _pending.AddRange(await WeiCaoCaoDesignRules.Draw(context,ChoiceSource,2));
        }
        finally{_busy=false;}
    }
    protected override void DeepCloneFields(){base.DeepCloneFields();_pending=[];_busy=false;}
}

[RegisterPower]
public sealed class WeiCaoCaoZhanYiPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/zhan_yi_buff.png";
    [SavedProperty]public int DiscardedType{get;set;}=-1;
    private bool _busy;
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context,Player player)
    {
        if(player.Creature!=Owner||Owner.IsDead)return;
        DiscardedType=-1;
        var source=ChoiceSource;
        var type=await WeiCaoCaoDesignRules.ChooseType(context,source);
        if(type==null)return;
        var cards=PileType.Hand.GetPile(player).Cards.Where(c=>c.Type==type).ToArray();
        if(cards.Length==0)return;
        _busy=true;
        try{foreach(var c in cards)if(c.Pile?.Type==PileType.Hand)await CardCmd.Discard(context,c);DiscardedType=(int)type.Value;}
        finally{_busy=false;}
    }
#if GAME_BETA
    public override decimal ModifyDamageMultiplicative(Creature? target,decimal amount,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)
#else
    public override decimal ModifyDamageMultiplicative(Creature? target,decimal amount,ValueProp props,Creature? dealer,CardModel? card)
#endif
        =>dealer==Owner && DiscardedType>=0 && DiscardedType!=(int)CardType.Attack && card?.Type==CardType.Attack && !props.HasFlag(ValueProp.Unpowered)?2:1;
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(_busy||DiscardedType<0||play.Card.Owner.Creature!=Owner||!play.IsFirstInSeries||Owner.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        if((int)play.Card.Type==DiscardedType)return;
        _busy=true;
        try
        {
            if(play.Card.Type==CardType.Skill)
            {
                foreach(var card in await WeiCaoCaoDesignRules.Draw(context,ChoiceSource,1,true))CardCmd.ApplyKeyword(card,CardKeyword.Retain);
            }
            else if(play.Card.Type==CardType.Power)
            {await PlayerCmd.GainEnergy(1,Owner.Player!);await PowerCmd.Apply<StrengthPower>(context,Owner,1,Owner,null,false);}
        }
        finally{_busy=false;}
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext c,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))DiscardedType=-1;return Task.CompletedTask;}
}

internal static class WeiCaoCaoQuanMouGuard
{
    internal static readonly AsyncLocal<int> DamageDepth=new();
    internal static readonly AsyncLocal<int> BlockDepth=new();
}
[RegisterPower]
public sealed class WeiCaoCaoQuanMouYinPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath=>"res://Yjcm/Images/Powers/mou_sima_yi_quan_mou_yin.png";
    public override PowerType Type=>PowerType.Debuff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override async Task AfterDamageReceived(PlayerChoiceContext context,Creature target,DamageResult result,ValueProp props,Creature? dealer,CardModel? card)
    {
        if(target!=Owner||result.TotalDamage<=0||WeiCaoCaoQuanMouGuard.DamageDepth.Value>0||CombatManager.Instance.IsOverOrEnding)return;
        WeiCaoCaoQuanMouGuard.DamageDepth.Value++;
        try
        {
#if GAME_BETA
            await CreatureCmd.Damage(context,Owner.CombatState!.HittableEnemies.ToArray(),Amount,ValueProp.Unpowered,null,null,null);
#else
            await CreatureCmd.Damage(context,Owner.CombatState!.HittableEnemies.ToArray(),Amount,ValueProp.Unpowered,null,null);
#endif
        }
        finally{WeiCaoCaoQuanMouGuard.DamageDepth.Value--;}
    }
}
[RegisterPower]
public sealed class WeiCaoCaoQuanMouYangPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath=>"res://Yjcm/Images/Powers/mou_sima_yi_quan_mou_yang.png";
    public override PowerType Type=>PowerType.Debuff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override async Task AfterBlockGained(Creature creature,decimal amount,ValueProp props,CardModel? card)
    {
        if(creature!=Owner||amount<=0||WeiCaoCaoQuanMouGuard.BlockDepth.Value>0||CombatManager.Instance.IsOverOrEnding)return;
        WeiCaoCaoQuanMouGuard.BlockDepth.Value++;
        try{foreach(var player in Owner.CombatState!.Players.Where(p=>!p.Creature.IsDead))await CreatureCmd.GainBlock(player.Creature,Amount,ValueProp.Unpowered,null);}
        finally{WeiCaoCaoQuanMouGuard.BlockDepth.Value--;}
    }
}
[RegisterPower]
public sealed class WeiCaoCaoXianMouCountPower:WeiCaoCaoDesignPower
{
    protected override bool IsVisibleInternal => false;
    public override PowerStackType StackType=>PowerStackType.Counter;
}
[RegisterPower]
public sealed class WeiCaoCaoXianMouDrawPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/xian_mou_buff.png";
    public override async Task AfterDamageReceived(PlayerChoiceContext context,Creature target,DamageResult result,ValueProp props,Creature? dealer,CardModel? card)
    {
        if(target!=Owner||result.UnblockedDamage<=0||Owner.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        await PowerCmd.Remove(this);
        await WeiCaoCaoDesignRules.Draw(context,ChoiceSource,2);
    }
}
