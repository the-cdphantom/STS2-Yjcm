using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using MegaCrit.Sts2.Core.ValueProps;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_XING_CAO_REN_DI_FENG")]
public sealed class WeiCaoCaoXingCaoRenDiFeng:WeiCaoCaoDesignCard
{
    public WeiCaoCaoXingCaoRenDiFeng():base(0,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy, hasJunHeEnhancement: true){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(6,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        bool military=await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this);
        await WeiDongZhuoQunEffects.Attack(this,play,DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
        if(military && WeiCaoCaoDesignRules.Alive(this))await PowerCmd.Apply<ArtifactPower>(context,Owner.Creature,1,Owner.Creature,this,false);
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_XING_ZHANG_CHUN_HUA_LIANG_YAN")]
public sealed class WeiCaoCaoXingZhangChunHuaLiangYan:WeiCaoCaoDesignCard
{
    public WeiCaoCaoXingZhangChunHuaLiangYan():base(1,CardType.Skill,CardRarity.Common,TargetType.AnyEnemy, hasJunHeEnhancement: true){}
    public override HashSet<CardKeyword> CanonicalKeywords=>[CardKeyword.Sly];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Count",2)];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        int n=(int)DynamicVars["Count"].BaseValue;
        await PowerCmd.Apply<StrengthPower>(context,Owner.Creature,n,Owner.Creature,this,false);
        if(play.Target is {IsDead:false} target && await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this))
            await PowerCmd.Apply<VulnerablePower>(context,target,n,Owner.Creature,this,false);
    }
    protected override void OnUpgrade()=>DynamicVars["Count"].UpgradeValueBy(1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_YAN_ROU_CHOU_TAO")]
public sealed class WeiCaoCaoYanRouChouTao:WeiCaoCaoDesignCard
{
    public WeiCaoCaoYanRouChouTao():base(1,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(7,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        await WeiDongZhuoQunEffects.Attack(this,play,DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
        var chosen=(await WeiCaoCaoDesignRules.Hand(context,this,0,1,prompt:"discard",forDiscard:true)).FirstOrDefault();
        if(chosen?.Pile?.Type!=PileType.Hand)return;
        await CardCmd.Discard(context,chosen);
        if(WeiCaoCaoDesignRules.Alive(this))await PlayerCmd.GainEnergy(1,Owner);
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_WU_ZHI_WEI_TI")]
public sealed class WeiCaoCaoWuZhiWeiTi:WeiCaoCaoDesignCard
{
    public WeiCaoCaoWuZhiWeiTi():base(2,CardType.Skill,CardRarity.Uncommon,TargetType.Self, hasJunHeEnhancement: true){}
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        CardModel[] options=[CombatState!.CreateCard<WeiCaoCaoChoiceAttack>(Owner),CombatState.CreateCard<WeiCaoCaoChoiceSkill>(Owner)];
        var selected=(await WeiCaoCaoDesignRules.Choose(context,this,options,1,1)).FirstOrDefault();
        if(selected==null)return;
        bool attack=selected is WeiCaoCaoChoiceAttack;
        var discard=PileType.Hand.GetPile(Owner).Cards.Where(c=>c.Type==(attack?CardType.Attack:CardType.Skill)).ToArray();
        int n=0;
        foreach(var c in discard)if(c.Pile?.Type==PileType.Hand){await CardCmd.Discard(context,c);n++;}
        if(!WeiCaoCaoDesignRules.Alive(this))return;
        if(await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this))n++;
        if(n<=0)return;
        if(attack)await PowerCmd.Apply<StrengthPower>(context,Owner.Creature,n,Owner.Creature,this,false);
        else await PowerCmd.Apply<DexterityPower>(context,Owner.Creature,n,Owner.Creature,this,false);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_SP_GUAN_YU_NU_CHEN")]
public sealed class WeiCaoCaoSpGuanYuNuChen:WeiCaoCaoDesignCard
{
    public WeiCaoCaoSpGuanYuNuChen():base(2,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy, hasJunHeEnhancement: true){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(10,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var representative=(await WeiCaoCaoDesignRules.Hand(context,this,1,1,prompt:"discard_type",forDiscard:true)).FirstOrDefault();
        if(representative==null)return;
        var discard=PileType.Hand.GetPile(Owner).Cards.Where(c=>c.Type==representative.Type).ToArray();
        bool military=await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this);
        int n=0;
        foreach(var c in discard)if(c.Pile?.Type==PileType.Hand){await CardCmd.Discard(context,c);n++;}
        if(n>0 && WeiCaoCaoDesignRules.Alive(this) && play.Target is {IsDead:false} target)
            await WeiDongZhuoQunEffects.Attack(this,play,n*(military?15:10)).Targeting(target).Execute(context);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_BIAN_XI_DUN_XI")]
public sealed class WeiCaoCaoBianXiDunXi:WeiCaoCaoDesignCard
{
    public WeiCaoCaoBianXiDunXi():base(1,CardType.Attack,CardRarity.Common,WeiCaoCaoTargetTypes.AnyCharacter){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(6,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        await WeiDongZhuoQunEffects.Attack(this,play,DynamicVars.Damage.BaseValue).Targeting(play.Target!).Execute(context);
        if(play.Target is {IsDead:false,Monster:{ } monster})
            await WeiCaoCaoIntentRules.Reroll(context,monster);
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_SHEN_DIAN_WEI_CHE_XIE")]
public sealed class WeiCaoCaoShenDianWeiCheXie:WeiCaoCaoDesignCard
{
    public WeiCaoCaoShenDianWeiCheXie():base(2,CardType.Skill,CardRarity.Rare,TargetType.Self, hasJunHeEnhancement: true){}
    protected override HashSet<CardTag> CanonicalTags=>[YjcmCardTags.WuJiang,YjcmCardTags.ShenJiang];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        bool military=await WeiCaoCaoJunHePower.TryConsume(context,Owner.Creature,1,this);
        var pool=WeiCaoCaoDesignRules.Pool(this,CardType.Attack).ToArray();
        if(pool.Length==0)return;
        var options=CardFactory.GetDistinctForCombat(Owner,pool,Math.Min(pool.Length,military?5:3),Owner.RunState.Rng.CombatCardGeneration);
        var chosen=await WeiCaoCaoDesignRules.Choose(context,this,options,2,2);
        foreach(var c in chosen)
        {c.SetToFreeThisTurn();await CardPileCmd.AddGeneratedCardToCombat(c,PileType.Hand,Owner,CardPilePosition.Bottom);}
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_MOU_HUANG_QUAN_QIAO_DUI")]
public sealed class WeiCaoCaoMouHuangQuanQiaoDui:WeiCaoCaoDesignCard
{
    public WeiCaoCaoMouHuangQuanQiaoDui():base(2,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy){}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(8,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var target=play.Target!;
        await PowerCmd.Apply<StrengthPower>(context,target,2,Owner.Creature,this,false);
        int n=target.Powers.OfType<StrengthPower>().FirstOrDefault()?.Amount??0;
        await WeiDongZhuoQunEffects.Attack(this,play,DynamicVars.Damage.BaseValue).Targeting(target).WithHitCount((int)Math.Min(int.MaxValue,1L+Math.Max(0,n))).Execute(context);
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(4);
}
