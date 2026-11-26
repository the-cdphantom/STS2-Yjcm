using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace Yjcm;

/// <summary>
/// 武诸葛亮卡池：TypeListCardPoolModel，通过 CardTypes 显式列出所有卡牌
/// </summary>
public sealed class WuZhugeliangCardPool : TypeListCardPoolModel
{
    public override string Title => "wuzhugeliang";
    public override string EnergyColorName => "yjcm_qimen";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => new("#4A90D9");
    public override Color EnergyOutlineColor => new("365D8A");
    public override bool IsColorless => false;

    // 显式列出所有卡牌类型，绕过 ModHelper.AddModelToPool 的 isFrozen 问题
    protected override IEnumerable<Type> CardTypes => new Type[]
    {
        // 基础牌
        typeof(Sha), typeof(Shan), typeof(Jiu),
        // 角色起始牌
        typeof(ZhiZhe), typeof(LiuBeiJiJiang),
        // 蜀国武将
        typeof(HuangWudieShuangRui), typeof(ZhugeJunGuMai), typeof(ZhugeJunGengDu),
        typeof(WuBanYouZhan), typeof(MouGuanPingWuWei),
        typeof(PangFengYiYiTong), typeof(PangFengYiPeiNiang),
        typeof(PangHongGangJian),
        typeof(WoLongZhuGeBaZhen), typeof(MouPangTongHongCe),
        typeof(MouPangTongYinMouYang), typeof(MouPangTongYinMouYin),
        typeof(WuGuanYuWuYou),
        // 转换技
        typeof(MouJiangWeiJueMouYang), typeof(MouJiangWeiJueMouYin),
        typeof(PangHongPingZhiYang), typeof(PangHongPingZhiYin),
        typeof(ShenPangTongFengLiaoYang), typeof(ShenPangTongFengLiaoYin),
        // 神将
        typeof(ShenZhangFeiShenCai), typeof(ShenZhangFeiXunShi),
        typeof(ShenHuangZhongLieCang), typeof(ShenHuangZhongZhanJue),
        typeof(ShenJiangWeiTianRen), typeof(ShenJiangWeiPingXiang),
        typeof(ShenZhaoYunLongHun),
        // 其他
        typeof(ShangYu), typeof(CaiXia), typeof(TunChu), typeof(WeiLiuBeiRenGou),
        typeof(LeBuSiShu), typeof(JinCuiXingTai), typeof(WuZhongShengYou), typeof(Tao), typeof(LiuBaZhuBi), typeof(LiuBaLiuZhuan), typeof(RenWangDun), typeof(BaGuaZhen), typeof(BaiYinShiZi), typeof(HuangWudieShouXing), typeof(HuangWudieShaXue), typeof(ChenShiQingBei), typeof(QingLongYanYueDao), typeof(GuanShiFu), typeof(ZhuQueYuShan), typeof(ZhuGeLianNu), typeof(YiZhenQingMeng), typeof(BeiFa),
        // Token
        typeof(WuSeSha), typeof(HuoGong), typeof(JueDou),
        typeof(TieSuoLianHuan), typeof(NanManRuQin), typeof(WanJianQiFa),
        // 坐骑
        typeof(DiLu), typeof(ChiTu), typeof(JueYing), typeof(ZiXing),
        // 威张星彩
        typeof(WeiZhangXingCaiXianKuang), typeof(WeiZhangXingCaiHuangNu),
        // 新装备
        typeof(HanBingJian), typeof(ZhangBaSheMao), typeof(QingGangJian),
        // 卧龙诸葛
        typeof(WoLongZhuGeLiangHuoJi), typeof(WoLongZhuGeLiangKanPo),
        // 乐诸葛果
        typeof(LeZhuGeGuoChengYan),
        // 联动牌（仅武诸葛亮）
        typeof(SunQianShuiHuoWuDi), typeof(XinSanGuoShuiHuoJianRong),
    };
}
