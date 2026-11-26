using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;

#nullable enable
namespace Yjcm;

// Use the same actual-play history boundary as the card effects. This also
// handles automatic plays and Fu's chosen card without replaying on each hit.
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class WeiCaoCaoCardVoices
{
    private static void Postfix(CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries) return;
        if (cardPlay.Card is WeiCaoCaoQuanMou)
        {
            MouSimaYiSfxPlayer.PlayQuanMou(cardPlay.Card is WeiCaoCaoMouSiMaYiQuanMouYang);
            return;
        }
        string? voice = cardPlay.Card switch
        {
            WeiCaoCaoMouXunYuBiZuo => "bi_zuo",
            WeiCaoCaoWeiCaoPiDianLun => "dian_lun",
            WeiCaoCaoMouJiangJiYingShi => "ying_shi",
            WeiCaoCaoMouChengYuShiZha => "shi_zha",
            WeiCaoCaoShiMou => cardPlay.Card is WeiCaoCaoMouXunYuShiMouYin ? "shi_mou_yin" : "shi_mou_yang",
            WeiCaoCaoXianMou => cardPlay.Card is WeiCaoCaoMouGuoJiaXianMouYin ? "xian_mou_yin" : "xian_mou_yang",
            WeiCaoCaoMouHuangQuanQiaoDui => "qiao_dui",
            WeiCaoCaoMouSiMaShiSanShi => "san_shi",
            WeiCaoCaoMouSiMaShiChenLue => "chen_lue",
            WeiCaoCaoHuBanChongYi => "chong_yi",
            WeiCaoCaoXingCaoRenSuJun => "su_jun",
            WeiCaoCaoXingCaoRenDiFeng => "di_feng",
            WeiCaoCaoZhongYuZhiDui => "zhi_dui",
            WeiCaoCaoXingZhangChunHuaLiangYan => "liang_yan",
            WeiCaoCaoSpGuanYuNuChen => "nu_chen",
            WeiCaoCaoBianXiDunXi => "dun_xi",
            WeiCaoCaoShenDianWeiCheXie => "che_xie",
            WeiCaoCaoSuYueGongTu => "gong_tu",
            WeiCaoCaoMouDengAiZhouXi => "zhou_xi",
            WeiCaoCaoXiaHouEnShiWu => "shi_wu",
            WeiCaoCaoZhaoYanBingJi => "bing_ji",
            WeiCaoCaoYueChenPoRui => "po_rui",
            WeiCaoCaoWenQinGuangAo => "guang_ao",
            WeiCaoCaoWeiCaoZhangNingPao => "ning_pao",
            WeiCaoCaoWeiCaoZhangKuiPan => "kui_pan",
            WeiCaoCaoGuiXinXingTai => "gui_xin_xing_tai",
            WeiCaoCaoXieTianZi => "xie_tian_zi",
            WeiCaoCaoXiaHouXuanBoXuan => "bo_xuan",
            WeiCaoCaoYanRouChouTao => "chou_tao",
            WeiCaoCaoSunLiKangLi => "kang_li",
            WeiCaoCaoWuZhiWeiTi => "wei_ti",
            WeiCaoCaoZhuLingZhanYi => "zhan_yi",
            WeiCaoCaoZhouXuanWuMei => "wu_mei",
            WeiCaoCaoCheZhouSheFu => "she_fu",
            WeiCaoCaoYangHuMingFa => "ming_fa",
            WeiCaoCaoShenDengAiTuoYu => "tuo_yu",
            _ => null
        };
        if (voice != null) WeiCaoCaoAudio.Play(voice);
    }
}
