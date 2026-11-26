using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_HU_BAN_CHONG_YI")]
public sealed class WeiCaoCaoHuBanChongYi:WeiCaoCaoDesignCard
{
    public WeiCaoCaoHuBanChongYi():base(2,CardType.Power,CardRarity.Uncommon,TargetType.Self, hasJunHeEnhancement: true){}
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)=>await PowerCmd.Apply<WeiCaoCaoChongYiPower>(c,Owner.Creature,1,Owner.Creature,this,false);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_XING_CAO_REN_SU_JUN")]
public sealed class WeiCaoCaoXingCaoRenSuJun:WeiCaoCaoDesignCard
{
    public WeiCaoCaoXingCaoRenSuJun():base(1,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)=>await PowerCmd.Apply<WeiCaoCaoSuJunPower>(c,Owner.Creature,1,Owner.Creature,this,false);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_ZHONG_YU_ZHI_DUI")]
public sealed class WeiCaoCaoZhongYuZhiDui:WeiCaoCaoDesignCard
{
    public WeiCaoCaoZhongYuZhiDui():base(2,CardType.Power,CardRarity.Uncommon,TargetType.Self){}
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)=>await PowerCmd.Apply<WeiCaoCaoZhiDuiPower>(c,Owner.Creature,1,Owner.Creature,this,false);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[RegisterCard(typeof(WeiCaoCaoCardPool),FullPublicEntry="YJCM_CARD_WEI_CAO_CAO_SU_YUE_GONG_TU")]
public sealed class WeiCaoCaoSuYueGongTu:WeiCaoCaoDesignCard
{
    public WeiCaoCaoSuYueGongTu():base(2,CardType.Power,CardRarity.Uncommon,TargetType.Self, hasJunHeEnhancement: true){}
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
    protected override async Task OnPlay(PlayerChoiceContext c,CardPlay p)
    {
        var first=await WeiCaoCaoDesignRules.ChooseType(c,this,"gong_tu_first");
        if(first==null)return;
        var second=await WeiCaoCaoDesignRules.ChooseType(c,this,"gong_tu_second");
        if(second==null)return;
        var power=await PowerCmd.Apply<WeiCaoCaoGongTuPower>(c,Owner.Creature,1,Owner.Creature,this,false);
        if(power==null)return;
        power.First=(int)first.Value;power.Second=(int)second.Value;power.Previous=-1;
        // Military enhancement belongs to the installed ability, not each match.
        // Replaying an unenhanced copy must not downgrade an existing enhancement.
        if(!power.Enhanced && await WeiCaoCaoJunHePower.TryConsume(c,Owner.Creature,1,this))power.Enhanced=true;
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerIntentHandler), "BeforeActionPausedForPlayerChoice")]
internal static class WeiCaoCaoGongTuChoiceIntentPatch
{
    private static void Prefix(GameAction action, out PlayerChoiceContext? __state)
    {
        var context = action switch
        {
            PlayCardAction play => play.PlayerChoiceContext,
            GenericHookGameAction hook => hook.ChoiceContext,
            _ => null
        };
        __state = null;
        if (context?.LastInvolvedModel is not WeiCaoCaoSuYueGongTu) return;

        // The power's play animation owns its node. Give the remote intent UI
        // a preview so it does not try to requeue that node after selection.
        __state = context;
        context.PushModel(ModelDb.Card<WeiCaoCaoSuYueGongTu>());
    }

    private static void Finalizer(PlayerChoiceContext? __state) =>
        __state?.PopModel(ModelDb.Card<WeiCaoCaoSuYueGongTu>());
}
