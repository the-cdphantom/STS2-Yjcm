using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_XUN_YU_BI_ZUO")]
public sealed class WeiCaoCaoMouXunYuBiZuo:WeiCaoCaoDesignCard
{
    public WeiCaoCaoMouXunYuBiZuo():base(2,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)
    {
        var power=await PowerCmd.Apply<WeiCaoCaoBiZuoPower>(c,Owner.Creature,1,Owner.Creature,this,false);
        power.InExtraTurn=CombatManager.Instance.PlayersTakingExtraTurn.Contains(Owner);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>[HoverTipFactory.FromPower<WeiCaoCaoBiZuoPower>()];
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_WEI_CAO_PI_DIAN_LUN")]
public sealed class WeiCaoCaoWeiCaoPiDianLun:WeiCaoCaoDesignCard
{
    public WeiCaoCaoWeiCaoPiDianLun():base(2,CardType.Skill,CardRarity.Rare,TargetType.Self, hasJunHeEnhancement: true){}
    internal static bool IsProgression(IEnumerable<int> costs)
    {
        var sorted=costs.Order().ToArray();
        if(sorted.Length<2)return true;
        int step=sorted[1]-sorted[0];
        return step>0 && sorted.Skip(1).Select((v,i)=>v-sorted[i]).All(gap=>gap==step);
    }
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        // Invalid selections are re-presented without moving or charging any cards.
        IReadOnlyList<CardModel> selected;
        do
        {
            selected=await WeiCaoCaoDesignRules.Hand(context,this,0,int.MaxValue,
                c=>!c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.All)>=0,
                "progression",forDiscard:true,releaseSelectionOnConfirm:true);
            if(selected.Count==0)return;
        }while(!IsProgression(selected.Select(c=>c.EnergyCost.GetResolved())) && WeiCaoCaoDesignRules.Alive(this));
        if(!WeiCaoCaoDesignRules.Alive(this))return;
        var valid=selected.Where(c=>c.Pile?.Type==PileType.Hand).ToArray();
        if(valid.Length==0)return;
        bool military=await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this);
        if(!WeiCaoCaoDesignRules.Alive(this))return;
        valid=valid.Where(c=>c.Pile?.Type==PileType.Hand).ToArray();
        if(valid.Length==0)return;
        await CardCmd.Discard(context,valid);
        await WeiCaoCaoDesignRules.Draw(context,this,valid.Length*(military?2:1),freeUntilPlayed:true);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_ZHOU_XUAN_WU_MEI")]
public sealed class WeiCaoCaoZhouXuanWuMei:WeiCaoCaoDesignCard
{
    [SavedProperty]public bool Started{get;set;}
    private string? _turnStartSnapshot;
    private int _snapshotTurn;

    public WeiCaoCaoZhouXuanWuMei():base(1,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    public override HashSet<CardKeyword> CanonicalKeywords=>[CardKeyword.Innate];

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // The native hook runs before opening draws and before this card auto-plays.
        if (side == CombatSide.Player && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { } turn)
        {
            _snapshotTurn = turn.TurnNumber;
            _turnStartSnapshot = WeiCaoCaoWuMeiPower.CaptureHp(Owner.Creature, combatState);
        }
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (Owner.Creature.CombatState is not { } combat) return;
        var snapshot = combat.CurrentSide == CombatSide.Player && Owner.PlayerCombatState?.TurnNumber == _snapshotTurn
            ? _turnStartSnapshot : null;
        snapshot ??= WeiCaoCaoWuMeiPower.CaptureHp(Owner.Creature, combat);
        var dream = await PowerCmd.Apply<WeiCaoCaoWuMeiPower>(context, Owner.Creature, 1, Owner.Creature, this, false);
        dream?.BeginDream(snapshot);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c,Player player)
    {
        if(Started||player!=Owner||Pile?.Type!=PileType.Hand||CombatState.RoundNumber!=1||!WeiCaoCaoDesignRules.Alive(this))return;
        Started=true;await CardCmd.AutoPlay(c,this,null,AutoPlayType.Default,false,false);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_JIANG_JI_YING_SHI")]
public sealed class WeiCaoCaoMouJiangJiYingShi:WeiCaoCaoDesignCard
{
    public WeiCaoCaoMouJiangJiYingShi():base(2,CardType.Skill,CardRarity.Common,TargetType.Self, hasJunHeEnhancement: true){}
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>base.AdditionalHoverTips.Concat([
        HoverTipFactory.Static(StaticHoverTip.ReplayDynamic,new DynamicVar("Times",1))]);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var discard=(await WeiCaoCaoDesignRules.Hand(context,this,0,1,prompt:"discard",forDiscard:true)).FirstOrDefault();
        if(discard==null)return;
        await CardCmd.Discard(context,discard);
        var chosen=(await WeiCaoCaoDesignRules.Hand(context,this,1,1,c=>c.Type==CardType.Skill)).FirstOrDefault();
        if(chosen!=null)chosen.BaseReplayCount++;
        if(await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this))await WeiCaoCaoDesignRules.Draw(context,this,1);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_CHENG_YU_SHI_ZHA")]
public sealed class WeiCaoCaoMouChengYuShiZha:WeiCaoCaoDesignCard
{
    public WeiCaoCaoMouChengYuShiZha():base(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(8,ValueProp.Move)];
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(4);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        await WeiDongZhuoQunEffects.Attack(this,play,DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
        if(!WeiCaoCaoDesignRules.Alive(this))return;
        var top=PileType.Draw.GetPile(Owner).Cards.LastOrDefault();
        if(top==null)return;
        if(top.Type==CardType.Attack)await PowerCmd.Apply<BufferPower>(context,Owner.Creature,1,Owner.Creature,this,false);
        else if(top.Type==CardType.Skill && play.Target is {IsDead:false} target)await PowerCmd.Apply<WeiCaoCaoNoBlockPower>(context,target,1,Owner.Creature,this,false);
        else if(top.Type==CardType.Power)
        {
            await WeiCaoCaoDesignRules.Play(context,top);
            if(WeiCaoCaoDesignRules.Alive(this))await PlayerCmd.GainEnergy(1,Owner);
        }
    }
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_YANG_HU_MING_FA")]
public sealed class WeiCaoCaoYangHuMingFa:WeiCaoCaoDesignCard
{
    public WeiCaoCaoYangHuMingFa():base(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self){}
    protected override bool HasEnergyCostX=>true;
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var chosen=(await WeiCaoCaoDesignRules.Hand(context,this,1,1,c=>c.Type is CardType.Attack or CardType.Skill or CardType.Power)).FirstOrDefault();
        int count=Math.Max(0,ResolveEnergyXValue())+(IsUpgraded?1:0);
        if(chosen==null||count==0)return;
        var power=await PowerCmd.Apply<WeiCaoCaoMingFaPower>(context,Owner.Creature,count,Owner.Creature,this,false);
        power.Add(chosen,count);
    }
    protected override void OnUpgrade(){}
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_XIA_HOU_XUAN_BO_XUAN")]
public sealed class WeiCaoCaoXiaHouXuanBoXuan:WeiCaoCaoDesignCard
{
    public WeiCaoCaoXiaHouXuanBoXuan():base(3,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Count",2)];
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)=>await PowerCmd.Apply<WeiCaoCaoBoXuanPower>(c,Owner.Creature,(int)DynamicVars["Count"].BaseValue,Owner.Creature,this,false);
    protected override void OnUpgrade()=>DynamicVars["Count"].UpgradeValueBy(1);
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_SUN_LI_KANG_LI")]
public sealed class WeiCaoCaoSunLiKangLi:WeiCaoCaoDesignCard
{
    public WeiCaoCaoSunLiKangLi():base(1,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)=>await PowerCmd.Apply<WeiCaoCaoKangLiPower>(c,Owner.Creature,1,Owner.Creature,this,false);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_ZHU_LING_ZHAN_YI")]
public sealed class WeiCaoCaoZhuLingZhanYi:WeiCaoCaoDesignCard
{
    public WeiCaoCaoZhuLingZhanYi():base(1,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)=>await PowerCmd.Apply<WeiCaoCaoZhanYiPower>(c,Owner.Creature,1,Owner.Creature,this,false);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_SHEN_DENG_AI_TUO_YU")]
public sealed class WeiCaoCaoShenDengAiTuoYu:WeiCaoCaoDesignCard
{
    public WeiCaoCaoShenDengAiTuoYu():base(2,CardType.Attack,CardRarity.Rare,TargetType.AnyEnemy, hasJunHeEnhancement: true){}
    protected override HashSet<CardTag> CanonicalTags=>[YjcmCardTags.WuJiang,YjcmCardTags.ShenJiang];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(15,ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>base.AdditionalHoverTips
        .Concat(HoverTipFactory.FromEnchantment<WeiCaoCaoFengTian>()).Concat(HoverTipFactory.FromEnchantment<WeiCaoCaoQingQu>()).Concat(HoverTipFactory.FromEnchantment<WeiCaoCaoJunShan>());
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        await WeiDongZhuoQunEffects.Attack(this,play,DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
        if(!WeiCaoCaoDesignRules.Alive(this))return;
        bool military=await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this);
        var cards=await WeiCaoCaoDesignRules.Hand(context,this,1,military?2:1,c=>c.Type==CardType.Attack && WeiCaoCaoDesignRules.CanEnchant(c,ModelDb.Enchantment<WeiCaoCaoJunShan>()));
        foreach(var card in cards)
        {
            CardModel[] choices=[CombatState.CreateCard<WeiCaoCaoChoiceFengTian>(Owner),CombatState.CreateCard<WeiCaoCaoChoiceQingQu>(Owner),CombatState.CreateCard<WeiCaoCaoChoiceJunShan>(Owner)];
            var option=(await WeiCaoCaoDesignRules.Choose(context,this,choices,1,1)).FirstOrDefault();
            if(option==null)continue;
            var enchant=option switch {WeiCaoCaoChoiceFengTian=>ModelDb.Enchantment<WeiCaoCaoFengTian>().ToMutable(),WeiCaoCaoChoiceQingQu=>ModelDb.Enchantment<WeiCaoCaoQingQu>().ToMutable(),_=>ModelDb.Enchantment<WeiCaoCaoJunShan>().ToMutable()};
            if(card.Pile?.Type!=PileType.Hand || !WeiCaoCaoDesignRules.CanEnchant(card,enchant))continue;
            if(card.Enchantment!=null)CardCmd.ClearEnchantment(card);
            CardCmd.Enchant(enchant,card,1);
        }
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(10);
}

public abstract class WeiCaoCaoShiMou(bool yin):WeiCaoCaoDesignCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self)
{
    internal static bool CanPrint(CardModel card) =>
        card is not WeiCaoCaoShiMou && card.Rarity != CardRarity.Rare && !card.Tags.Contains(YjcmCardTags.ShenJiang);

    public override HashSet<CardKeyword> CanonicalKeywords=>[YjcmKeywords.ZhuanHuanJi];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>base.AdditionalHoverTips.Concat([
        yin?HoverTipFactory.FromCard<WeiCaoCaoMouXunYuShiMouYang>(IsUpgraded):HoverTipFactory.FromCard<WeiCaoCaoMouXunYuShiMouYin>(IsUpgraded),
        HoverTipFactory.Static(StaticHoverTip.ReplayDynamic,new DynamicVar("Times",1)),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)]);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        bool discarded=false,drew=false;
        List<HashSet<CardModel>> visited=[];
        while(WeiCaoCaoDesignRules.Alive(this))
        {
            var hand=PileType.Hand.GetPile(Owner).Cards.ToHashSet();
            if(hand.Count==5 || visited.Any(previous=>previous.SetEquals(hand)))break;
            visited.Add(hand);
            if(hand.Count>5)
            {
                var selected=await WeiCaoCaoDesignRules.Hand(context,this,hand.Count-5,hand.Count-5,prompt:"discard",forDiscard:true);
                if(selected.Count==0)break;
                foreach(var card in selected)if(card.Pile?.Type==PileType.Hand){await CardCmd.Discard(context,card);discarded=true;}
            }
            else
            {
                var drawn=await WeiCaoCaoDesignRules.Draw(context,this,5-hand.Count);
                if(drawn.Count==0)break;
                drew=true;
            }
        }
        CardModel CreateTemporaryCard(CardModel model)
        {
            var card=CombatState.CreateCard(model,Owner);
            if(!card.Keywords.Contains(CardKeyword.Exhaust))card.AddKeyword(CardKeyword.Exhaust);
            return card;
        }

        var options=WeiCaoCaoDesignRules.Pool(this,yin?CardType.Attack:CardType.Skill)
            .Where(CanPrint).Select(CreateTemporaryCard).ToArray();
        var chosen=(await WeiCaoCaoDesignRules.Choose(context,this,options,1,1)).FirstOrDefault();
        if(chosen!=null)
        {
            if(discarded)chosen.BaseReplayCount++;
            if(drew && WeiCaoCaoFuPlay.NeedsTarget(chosen.TargetType))
            {
                var targets=CombatState.Creatures.Where(chosen.IsValidTarget).ToArray();
                foreach(var target in targets)
                {
                    if(!WeiCaoCaoDesignRules.Alive(this))break;
                    var copy=CreateTemporaryCard(ModelDb.GetById<CardModel>(chosen.Id));
                    copy.BaseReplayCount=chosen.BaseReplayCount;
                    await CardCmd.AutoPlay(context,copy,target,AutoPlayType.Default,false,false);
                }
            }
            else await WeiCaoCaoDesignRules.Play(context,chosen);
        }
        if(yin)await WeiCaoCaoDesignRules.Convert<WeiCaoCaoMouXunYuShiMouYang>(context,this);
        else await WeiCaoCaoDesignRules.Convert<WeiCaoCaoMouXunYuShiMouYin>(context,this);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_XUN_YU_SHI_MOU_YANG")]
public sealed class WeiCaoCaoMouXunYuShiMouYang:WeiCaoCaoShiMou{public WeiCaoCaoMouXunYuShiMouYang():base(false){}}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_XUN_YU_SHI_MOU_YIN")]
public sealed class WeiCaoCaoMouXunYuShiMouYin:WeiCaoCaoShiMou{public WeiCaoCaoMouXunYuShiMouYin():base(true){}}

public abstract class WeiCaoCaoQuanMou(bool yang):WeiCaoCaoDesignCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.AnyEnemy,
    hasJunHeEnhancement: true)
{
    public override HashSet<CardKeyword> CanonicalKeywords=>[YjcmKeywords.ZhuanHuanJi];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Count",3)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>base.AdditionalHoverTips.Concat([
        yang?HoverTipFactory.FromCard<WeiCaoCaoMouSiMaYiQuanMouYin>(IsUpgraded):HoverTipFactory.FromCard<WeiCaoCaoMouSiMaYiQuanMouYang>(IsUpgraded),
        yang?HoverTipFactory.FromPower<WeiCaoCaoQuanMouYangPower>():HoverTipFactory.FromPower<WeiCaoCaoQuanMouYinPower>()]);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        int n=(int)DynamicVars["Count"].BaseValue;
        if(yang)await PowerCmd.Apply<WeiCaoCaoQuanMouYangPower>(context,play.Target!,n,Owner.Creature,this,false);
        else await PowerCmd.Apply<WeiCaoCaoQuanMouYinPower>(context,play.Target!,n,Owner.Creature,this,false);
        if(await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this))
        {
            if(yang)await PowerCmd.Apply<BufferPower>(context,Owner.Creature,1,Owner.Creature,this,false);
            else await PowerCmd.Apply<StrengthPower>(context,Owner.Creature,2,Owner.Creature,this,false);
        }
        if(yang)await WeiCaoCaoDesignRules.Convert<WeiCaoCaoMouSiMaYiQuanMouYin>(context,this);
        else await WeiCaoCaoDesignRules.Convert<WeiCaoCaoMouSiMaYiQuanMouYang>(context,this);
    }
    protected override void OnUpgrade()=>DynamicVars["Count"].UpgradeValueBy(2);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_SI_MA_YI_QUAN_MOU_YIN")]
public sealed class WeiCaoCaoMouSiMaYiQuanMouYin:WeiCaoCaoQuanMou{public WeiCaoCaoMouSiMaYiQuanMouYin():base(false){}}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_SI_MA_YI_QUAN_MOU_YANG")]
public sealed class WeiCaoCaoMouSiMaYiQuanMouYang:WeiCaoCaoQuanMou{public WeiCaoCaoMouSiMaYiQuanMouYang():base(true){}}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHE_ZHOU_SHE_FU")]
public sealed class WeiCaoCaoCheZhouSheFu:WeiCaoCaoDesignCard
{
    [SavedProperty]public int RetainedTurns{get;set;}
    [SavedProperty]public bool Returned{get;set;}
    public WeiCaoCaoCheZhouSheFu():base(0,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy, hasJunHeEnhancement: true){}
    public override HashSet<CardKeyword> CanonicalKeywords=>[CardKeyword.Retain];
    internal decimal CurrentDamage=>(IsUpgraded?8:6)+RetainedTurns*6;
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new WeiCaoCaoSheFuDamageVar()];
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext c,Player p)
    {
        if(p==Owner)Returned=false;
        return Task.CompletedTask;
    }
    public override Task AfterFlush(PlayerChoiceContext c,Player p,IReadOnlyCollection<CardModel> flushed,IReadOnlyCollection<CardModel> retained)
    {if(p==Owner && retained.Contains(this))RetainedTurns++;return Task.CompletedTask;}
#if GAME_BETA
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var result=base.GetResultLocationForCardPlay();
        return result.pileType==PileType.Discard && !Returned && WeiCaoCaoJunHePower.AmountOf(Owner.Creature)>0
            ? new CardLocation(Owner,PileType.Hand,CardPilePosition.Bottom) : result;
    }
#else
    protected override PileType GetResultPileTypeForCardPlay()
    {
        var result=base.GetResultPileTypeForCardPlay();
        return result==PileType.Discard && !Returned && WeiCaoCaoJunHePower.AmountOf(Owner.Creature)>0 ? PileType.Hand : result;
    }
#endif
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)
    {
        if(p.IsFirstInSeries && !Returned && await WeiCaoCaoJunHePower.TryConsume(c,Owner.Creature,1,this))Returned=true;
        await WeiDongZhuoQunEffects.Attack(this,p,CurrentDamage).Targeting(p.Target!).Execute(c);
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(2);
}

internal sealed class WeiCaoCaoSheFuDamageVar():DamageVar(6,ValueProp.Move)
{
    public override void UpdateCardPreview(CardModel card,CardPreviewMode mode,Creature? target,bool runGlobalHooks)
    {if(card is WeiCaoCaoCheZhouSheFu s)BaseValue=s.CurrentDamage;base.UpdateCardPreview(card,mode,target,runGlobalHooks);}
}

public abstract class WeiCaoCaoXianMou(bool yin):WeiCaoCaoDesignCard(2,CardType.Skill,CardRarity.Uncommon,yin?TargetType.AnyEnemy:TargetType.Self)
{
    public override HashSet<CardKeyword> CanonicalKeywords=>[YjcmKeywords.ZhuanHuanJi,CardKeyword.Sly];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>base.AdditionalHoverTips.Concat([
        yin?HoverTipFactory.FromCard<WeiCaoCaoMouGuoJiaXianMouYang>(IsUpgraded):HoverTipFactory.FromCard<WeiCaoCaoMouGuoJiaXianMouYin>(IsUpgraded)]);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var player = Owner.Creature;
        int times = (player.Powers.OfType<WeiCaoCaoXianMouCountPower>().FirstOrDefault()?.Amount ?? 0) + 1;
        await PowerCmd.Apply<WeiCaoCaoXianMouCountPower>(context, player, 1, player, this, true);
        if (!yin)
        {
            var options = PileType.Draw.GetPile(Owner).Cards.Take(5).ToArray();
            var chosen = await WeiCaoCaoDesignRules.Choose(context, this, options, 0, times);
            int gained = 0;
            foreach (var card in chosen)
            {
                if (!WeiCaoCaoDesignRules.Alive(this)) break;
                if (card.Pile?.Type != PileType.Draw) continue;
                var move = await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, this);
                if (move.success && move.cardAdded.Pile?.Type == PileType.Hand) gained++;
            }
            if (gained < times && WeiCaoCaoDesignRules.Alive(this))
                await PowerCmd.Apply<WeiCaoCaoXianMouDrawPower>(context, player, 1, player, this, false);
            await WeiCaoCaoDesignRules.Convert<WeiCaoCaoMouGuoJiaXianMouYin>(context, this);
        }
        else
        {
            var chosen = await WeiCaoCaoDesignRules.Hand(context, this, 0, times, prompt: "discard", forDiscard: true);
            var discarded = chosen.Where(card => card.Pile?.Type == PileType.Hand).ToArray();
            if (discarded.Length > 0 && WeiCaoCaoDesignRules.Alive(this))
            {
                await CardCmd.Discard(context, discarded);
                if (WeiCaoCaoDesignRules.Alive(this) && play.Target is { IsDead: false } target)
                    await PowerCmd.Apply<LiJiePower>(context, target, discarded.Length, player, this, false);
                if (discarded.Length == times && WeiCaoCaoDesignRules.Alive(this))
                    await CreatureCmd.Damage(context, player, 6, ValueProp.Unpowered, null, null);
            }
            await WeiCaoCaoDesignRules.Convert<WeiCaoCaoMouGuoJiaXianMouYang>(context, this);
        }
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_GUO_JIA_XIAN_MOU_YANG")]
public sealed class WeiCaoCaoMouGuoJiaXianMouYang:WeiCaoCaoXianMou{public WeiCaoCaoMouGuoJiaXianMouYang():base(false){}}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_GUO_JIA_XIAN_MOU_YIN")]
public sealed class WeiCaoCaoMouGuoJiaXianMouYin:WeiCaoCaoXianMou{public WeiCaoCaoMouGuoJiaXianMouYin():base(true){}}

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_SI_MA_SHI_SAN_SHI")]
public sealed class WeiCaoCaoMouSiMaShiSanShi:WeiCaoCaoDesignCard
{
    [SavedProperty]public bool Started{get;set;}
    public WeiCaoCaoMouSiMaShiSanShi():base(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self){}
    public override int MaxUpgradeLevel=>0;
    public override HashSet<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust,CardKeyword.Innate];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>base.AdditionalHoverTips.Concat([
        HoverTipFactory.FromCard<WeiCaoCaoMouSiMaShiChenLue>()]);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var all=new[]{PileType.Hand,PileType.Draw,PileType.Discard}.SelectMany(t=>t.GetPile(Owner).Cards).Where(c=>c!=this && c.Type is CardType.Attack or CardType.Skill or CardType.Power && WeiCaoCaoDesignRules.CanEnchant(c,ModelDb.Enchantment<WeiCaoCaoSiShi>())).ToArray();
        foreach(var type in new[]{CardType.Attack,CardType.Skill,CardType.Power})
        {
            var selected=await WeiCaoCaoDesignRules.Choose(context,this,all.Where(c=>c.Type==type),2,2);
            foreach(var card in selected){if(card.Enchantment!=null)CardCmd.ClearEnchantment(card);CardCmd.Enchant(ModelDb.Enchantment<WeiCaoCaoSiShi>().ToMutable(),card,1);}
        }
        await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard<WeiCaoCaoMouSiMaShiChenLue>(Owner),PileType.Draw,Owner,CardPilePosition.Random);
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c,Player p)
    {
        if(Started||p!=Owner||Pile?.Type!=PileType.Hand||CombatState.RoundNumber!=1||!WeiCaoCaoDesignRules.Alive(this))return;
        Started=true;await CardCmd.AutoPlay(c,this,null,AutoPlayType.Default,false,false);
    }
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_SI_MA_SHI_CHEN_LUE")]
public sealed class WeiCaoCaoMouSiMaShiChenLue:WeiCaoCaoDesignCard
{
    public override bool CanBeGeneratedInCombat=>false;
    public WeiCaoCaoMouSiMaShiChenLue():base(0,CardType.Skill,CardRarity.Token,TargetType.Self,false){}
    public override int MaxUpgradeLevel=>0;
    public override HashSet<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var marked=new[]{PileType.Draw,PileType.Discard}.SelectMany(t=>t.GetPile(Owner).Cards).Where(c=>c.Enchantment is WeiCaoCaoSiShi).ToArray();
        foreach(var card in marked)
        {
            if(!WeiCaoCaoDesignRules.Alive(this))break;
            if(card.Pile?.Type is not(PileType.Draw or PileType.Discard))continue;
            card.EnergyCost.SetThisCombat(0);CardCmd.ApplyKeyword(card,CardKeyword.Exhaust);CardCmd.ApplyKeyword(card,CardKeyword.Ethereal);
            await CardPileCmd.Add(card,PileType.Hand,CardPilePosition.Bottom,this);
        }
    }
}
