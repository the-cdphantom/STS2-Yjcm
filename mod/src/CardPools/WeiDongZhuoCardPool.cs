using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

// Only independent WeiDongZhuo types. Basic and hidden Token rarities stay unchanged.
public sealed class WeiDongZhuoCardPool : TypeListCardPoolModel
{
    public override string Title => "weidongzhuo";
    public override string EnergyColorName => "yjcm_dongzhuo";
    public override string CardFrameMaterialPath => "card_frame_weidongzhuo";
    public override Color DeckEntryCardColor => new("#B98A31");
    public override Color EnergyOutlineColor => new("#5A3825");
    public override bool IsColorless => false;

    // Native AllCards appends attribute registrations to CardTypes; expose each ID once.
    // Keep this local to the test pool; never alter the original pool's enumeration.
    public override IEnumerable<CardModel> AllCards => base.AllCards.DistinctBy(card => card.Id);

    protected override IEnumerable<Type> CardTypes => new Type[]
    {
        // basic
        typeof(WeiDongZhuoSha),
        typeof(WeiDongZhuoShan),
        typeof(WeiDongZhuoJiu),
        typeof(WeiDongZhuoTao),
        // trick
        typeof(WeiDongZhuoLeBuSiShu),
        typeof(WeiDongZhuoWuZhongShengYou),
        typeof(WeiDongZhuoHuoGong),
        typeof(WeiDongZhuoJueDou),
        typeof(WeiDongZhuoTieSuoLianHuan),
        typeof(WeiDongZhuoNanManRuQin),
        typeof(WeiDongZhuoWanJianQiFa),
        // weapon
        typeof(WeiDongZhuoQingLongYanYueDao),
        typeof(WeiDongZhuoGuanShiFu),
        typeof(WeiDongZhuoZhuQueYuShan),
        typeof(WeiDongZhuoZhuGeLianNu),
        typeof(WeiDongZhuoHanBingJian),
        typeof(WeiDongZhuoZhangBaSheMao),
        typeof(WeiDongZhuoQingGangJian),
        typeof(WeiDongZhuoCiXiongShuangGuJian),
        // armor
        typeof(WeiDongZhuoRenWangDun),
        typeof(WeiDongZhuoBaGuaZhen),
        typeof(WeiDongZhuoBaiYinShiZi),
        // mount
        typeof(WeiDongZhuoDiLu),
        typeof(WeiDongZhuoChiTu),
        typeof(WeiDongZhuoJueYing),
        typeof(WeiDongZhuoZiXing),
        // general
        typeof(WeiDongZhuoShenHuangZhongLieCang),
        typeof(WeiDongZhuoShenHuangZhongZhanJue),
        typeof(WeiDongZhuoShenJiangWeiPingXiang),
        typeof(WeiDongZhuoShenJiangWeiTianRen),
        typeof(WeiDongZhuoShenMaChaoShouLi),
        typeof(WeiDongZhuoShenPangTongFengLiaoYang),
        typeof(WeiDongZhuoShenZhangFeiShenCai),
        typeof(WeiDongZhuoShenZhangFeiXunShi),
        typeof(WeiDongZhuoShenZhaoYunLongHun),
        typeof(WeiDongZhuoShenZhugeliangDaWu),
        typeof(WeiDongZhuoShenZhugeliangKuangFeng),
        typeof(WeiDongZhuoShenZhugeliangQiXing),
        // token
        typeof(WeiDongZhuoWuSeSha),
        typeof(WeiDongZhuoShenPangTongFengLiaoYin),
        typeof(WeiDongZhuoLieCangOption1),
        typeof(WeiDongZhuoLieCangOption2),
        typeof(WeiDongZhuoLieCangOption3),
        typeof(WeiDongZhuoLieCangOption4),
        typeof(WeiDongZhuoLieCangOption5),
        typeof(WeiDongZhuoZhanJueOption1),
        typeof(WeiDongZhuoZhanJueOption2),
    };
}
