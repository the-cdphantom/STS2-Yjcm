using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable
namespace Yjcm;

internal static class WeiCaoCaoDesignRegistry
{
    internal static IEnumerable<Type> CardTypes=>typeof(WeiCaoCaoDesignCard).Assembly.GetTypes()
        .Where(t=>!t.IsAbstract && t.IsSubclassOf(typeof(WeiCaoCaoDesignCard))).OrderBy(t=>t.FullName,StringComparer.Ordinal);
}

public abstract class WeiCaoCaoDesignCard(int cost, CardType type, CardRarity rarity, TargetType target,
    bool showInLibrary = true, bool hasJunHeEnhancement = false)
    : WeiCaoCaoCard(cost,type,rarity,target,showInLibrary)
{
    public override string CustomPortraitPath => "res://Experimental/WeiCaoCao/images/" + (this switch
    {
        WeiCaoCaoWeiCaoPiDianLun => "dian_lun",
        WeiCaoCaoHuBanChongYi => "chong_yi",
        WeiCaoCaoXingCaoRenSuJun => "su_jun",
        WeiCaoCaoXingCaoRenDiFeng => "di_feng",
        WeiCaoCaoZhongYuZhiDui => "zhi_dui",
        WeiCaoCaoMouJiangJiYingShi => "ying_shi",
        WeiCaoCaoMouChengYuShiZha => "shi_zha",
        WeiCaoCaoXingZhangChunHuaLiangYan => "liang_yan",
        WeiCaoCaoMouXunYuShiMouYang => "shi_mou_yang",
        WeiCaoCaoMouXunYuBiZuo => "shi_mou_yang",
        WeiCaoCaoMouXunYuShiMouYin => "shi_mou_yin",
        WeiCaoCaoMouSiMaYiQuanMouYang => "quan_mou_yang",
        WeiCaoCaoMouSiMaYiQuanMouYin => "quan_mou_yin",
        WeiCaoCaoMouGuoJiaXianMouYang => "xian_mou_yang",
        WeiCaoCaoMouGuoJiaXianMouYin => "xian_mou_yin",
        WeiCaoCaoMouSiMaShiSanShi => "san_shi",
        WeiCaoCaoMouSiMaShiChenLue => "chen_lue",
        WeiCaoCaoBianXiDunXi => "dun_xi",
        WeiCaoCaoCheZhouSheFu => "she_fu",
        WeiCaoCaoMouHuangQuanQiaoDui => "qiao_dui",
        WeiCaoCaoShenDengAiTuoYu => "tuo_yu",
        WeiCaoCaoShenDianWeiCheXie => "che_xie",
        WeiCaoCaoSunLiKangLi => "kang_li",
        WeiCaoCaoWuZhiWeiTi => "wei_ti",
        WeiCaoCaoXiaHouXuanBoXuan => "bo_xuan",
        WeiCaoCaoYanRouChouTao => "chou_tao",
        WeiCaoCaoYangHuMingFa => "ming_fa",
        WeiCaoCaoZhouXuanWuMei => "wu_mei",
        WeiCaoCaoZhuLingZhanYi => "zhan_yi",
        WeiCaoCaoSpGuanYuNuChen => "nu_chen",
        WeiCaoCaoSuYueGongTu => "gong_tu",
        WeiCaoCaoMouDengAiZhouXi => "zhou_xi",
        WeiCaoCaoXiaHouEnShiWu => "shi_wu",
        WeiCaoCaoZhaoYanBingJi => "bing_ji",
        WeiCaoCaoYueChenPoRui => "po_rui",
        WeiCaoCaoWenQinGuangAo => "guang_ao",
        WeiCaoCaoWeiCaoZhangNingPao => "ning_pao",
        WeiCaoCaoWeiCaoZhangKuiPan => "kui_pan",
        _ => "duo_yue"
    }) + ".png";
    protected override HashSet<CardTag> CanonicalTags => [YjcmCardTags.WuJiang,YjcmCardTags.Wei];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return WuJiangHoverTip.ForCard(CanonicalTags);
            if (hasJunHeEnhancement)
                yield return HoverTipFactory.FromPower<WeiCaoCaoJunHePower>();
        }
    }
}

public abstract class WeiCaoCaoDesignPower : ModPowerTemplate
{
    private CardModel? _choiceSource;
    protected CardModel ChoiceSource => _choiceSource ??= Owner.CombatState!.CreateCard<WeiCaoCaoJiu>(Owner.Player!);
    protected override void DeepCloneFields(){base.DeepCloneFields();_choiceSource=null;}
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/jun_he_buff.png";
    public override string CustomBigIconPath => CustomIconPath;
}

internal static class WeiCaoCaoDesignRules
{
    internal static bool Alive(CardModel source) => source.Owner?.Creature.CombatState != null &&
        !source.Owner.Creature.IsDead && !CombatManager.Instance.IsOverOrEnding;
    internal static async Task<IReadOnlyList<CardModel>> Choose(PlayerChoiceContext context, CardModel source,
        IEnumerable<CardModel> options, int min, int max, string prompt="select")
    {
        var list=options.Distinct().ToList();
        max=Math.Min(max,list.Count); min=Math.Min(min,max);
        if(max<=0 || !Alive(source)) return [];
        return (await CardSelectCmd.FromSimpleGrid(context,list,source.Owner,
            new CardSelectorPrefs(new LocString("cards","YJCM_CARD_WEI_CAO_CAO_DESIGN."+prompt),min,max)
            { RequireManualConfirmation=true })).ToList();
    }
    internal static async Task<IReadOnlyList<CardModel>> Hand(PlayerChoiceContext context, CardModel source,
        int min,int max,Func<CardModel,bool>? predicate=null,string prompt="select",bool forDiscard=false,
        bool releaseSelectionOnConfirm=false)
    {
        bool Eligible(CardModel c)=>c!=source && (predicate?.Invoke(c)??true);
        int count=PileType.Hand.GetPile(source.Owner).Cards.Count(Eligible);
        max=Math.Min(max,count);min=Math.Min(min,max);
        if(max<=0 || !Alive(source)) return [];
        var prefs=new CardSelectorPrefs(new LocString("cards","YJCM_CARD_WEI_CAO_CAO_DESIGN."+prompt),min,max)
            {RequireManualConfirmation=true};
        var visualSource=releaseSelectionOnConfirm?null:source;
        return (await (forDiscard
            ? CardSelectCmd.FromHandForDiscard(context,source.Owner,prefs,Eligible,visualSource!)
            : CardSelectCmd.FromHand(context,source.Owner,prefs,Eligible,visualSource))).ToList();
    }
    internal static async Task<IReadOnlyList<CardModel>> Draw(PlayerChoiceContext context, CardModel source,
        int count,bool freeThisTurn=false,bool freeUntilPlayed=false)
    {
        List<CardModel> result=[];
        for(int i=0;i<count && Alive(source);i++)
        {
            var c=await CardPileCmd.Draw(context,source.Owner);
            if(c==null) break;
            if(freeThisTurn)c.SetToFreeThisTurn();
            else if(freeUntilPlayed)WeiCaoCaoFuPlay.MakeFreeUntilPlayed(c);
            result.Add(c);
        }
        return result;
    }
    internal static async Task Play(PlayerChoiceContext context,CardModel card,Creature? fallback=null)
    {
        if(!Alive(card))return;
        var (confirmed,target)=await WeiCaoCaoFuPlay.SelectTarget(context,card,fallback);
        if(!confirmed || !Alive(card))return;
        await CardCmd.AutoPlay(context,card,target,AutoPlayType.Default,false,false);
    }
    internal static IEnumerable<CardModel> Pool(CardModel source,CardType? type=null) =>
        ModelDb.CardPool<WeiCaoCaoCardPool>().GetUnlockedCards(source.Owner.UnlockState,source.Owner.RunState.CardMultiplayerConstraint)
            .Where(c=>c.CanBeGeneratedInCombat && c.ShouldShowInCardLibrary && c.Rarity!=CardRarity.Token &&
                c.Type is CardType.Attack or CardType.Skill or CardType.Power && (type==null||c.Type==type) && c.GetType()!=source.GetType())
            .DistinctBy(c=>c.Id).OrderBy(c=>c.Id.Entry,StringComparer.Ordinal);
    internal static int NameLength(CardModel card)=>WeiCaoCaoDesignNames.Length(card.Id.Entry);
    internal static bool CanEnchant(CardModel card,EnchantmentModel enchantment)
    {
        if(card.IsCanonical)return false;
        if(card.Enchantment==null)return enchantment.CanEnchant(card);
        var probe=(CardModel)card.ClonePreservingMutability();
        probe.ClearEnchantmentInternal();
        return enchantment.CanEnchant(probe);
    }
    internal static async Task<CardType?> ChooseType(PlayerChoiceContext context,CardModel source,string prompt="select")
    {
        // Power-triggered choices use a source card that is not in a combat pile.
        var owner=source.Owner;
        if(owner?.Creature.CombatState is not { } state || !Alive(source)) return null;
        CardModel[] choices=[state.CreateCard<WeiCaoCaoChoiceAttack>(owner),state.CreateCard<WeiCaoCaoChoiceSkill>(owner),state.CreateCard<WeiCaoCaoChoicePower>(owner)];
        var c=(await Choose(context,source,choices,1,1,prompt)).FirstOrDefault();
        return c switch{WeiCaoCaoChoiceAttack=>CardType.Attack,WeiCaoCaoChoiceSkill=>CardType.Skill,WeiCaoCaoChoicePower=>CardType.Power,_=>null};
    }
    internal static async Task Convert<T>(PlayerChoiceContext context,CardModel source) where T:CardModel,new()
    {
        if(!Alive(source))return;
        var next=source.CombatState.CreateCard<T>(source.Owner);
        WeiCaoCaoFuPlay.MatchSourceUpgrade(next,source);
        await CardPileCmd.AddGeneratedCardToCombat(next,PileType.Draw,source.Owner,CardPilePosition.Random);
    }
}

public abstract class WeiCaoCaoDesignChoice : WeiCaoCaoDesignCard
{
    protected WeiCaoCaoDesignChoice(CardType type=CardType.Skill, TargetType target=TargetType.None):base(0,type,CardRarity.Token,target,false){}
    public override bool CanBeGeneratedInCombat=>false;
    public override int MaxUpgradeLevel=>0;
    protected override HashSet<CardTag> CanonicalTags=>[];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>[];
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>Task.CompletedTask;
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_ATTACK")]
public sealed class WeiCaoCaoChoiceAttack():WeiCaoCaoDesignChoice(CardType.Attack);
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_SKILL")]
public sealed class WeiCaoCaoChoiceSkill:WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_POWER")]
public sealed class WeiCaoCaoChoicePower():WeiCaoCaoDesignChoice(CardType.Power);
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_DRAW")]
public sealed class WeiCaoCaoChoiceDraw:WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_ENERGY")]
public sealed class WeiCaoCaoChoiceFreePlay:WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_FENG_TIAN")]
public sealed class WeiCaoCaoChoiceFengTian:WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_QING_QU")]
public sealed class WeiCaoCaoChoiceQingQu:WeiCaoCaoDesignChoice;
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_CHOICE_JUN_SHAN")]
public sealed class WeiCaoCaoChoiceJunShan:WeiCaoCaoDesignChoice;
