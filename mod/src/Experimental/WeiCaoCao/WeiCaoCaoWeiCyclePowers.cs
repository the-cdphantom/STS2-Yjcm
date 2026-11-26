using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

#nullable enable
namespace Yjcm;

[RegisterPower]
public sealed class WeiCaoCaoChongYiPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/chong_yi_buff.png";
    private bool _busy;
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(_busy||play.Card.Owner.Creature!=Owner||!play.IsFirstInSeries||play.Card.Type!=CardType.Attack||Owner.IsDead)return;
        var state=Owner.CombatState;
        int count=CombatManager.Instance.History.CardPlaysStarted.Count(e=>e.HappenedThisTurn(state)&&e.CardPlay.Card.Owner.Creature==Owner&&e.CardPlay.IsFirstInSeries);
        if(count!=1)return;
        _busy=true;
        try{await WeiCaoCaoDesignRules.Draw(context,ChoiceSource,2);if(!Owner.IsDead)await PlayerCmd.GainEnergy(1,Owner.Player!);}
        finally{_busy=false;}
    }
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side!=CombatSide.Player||!participants.Contains(Owner)||Owner.IsDead)return;
        var last=CombatManager.Instance.History.CardPlaysStarted.LastOrDefault(e=>e.HappenedThisTurn(Owner.CombatState)&&e.CardPlay.Card.Owner.Creature==Owner&&e.CardPlay.IsFirstInSeries)?.CardPlay.Card;
        if(last?.Type!=CardType.Attack)return;
        var physical=WeiCaoCaoFuPlay.Physical(last);
        if(physical.Pile?.Type is not(PileType.Discard or PileType.Hand))return;
        if(!await WeiCaoCaoJunHePower.TryConsume(context,Owner,1,ChoiceSource))return;
        if(physical.Pile?.Type==PileType.Discard)await CardPileCmd.Add(physical,PileType.Hand,CardPilePosition.Bottom);
        physical.GiveSingleTurnRetain();
        await WeiCaoCaoJunHePower.Gain(context,Owner,1);
    }
}
[RegisterPower]
public sealed class WeiCaoCaoSuJunPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/su_jun_buff.png";
    private bool _busy;
    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(_busy||play.Card.Owner.Creature!=Owner||!play.IsFirstInSeries||play.Card is WeiCaoCaoXingCaoRenSuJun||Owner.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        var hand=PileType.Hand.GetPile(Owner.Player!).Cards;
        if(hand.Count(c=>c.Type==CardType.Attack)!=hand.Count(c=>c.Type!=CardType.Attack))return;
        _busy=true;
        try{await WeiCaoCaoDesignRules.Draw(context,ChoiceSource,2);}
        finally{_busy=false;}
    }
}
[RegisterPower]
public sealed class WeiCaoCaoZhiDuiPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/zhi_dui_buff.png";
    [SavedProperty]public int PreviousType{get;set;}=-1;
    [SavedProperty]public int PreviousLength{get;set;}
    [SavedProperty]public bool Disabled{get;set;}
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (player.Creature == Owner) Disabled = false;
        return Task.CompletedTask;
    }
    internal static int Compare(int oldType,int oldLength,int type,int length)=>oldType==type && oldLength==length?1:oldType!=type&&oldLength!=length?-1:0;
    internal bool Matches(CardModel card) => !Disabled && PreviousType >= 0 &&
        Compare(PreviousType, PreviousLength, (int)card.Type, WeiCaoCaoDesignRules.NameLength(card)) == 1;

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsAutoPlay || !play.IsFirstInSeries || play.Card.Owner.Creature != Owner ||
            play.Card is WeiCaoCaoZhongYuZhiDui || Owner.IsDead)
            return Task.CompletedTask;

        int type = (int)play.Card.Type;
        int length = WeiCaoCaoDesignRules.NameLength(play.Card);
        if (PreviousType >= 0 && Compare(PreviousType, PreviousLength, type, length) < 0)
            Disabled = true;
        PreviousType = type;
        PreviousLength = length;
        return Task.CompletedTask;
    }
}
[RegisterPower]
public sealed class WeiCaoCaoGongTuPower:WeiCaoCaoDesignPower
{
    public override string CustomIconPath => "res://Experimental/WeiCaoCao/images/gong_tu_buff.png";
    [SavedProperty]public int First{get;set;}=-1;
    [SavedProperty]public int Second{get;set;}=-1;
    [SavedProperty]public int Previous{get;set;}=-1;
    [SavedProperty]public bool Enhanced{get;set;}
    private bool _busy;
    public override LocString Description
    {
        get
        {
            if (First < 0 || Second < 0) return base.Description;
            var description = new LocString("powers", Id.Entry + ".active_description");
            description.Add("First", TypeName((CardType)First));
            description.Add("Second", TypeName((CardType)Second));
            return description;
        }
    }

    private static string TypeName(CardType type) =>
        new LocString("cards", $"YJCM_CARD_WEI_CAO_CAO_CHOICE_{type.ToString().ToUpperInvariant()}.title").GetFormattedText();

    public override async Task AfterCardPlayed(PlayerChoiceContext context,CardPlay play)
    {
        if(_busy||play.Card.Owner.Creature!=Owner||!play.IsFirstInSeries||play.Card is WeiCaoCaoSuYueGongTu||Owner.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        int current=(int)play.Card.Type;
        bool matched=Previous==First&&current==Second;
        Previous=current;
        if(!matched)return;
        _busy=true;
        try
        {
            await WeiCaoCaoDesignRules.Draw(context,ChoiceSource,2);
            if(Enhanced && !Owner.IsDead)await PlayerCmd.GainEnergy(1,Owner.Player!);
        }
        finally{_busy=false;}
    }
}

internal static class WeiCaoCaoIntentRules
{
    internal static Task Reroll(PlayerChoiceContext context,MonsterModel monster)
    {
        if(monster.Creature.IsDead||monster.MoveStateMachine==null||!monster.NextMove.CanTransitionAway)return Task.CompletedTask;
        // RollMove intentionally keeps the opening intent until a move is consumed.
        // DunXi consumes that intent without executing its action, including before
        // the first enemy turn (and during an opening WuMei extra player turn).
        monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
        monster.RollMove(monster.CombatState!.PlayerCreatures);
        monster.SetMoveImmediate(monster.NextMove,true);
        return Task.CompletedTask;
    }
}
