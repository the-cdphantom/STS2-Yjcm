using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

internal static class WeiDesignCases
{
    private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static Assembly Mod=null!;
    private static Player Owner=null!;
    private static PlayerChoiceContext Context=null!;
    private static int Checks,Discards,Drawn,Selections,MilitarySpends,DamageCalls,BlockCalls,EnergyGained;
    private static bool UseMilitary,FreeUntilPlayed,ReleaseSelectionOnConfirm;
    private static Queue<CardModel[]> Choices=[];
    private static PowerModel InstalledGongTu=null!;
    private static Queue<CardType?> Types=[];
    private static Func<Task>? NestedDamage,NestedBlock;
    private static Action<CardModel>? AfterDiscard;
    private static readonly List<CardModel> Played=[];
    private static readonly List<int> ReplayCounts=[];
    private static int TurnNotices;
    private static bool FailPlay;
    private static readonly List<string> Voices=[];
    private static readonly List<bool> BossVoices=[];
    private static readonly Dictionary<Creature,decimal> Restored=[];
    private static ResourceInfo? ZhiDuiResources;
    private static int ZhiDuiSpent = -1;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Checks++;}
    private static MethodInfo Method(Type t,string name)=>t.GetMethod(name,F)!;
    private static Type Type(string name)=>Mod.GetType("Yjcm."+name,true)!;
    private static List<CardModel> Cards(CardPile pile)=>(List<CardModel>)typeof(CardPile).GetField("_cards",F)!.GetValue(pile)!;
    private static List<CardModel> Hand=>Cards(Owner.PlayerCombatState!.Hand);
    private static Player NewPlayer()
    {
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var pcs=(PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        typeof(Player).GetProperty("PlayerCombatState")!.SetValue(player,pcs);
        typeof(Player).GetField("<Character>k__BackingField",F)!.SetValue(player,new Ironclad());
        typeof(Player).GetField("<Deck>k__BackingField",F)!.SetValue(player,new CardPile(PileType.Deck));
        var creature=new Creature(player,100,100){CombatState=new CombatState()};
        typeof(Player).GetField("<Creature>k__BackingField",F)!.SetValue(player,creature);
        foreach(var (name,type) in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})
            typeof(PlayerCombatState).GetField("<"+name+">k__BackingField",F)!.SetValue(pcs,new CardPile(type));
        return player;
    }
    private static CardModel Card(string name)
    {
        var canonical=ModelDb.GetById<CardModel>(ModelDb.GetId(Type(name)));
        var c=canonical.ToMutable();c.Owner=Owner;return c;
    }
    private static PowerModel Power(string name,Creature? owner=null,int amount=1)
    {
        var p=ModelDb.GetById<PowerModel>(ModelDb.GetId(Type(name))).ToMutable();
        typeof(PowerModel).GetField("_owner",F)!.SetValue(p,owner??Owner.Creature);
        typeof(PowerModel).GetField("_amount",F)!.SetValue(p,amount);return p;
    }
    private static CardPlay Record(CardModel card)
    {
        var p=(CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
        foreach(var (n,v) in new (string,object?)[]{("Card",card),("Player",Owner),("Target",Owner.Creature),("PlayIndex",0),("PlayCount",1),("ResultPile",PileType.Discard)})
            typeof(CardPlay).GetProperty(n)?.SetValue(p,v);
        return p;
    }
    private static bool ZhiDuiUnaffordable(ref UnplayableReason reason, ref bool __result)
    {
        reason = UnplayableReason.EnergyCostTooHigh;
        __result = false;
        return false;
    }
    private static bool ZhiDuiEnergySpent(int amount, ref Task __result)
    {
        ZhiDuiSpent = amount;
        __result = Task.CompletedTask;
        return false;
    }
    private static bool ZhiDuiCapturePlay(ResourceInfo resources, ref Task __result)
    {
        ZhiDuiResources = resources;
        __result = Task.CompletedTask;
        return false;
    }
    private static DamageResult Damage()
    {
        var r=(DamageResult)RuntimeHelpers.GetUninitializedObject(typeof(DamageResult));
        typeof(DamageResult).GetProperty("UnblockedDamage")!.SetValue(r,6);return r;
    }
    internal static async Task Run(Assembly mod)
    {
        Mod=mod;Owner=NewPlayer();Context=new ThrowingPlayerChoiceContext();
        var db=(Dictionary<ModelId,AbstractModel>)typeof(ModelDb).GetField("_contentById",F)!.GetValue(null)!;
        foreach(var t in mod.GetTypes().Where(t=>!t.IsAbstract && t.Name.StartsWith("WeiCaoCao") && (t.IsSubclassOf(typeof(CardModel))||t.IsSubclassOf(typeof(EnchantmentModel))||t.IsSubclassOf(typeof(PowerModel)))))
            if(!db.ContainsKey(ModelDb.GetId(t)))db[ModelDb.GetId(t)]=(AbstractModel)Activator.CreateInstance(t)!;
        var rules=Type("WeiCaoCaoDesignRules");
        var progression=Type("WeiCaoCaoWeiCaoPiDianLun");
        bool Arithmetic(params int[] a)=>(bool)Method(progression,"IsProgression").Invoke(null,[a])!;
        Check(Arithmetic()&&Arithmetic(2)&&Arithmetic(4,0)&&Arithmetic(2,0,1),"Short/unsorted arithmetic progressions");
        Check(Arithmetic(1,2,3)&&Arithmetic(1,3,5)&&Arithmetic(5,1,3),"Positive common differences support sorted and unsorted selections");
        Check(!Arithmetic(1,1,1)&&!Arithmetic(0,0,0)&&!Arithmetic(1,1)&&!Arithmetic(0,0),"Zero common difference is rejected even with only two cards");
        Check(!Arithmetic(0,1,3)&&!Arithmetic(0,0,2)&&!Arithmetic(1,2,2),"Unequal steps and partial duplicates are rejected");
        var compare=Method(Type("WeiCaoCaoZhiDuiPower"),"Compare");
        Check((int)compare.Invoke(null,[1,2,1,2])! ==1,"ZhiDui equal type and length");
        Check((int)compare.Invoke(null,[1,2,2,3])! ==-1,"ZhiDui disables only if both differ");
        Check((int)compare.Invoke(null,[1,2,1,3])! ==0 && (int)compare.Invoke(null,[1,2,2,2])! ==0,"Partial matches neither trigger nor disable");
        // 2026-11-15 智对改版：匹配判定由能力自身给出，供出牌时的“摸2张/免费打出”选择使用。
        var zhiDui=Power("WeiCaoCaoZhiDuiPower");
        var zhiMatches=Method(zhiDui.GetType(),"Matches");
        var zhiSha=Card("WeiCaoCaoSha");
        Check(!(bool)zhiMatches.Invoke(zhiDui,[zhiSha])!,"ZhiDui has nothing to compare before the first played card");
        zhiDui.GetType().GetProperty("PreviousType")!.SetValue(zhiDui,(int)zhiSha.Type);
        zhiDui.GetType().GetProperty("PreviousLength")!.SetValue(zhiDui,1);
        Check((bool)zhiMatches.Invoke(zhiDui,[zhiSha])!,"ZhiDui matches a card with the same type and name length");
        zhiDui.GetType().GetProperty("PreviousLength")!.SetValue(zhiDui,9);
        Check(!(bool)zhiMatches.Invoke(zhiDui,[zhiSha])!,"ZhiDui partial match does not offer the choice");
        zhiDui.GetType().GetProperty("PreviousLength")!.SetValue(zhiDui,1);
        zhiDui.GetType().GetProperty("Disabled")!.SetValue(zhiDui,true);
        Check(!(bool)zhiMatches.Invoke(zhiDui,[zhiSha])!,"Disabled ZhiDui offers no choice this turn");
        await zhiDui.AfterPlayerTurnStartEarly(Context, Owner);
        Check((bool)zhiMatches.Invoke(zhiDui, [zhiSha])!, "Own turn start clears the lockout and preserves the last play");
        var zhiHuJia = Card("WeiCaoCaoHuJia");
        var autoPlay = Record(zhiHuJia);
        typeof(CardPlay).GetProperty("IsAutoPlay")!.SetValue(autoPlay, true);
        await zhiDui.AfterCardPlayed(Context, autoPlay);
        var replay = Record(zhiHuJia);
        typeof(CardPlay).GetProperty("PlayIndex")!.SetValue(replay, 1);
        typeof(CardPlay).GetProperty("PlayCount")!.SetValue(replay, 2);
        await zhiDui.AfterCardPlayed(Context, replay);
        Check((bool)zhiMatches.Invoke(zhiDui, [zhiSha])!, "Autoplay and replay do not advance or break the sequence");
        await zhiDui.AfterCardPlayed(Context, Record(zhiHuJia));
        Check(!(bool)zhiMatches.Invoke(zhiDui, [zhiHuJia])!, "Different type and length prevent rewards for the rest of this turn");
        var zhiShan = Card("WeiCaoCaoShan");
        await zhiDui.AfterCardPlayed(Context, Record(zhiShan));
        await zhiDui.AfterPlayerTurnStartEarly(Context, Owner);
        Check((bool)zhiMatches.Invoke(zhiDui, [zhiShan])!, "Manual plays during lockout still establish the next turn's baseline");
        var installedPowers = (List<PowerModel>)typeof(Creature).GetField("_powers", F)!.GetValue(Owner.Creature)!;
        installedPowers.Add(zhiDui);
        var coverCost = Method(Type("WeiCaoCaoZhiDuiPlay"), "CanCoverCost");
        Check((bool)coverCost.Invoke(null, [zhiShan, UnplayableReason.EnergyCostTooHigh])! &&
            !(bool)coverCost.Invoke(null, [zhiShan, UnplayableReason.EnergyCostTooHigh | UnplayableReason.BlockedByHook])!,
            "Free play covers missing resources and preserves other play restrictions");
        var paymentProbe = new Harmony("Yjcm.tests.zhidui.payment");
        try
        {
            void PatchPayment(Type type, string target, string prefix) =>
                paymentProbe.Patch(Method(type, target), prefix: new HarmonyMethod(Method(typeof(WeiDesignCases), prefix)));
            PatchPayment(typeof(PlayerCombatState), "HasEnoughResourcesFor", nameof(ZhiDuiUnaffordable));
            PatchPayment(typeof(Hook), "AfterEnergySpent", nameof(ZhiDuiEnergySpent));
            PatchPayment(typeof(CardModel), "OnPlayWrapper", nameof(ZhiDuiCapturePlay));
            await (Task)Method(Type("WeiCaoCaoZhiDuiPlay"), "Play").Invoke(null, [Context, zhiShan, null, false])!;
            Check(ZhiDuiSpent == 0 && ZhiDuiResources is { EnergySpent: 0, StarsSpent: 0 },
                "A matching manual play with insufficient resources uses the original card wrapper at zero cost");
        }
        finally { paymentProbe.UnpatchAll(paymentProbe.Id); }
        installedPowers.Remove(zhiDui);
        Check((int)Method(rules,"NameLength").Invoke(null,[Card("WeiCaoCaoSha")])! ==1,"Stable Chinese name lengths for common cards");
        Check((int)Method(rules,"NameLength").Invoke(null,[Card("WeiCaoCaoXingCaoRenDiFeng")])! ==5,"Full Chinese title counts its general and skill, excluding punctuation");
        Check((int)Method(rules,"NameLength").Invoke(null,[Card("WeiCaoCaoSpGuanYuNuChen")])! ==4,"Latin sp prefix must not count toward Chinese name length");
        Check((int)Method(Type("WeiCaoCaoDesignNames"),"Length").Invoke(null,["STRIKE_IRONCLAD"])! ==2,"Native Chinese titles use the same deterministic character rule");
        var shefu=Card("WeiCaoCaoCheZhouSheFu");
        await shefu.AfterFlush(Context,Owner,[],[shefu]);
        Check((decimal)Type("WeiCaoCaoCheZhouSheFu").GetProperty("CurrentDamage",F)!.GetValue(shefu)! ==12,"SheFu grows once per actual retain");
        await shefu.AfterFlush(Context,NewPlayer(),[],[shefu]);
        Check((int)Type("WeiCaoCaoCheZhouSheFu").GetProperty("RetainedTurns")!.GetValue(shefu)! ==1,"Teammate retain cannot grow our card");
        shefu.UpgradeInternal();shefu.FinalizeUpgradeInternal();
        Check((decimal)Type("WeiCaoCaoCheZhouSheFu").GetProperty("CurrentDamage",F)!.GetValue(shefu)! ==14,"SheFu upgrade changes base damage only");
        var noBlock=Power("WeiCaoCaoNoBlockPower");
        Check(noBlock.ModifyBlockMultiplicative(Owner.Creature,10,ValueProp.Move,null,null)==0 && noBlock.ModifyBlockMultiplicative(NewPlayer().Creature,10,ValueProp.Move,null,null)==1,"NoBlock applies only to its owner");
        var zhan=Power("WeiCaoCaoZhanYiPower");
        Type("WeiCaoCaoZhanYiPower").GetProperty("DiscardedType")!.SetValue(zhan,(int)CardType.Skill);
        var sha=Card("WeiCaoCaoSha");
        var mult=Method(zhan.GetType(),"ModifyDamageMultiplicative");
        object?[] args=mult.GetParameters().Length==6?[Owner.Creature,10m,ValueProp.Move,Owner.Creature,sha,null]:[Owner.Creature,10m,ValueProp.Move,Owner.Creature,sha];
        Check((decimal)mult.Invoke(zhan,args)! ==2,"ZhanYi boosts the surviving attack type");
        await zhan.AfterSideTurnEnd(Context,CombatSide.Player,[Owner.Creature]);
        Check((decimal)mult.Invoke(zhan,args)! ==1,"ZhanYi expires at end of own turn");
        var modelTypes=(IEnumerable<System.Type>)Type("WeiCaoCaoDesignRegistry").GetProperty("CardTypes",F)!.GetValue(null)!;
        Check(modelTypes.Count()==49 && modelTypes.Distinct().Count()==49,"All design cards and choice tokens registered once");
        foreach(var t in modelTypes)
        {
            var card=Card(t.Name);
            Check(card.CustomPortraitSafe()!=null,"Every new card has placeholder art: "+t.Name);
            if(card.Rarity==CardRarity.Token)Check(!card.ShouldShowInCardLibrary,"Support card must stay out of discovery/library: "+t.Name);
        }
        foreach(var (name,cost,upgradeCost) in new[]{("WeiCaoCaoWeiCaoPiDianLun",2,1),("WeiCaoCaoShenDianWeiCheXie",2,1),("WeiCaoCaoZhongYuZhiDui",2,1),("WeiCaoCaoXingCaoRenSuJun",1,0),("WeiCaoCaoMouXunYuShiMouYin",1,0)})
        {
            var c=Card(name);Check(c.EnergyCost.GetResolved()==cost,"Original cost "+name);c.UpgradeInternal();c.FinalizeUpgradeInternal();Check(c.EnergyCost.GetResolved()==upgradeCost,"Upgrade cost "+name);
        }
        var bingJi=Card("WeiCaoCaoZhaoYanBingJi");
        var canPlayBingJi=bingJi.GetType().GetProperty("IsPlayable", F)!;
        Hand.AddRange([bingJi, Card("WeiCaoCaoShan"), Card("WeiCaoCaoShan")]);
        Check((bool)canPlayBingJi.GetValue(bingJi)!,"Bing Ji ignores itself when the other cards are the same type");
        Hand.Add(Card("WeiCaoCaoSha"));
        Check(!(bool)canPlayBingJi.GetValue(bingJi)!,"Bing Ji cannot be played with mixed card types");
        Hand.Clear();
        foreach(var (name, baseDamage, upgradedDamage) in new[]
        {
            ("WeiCaoCaoXiaHouEnShiWu",8,12),
            ("WeiCaoCaoZhaoYanBingJi",20,30),
            ("WeiCaoCaoWenQinGuangAo",7,11)
        })
        {
            var c=Card(name);
            Check(c.DynamicVars.Damage.IntValue==baseDamage,"New card base damage: "+name);
            c.UpgradeInternal();c.FinalizeUpgradeInternal();
            Check(c.DynamicVars.Damage.IntValue==upgradedDamage,"New card upgraded damage: "+name);
        }
        Check(Card("WeiCaoCaoYangHuMingFa").EnergyCost.CostsX,"MingFa is a native X-cost card");
        var suYue=Card("WeiCaoCaoSuYueGongTu");
        Check(suYue.EnergyCost.GetResolved()==2 && !suYue.Keywords.Contains(CardKeyword.Innate),"SuYue costs 2 and has no base Innate");
        suYue.UpgradeInternal();suYue.FinalizeUpgradeInternal();
        Check(suYue.EnergyCost.GetResolved()==2 && suYue.Keywords.Contains(CardKeyword.Innate),"Upgraded SuYue gains Innate without changing cost");
        Check(Card("WeiCaoCaoChoiceAttack").Type==CardType.Attack &&
            Card("WeiCaoCaoChoiceSkill").Type==CardType.Skill &&
            Card("WeiCaoCaoChoicePower").Type==CardType.Power,"GongTu option models render their actual card type");
        foreach(var name in new[]{"WeiCaoCaoWuZhiWeiTi","WeiCaoCaoZhongYuZhiDui"})
            Check(Card(name).Rarity==CardRarity.Uncommon,"Rebalanced uncommon rarity: "+name);
        var bizuo=Power("WeiCaoCaoBiZuoPower");
        Check(bizuo.ShouldTakeExtraTurn(Owner) && bizuo.ShouldTakeExtraTurn(Owner),"Extra-turn eligibility queries cannot spend BiZuo");
        await bizuo.AfterTakingExtraTurn(NewPlayer());
        Check(bizuo.ShouldTakeExtraTurn(Owner),"Teammate extra turns cannot spend BiZuo");
        var otherExtra=Power("WeiCaoCaoBiZuoPower");
        var low=new Creature(null!,1,1){CombatState=Owner.Creature.CombatState};
        var opposing=(List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(Owner.Creature.CombatState)!;
        opposing.Add(low);
        await otherExtra.AfterTakingExtraTurn(Owner);
        Check(!(bool)otherExtra.GetType().GetProperty("ExtraTurnUsed")!.GetValue(otherExtra)!,"An unrelated extra turn while not eligible does not spend BiZuo");
        opposing.Remove(low);otherExtra.GetType().GetProperty("InExtraTurn")!.SetValue(otherExtra,false);
        Check(otherExtra.ShouldTakeExtraTurn(Owner),"BiZuo stays available after an unrelated extra turn");
        await bizuo.AfterTakingExtraTurn(Owner);
        Check((bool)bizuo.GetType().GetProperty("ExtraTurnUsed")!.GetValue(bizuo)! && (bool)bizuo.GetType().GetProperty("StripOnStart")!.GetValue(bizuo)!,"BiZuo spends its combat use only when taking the extra turn and keeps the strip pending");
        var enemy = new Creature(null!, 50, 50) { CombatState = Owner.Creature.CombatState };
        opposing.Add(enemy);
        void AddBuff(PowerModel buff, int amount)
        {
            typeof(PowerModel).GetField("_owner",F)!.SetValue(buff,enemy);
            typeof(PowerModel).GetField("_amount",F)!.SetValue(buff,amount);
            enemy.ApplyPowerInternal(buff);
        }
        var strength = new StrengthPower().ToMutable();
        var monsterBuff = new CurlUpPower().ToMutable();
        var exempt = new SlumberPower().ToMutable();
        var weak = new WeakPower().ToMutable();
        AddBuff(strength,4);
        AddBuff(monsterBuff,3);
        AddBuff(exempt,2);
        AddBuff(weak,1);
        var noticeProbe = new Harmony("Yjcm.tests.bizuo-notice");
        try
        {
            noticeProbe.Patch(Method(Type("WeiCaoCaoTurnNotice"),"Show"),
                prefix:new HarmonyMethod(Method(typeof(WeiDesignCases),nameof(ShowTurnNotice))));
            TurnNotices=0;
            await bizuo.BeforeSideTurnStart(Context,CombatSide.Player,[NewPlayer().Creature],Owner.Creature.CombatState!);
            Check(TurnNotices==0,"BiZuo does not announce a teammate's turn");
            await bizuo.BeforeSideTurnStart(Context,CombatSide.Player,[Owner.Creature],Owner.Creature.CombatState!);
            Check(TurnNotices==1,"BiZuo announces its granted extra turn once");
            await bizuo.BeforeSideTurnStart(Context,CombatSide.Player,[Owner.Creature],Owner.Creature.CombatState!);
            Check(TurnNotices==1,"BiZuo does not announce unrelated later turns");
        }
        finally { noticeProbe.UnpatchAll(noticeProbe.Id); }
        Check(!enemy.Powers.Contains(strength) && !enemy.Powers.Contains(monsterBuff),"BiZuo suspends common and monster buffs at extra-turn start");
        Check(enemy.Powers.Contains(exempt) && enemy.Powers.Contains(weak),"BiZuo preserves exempt buffs and debuffs");
        var fresh = new StrengthPower().ToMutable();
        AddBuff(fresh,2);
        await bizuo.AfterSideTurnEndLate(Context,CombatSide.Player,[NewPlayer().Creature]);
        Check(!enemy.Powers.Contains(strength),"Teammate turn end cannot restore BiZuo buffs early");
        await bizuo.AfterSideTurnEndLate(Context,CombatSide.Player,[Owner.Creature]);
        Check(enemy.Powers.Contains(strength) && strength.Amount==6 && !enemy.Powers.Contains(fresh),"BiZuo restores the original buff and merges stacks gained during the extra turn");
        Check(enemy.Powers.Contains(monsterBuff) && monsterBuff.Amount==3,"BiZuo restores the monster buff instance and amount");
        opposing.Remove(enemy);
        bizuo.GetType().GetProperty("InExtraTurn")!.SetValue(bizuo,false);
        Check(!bizuo.ShouldTakeExtraTurn(Owner),"Later normal turns cannot trigger BiZuo again");
        var restoredBizuo=Power("WeiCaoCaoBiZuoPower");
        Method(typeof(Program),"RoundTrip").Invoke(null,[bizuo,restoredBizuo]);
        Check(!restoredBizuo.ShouldTakeExtraTurn(Owner),"Reloading the battle cannot restore BiZuo's spent use");
        Check(Power("WeiCaoCaoBiZuoPower").ShouldTakeExtraTurn(Owner),"A new battle starts with an available BiZuo use");
        var saved=Power("WeiCaoCaoGongTuPower");
        saved.GetType().GetProperty("First")!.SetValue(saved,(int)CardType.Attack);
        saved.GetType().GetProperty("Second")!.SetValue(saved,(int)CardType.Skill);
        saved.GetType().GetProperty("Previous")!.SetValue(saved,(int)CardType.Attack);
        saved.GetType().GetProperty("Enhanced")!.SetValue(saved,true);
        var loaded=Power("WeiCaoCaoGongTuPower");
        Method(typeof(Program),"RoundTrip").Invoke(null,[saved,loaded]);
        Check((int)loaded.GetType().GetProperty("First")!.GetValue(loaded)! ==(int)CardType.Attack && (int)loaded.GetType().GetProperty("Second")!.GetValue(loaded)! ==(int)CardType.Skill && (int)loaded.GetType().GetProperty("Previous")!.GetValue(loaded)! ==(int)CardType.Attack,"Pattern and pending previous type survive native saved properties");
        Check((bool)loaded.GetType().GetProperty("Enhanced")!.GetValue(loaded)!,"GongTu military enhancement survives saved properties");
        foreach(var n in new[]{"WeiCaoCaoMouSiMaShiSanShi","WeiCaoCaoMouSiMaShiChenLue"})Check(Card(n).MaxUpgradeLevel==0,"No invented upgrade where sheet is blank: "+n);
        var shizha=Card("WeiCaoCaoMouChengYuShiZha");
        Check(shizha.Type==CardType.Attack && shizha.Rarity==CardRarity.Uncommon && shizha.DynamicVars.Damage.BaseValue==8 && shizha.MaxUpgradeLevel==1,"ShiZha is an uncommon eight-damage attack and can upgrade once");
        shizha.UpgradeInternal();shizha.FinalizeUpgradeInternal();
        Check(shizha.DynamicVars.Damage.BaseValue==12 && shizha.EnergyCost.GetResolved()==1,"ShiZha upgrade grants twelve damage without changing cost");
        foreach(var n in new[]{"WeiCaoCaoMouXunYuShiMouYang","WeiCaoCaoMouXunYuShiMouYin","WeiCaoCaoMouGuoJiaXianMouYang","WeiCaoCaoMouGuoJiaXianMouYin","WeiCaoCaoMouSiMaYiQuanMouYin","WeiCaoCaoMouSiMaYiQuanMouYang"})
        {
            var face=Card(n);
            Check(face.Rarity==CardRarity.Uncommon && face.ShouldShowInCardLibrary && face.CanBeGeneratedInCombat,"Both conversion faces belong to the normal card pool: "+n);
        }
        var mark=ModelDb.GetById<EnchantmentModel>(ModelDb.GetId(Type("WeiCaoCaoSiShi")));
        var dazed=new MegaCrit.Sts2.Core.Models.Cards.Dazed();db[dazed.Id]=dazed;
        var unusable=dazed.ToMutable();unusable.Owner=Owner;
        Check(!(bool)Method(rules,"CanEnchant").Invoke(null,[unusable,mark])!,"Status cards cannot enter death-soldier enchantment choices");
        Check(!(bool)Method(rules,"CanEnchant").Invoke(null,[ModelDb.GetById<CardModel>(ModelDb.GetId(Type("WeiCaoCaoSha"))),mark])!,"Canonical card models must never be enchanted");

        var harmony=new Harmony("Yjcm.tests.wei-design");bool old=TestMode.IsOn;TestMode.IsOn=true;
        void Patch(MethodInfo target,string prefix)=>harmony.Patch(target,prefix:new HarmonyMethod(Method(typeof(WeiDesignCases),prefix)));
        try
        {
            Patch(Method(Type("WeiCaoCaoAudio"),"Play"),nameof(RecordVoice));
            Patch(Method(Type("MouSimaYiSfxPlayer"),"PlayQuanMou"),nameof(RecordBossVoice));
            harmony.CreateClassProcessor(Type("WeiCaoCaoCardVoices")).Patch();
            var voiceHook=Method(Type("WeiCaoCaoCardVoices"),"Postfix");
            foreach(var (model,voice) in new[]{("WeiCaoCaoMouXunYuBiZuo","bi_zuo"),("WeiCaoCaoWeiCaoPiDianLun","dian_lun"),("WeiCaoCaoMouJiangJiYingShi","ying_shi"),("WeiCaoCaoMouChengYuShiZha","shi_zha"),("WeiCaoCaoMouXunYuShiMouYang","shi_mou_yang"),("WeiCaoCaoMouXunYuShiMouYin","shi_mou_yin"),("WeiCaoCaoMouGuoJiaXianMouYang","xian_mou_yang"),("WeiCaoCaoMouGuoJiaXianMouYin","xian_mou_yin"),("WeiCaoCaoMouHuangQuanQiaoDui","qiao_dui"),("WeiCaoCaoMouSiMaShiSanShi","san_shi"),("WeiCaoCaoMouSiMaShiChenLue","chen_lue"),("WeiCaoCaoHuBanChongYi","chong_yi"),("WeiCaoCaoXingCaoRenSuJun","su_jun"),("WeiCaoCaoXingCaoRenDiFeng","di_feng"),("WeiCaoCaoZhongYuZhiDui","zhi_dui"),("WeiCaoCaoXingZhangChunHuaLiangYan","liang_yan"),("WeiCaoCaoSpGuanYuNuChen","nu_chen"),("WeiCaoCaoBianXiDunXi","dun_xi"),("WeiCaoCaoShenDianWeiCheXie","che_xie"),("WeiCaoCaoSuYueGongTu","gong_tu"),("WeiCaoCaoXiaHouXuanBoXuan","bo_xuan"),("WeiCaoCaoYanRouChouTao","chou_tao"),("WeiCaoCaoSunLiKangLi","kang_li"),("WeiCaoCaoWuZhiWeiTi","wei_ti"),("WeiCaoCaoZhuLingZhanYi","zhan_yi"),("WeiCaoCaoZhouXuanWuMei","wu_mei"),("WeiCaoCaoCheZhouSheFu","she_fu"),("WeiCaoCaoYangHuMingFa","ming_fa"),("WeiCaoCaoShenDengAiTuoYu","tuo_yu")})
            {
                int count=Voices.Count;var played=Record(Card(model));voiceHook.Invoke(null,[played]);
                Check(Voices.Count==count+1&&Voices[^1]==voice,"New supplied voice matches actual played card: "+model);
                typeof(CardPlay).GetProperty("PlayIndex")!.SetValue(played,1);voiceHook.Invoke(null,[played]);
                Check(Voices.Count==count+1,"Native replay does not duplicate voice: "+model);
            }
            foreach(var (model,yangForm) in new[]{("WeiCaoCaoMouSiMaYiQuanMouYin",false),("WeiCaoCaoMouSiMaYiQuanMouYang",true)})
            {
                int count=BossVoices.Count;var played=Record(Card(model));voiceHook.Invoke(null,[played]);
                Check(BossVoices.Count==count+1&&BossVoices[^1]==yangForm,"QuanMou uses the boss random voice series for its matching form");
                typeof(CardPlay).GetProperty("PlayIndex")!.SetValue(played,1);voiceHook.Invoke(null,[played]);
                Check(BossVoices.Count==count+1,"Replays cannot duplicate boss voice playback");
            }
            int voiceCount=Voices.Count;voiceHook.Invoke(null,[Record(Card("WeiCaoCaoSha"))]);
            Check(Voices.Count==voiceCount,"Voice mapping leaves other cards to their existing audio");
            Patch(Method(rules,"Hand"),nameof(SelectHand));
            Patch(Method(rules,"Draw"),nameof(DrawCards));
            Patch(Method(rules,"Play"),nameof(PlayCard));
            Patch(Method(Type("WeiCaoCaoJunHePower"),"TryConsume"),nameof(Consume));
            Patch(typeof(CardCmd).GetMethods().Single(m=>m.Name=="Discard"&&m.GetParameters().Any(p=>p.ParameterType==typeof(IEnumerable<CardModel>))),nameof(DiscardCards));
            Patch(typeof(CombatManager).GetProperty("IsOverOrEnding")!.GetMethod!,nameof(NotEnding));
            Patch(typeof(PowerCmd).GetMethods().Single(m=>m.Name=="Remove"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(PowerModel)),nameof(RemovePower));
            var card=Card("WeiCaoCaoWeiCaoPiDianLun");
            var a=Card("WeiCaoCaoXingCaoRenDiFeng");var b=Card("WeiCaoCaoShan");var c=Card("WeiCaoCaoWeiCaoPiDianLun");
            Hand.AddRange([a,b,c]);Choices.Enqueue([a,b,c]);UseMilitary=true;
            await (Task)Method(card.GetType(),"OnPlay").Invoke(card,[Context,Record(card)])!;
            Check(Discards==3 && Drawn==6 && MilitarySpends==1 && FreeUntilPlayed,"DianLun consumes one military resource and doubles actual free draws");
            Check(ReleaseSelectionOnConfirm,"DianLun releases selection visuals before discarding or reopening the hand picker");
            var invalid=Card("WeiCaoCaoXiaHouXuanBoXuan");Hand.AddRange([a,b,invalid]);Choices.Enqueue([a,b,invalid]);Choices.Enqueue([]);
            int discards=Discards,draws=Drawn,spends=MilitarySpends,selects=Selections;
            await (Task)Method(card.GetType(),"OnPlay").Invoke(card,[Context,Record(card)])!;
            Check(Selections==selects+2 && Discards==discards && Drawn==draws && MilitarySpends==spends,"Invalid progression reselects and skip causes no effects or resource spend");
            var sameCost=Enumerable.Range(0,3).Select(_=>Card("WeiCaoCaoShan")).ToArray();
            Hand.AddRange(sameCost);Choices.Enqueue(sameCost);Choices.Enqueue([]);selects=Selections;
            await (Task)Method(card.GetType(),"OnPlay").Invoke(card,[Context,Record(card)])!;
            Check(Selections==selects+2 && sameCost.All(Hand.Contains) && Discards==discards && Drawn==draws && MilitarySpends==spends,"Equal-cost selection is reoffered without discarding, drawing, or consuming military");
            Patch(Method(rules,"ChooseType"),nameof(SelectType));
            Patch(typeof(PowerCmd).GetMethods().Single(m=>m.Name=="Apply"&&!m.IsGenericMethod&&m.GetParameters()[1].ParameterType==typeof(PowerModel)),nameof(InstallGongTu));
            Patch(typeof(PowerCmd).GetMethod("ModifyAmount")!,nameof(KeepPower));
            Patch(typeof(PlayerCmd).GetMethods().Single(m=>m.Name=="GainEnergy"&&m.GetParameters().Length==2),nameof(GainEnergy));
            ((List<PowerModel>)typeof(Creature).GetField("_powers",F)!.GetValue(Owner.Creature)!).Add(bizuo);
            var duplicateBiZuo=Card("WeiCaoCaoMouXunYuBiZuo");
            await (Task)Method(duplicateBiZuo.GetType(),"OnPlay").Invoke(duplicateBiZuo,[Context,Record(duplicateBiZuo)])!;
            Check(!bizuo.ShouldTakeExtraTurn(Owner),"Playing another BiZuo reuses the same spent ability instead of restoring its combat use");
            var gong=Card("WeiCaoCaoSuYueGongTu");InstalledGongTu=Power("WeiCaoCaoGongTuPower");
            UseMilitary=false;Types.Enqueue(CardType.Attack);Types.Enqueue(CardType.Skill);
            spends=MilitarySpends;draws=Drawn;
            await (Task)Method(gong.GetType(),"OnPlay").Invoke(gong,[Context,Record(gong)])!;
            UseMilitary=true;
            await InstalledGongTu.AfterCardPlayed(Context,Record(Card("WeiCaoCaoSha")));
            await InstalledGongTu.AfterCardPlayed(Context,Record(Card("WeiCaoCaoShan")));
            Check(Drawn==draws+2 && EnergyGained==0 && MilitarySpends==spends,"Unenhanced GongTu draws two and cannot start consuming military on later matches");
            Types.Enqueue(CardType.Attack);Types.Enqueue(CardType.Skill);draws=Drawn;
            await (Task)Method(gong.GetType(),"OnPlay").Invoke(gong,[Context,Record(gong)])!;
            Check(MilitarySpends==spends+1 && EnergyGained==0,"GongTu consumes military at activation without immediately granting energy");
            UseMilitary=false;
            for(int i=0;i<2;i++)
            {
                await InstalledGongTu.AfterCardPlayed(Context,Record(Card("WeiCaoCaoSha")));
                await InstalledGongTu.AfterCardPlayed(Context,Record(Card("WeiCaoCaoShan")));
            }
            Check(Drawn==draws+4 && EnergyGained==2 && MilitarySpends==spends+1,"Enhanced GongTu repeatedly draws two and grants one energy without further military payments");
            Types.Enqueue(CardType.Attack);Types.Enqueue(CardType.Skill);
            await (Task)Method(gong.GetType(),"OnPlay").Invoke(gong,[Context,Record(gong)])!;
            Check((bool)InstalledGongTu.GetType().GetProperty("Enhanced")!.GetValue(InstalledGongTu)! && MilitarySpends==spends+1,"Replaying without military retains the installed enhancement");
            Patch(Method(rules,"Pool"),nameof(EmptyPool));
            Patch(Method(rules,"Convert").MakeGenericMethod(Type("WeiCaoCaoMouXunYuShiMouYin")),nameof(SkipConversion));
            var shi=Card("WeiCaoCaoMouXunYuShiMouYang");
            bool CanPrint(string name)=>(bool)Method(Type("WeiCaoCaoShiMou"),"CanPrint").Invoke(null,[Card(name)])!;
            Check(!CanPrint("WeiCaoCaoWeiCaoPiDianLun") && !CanPrint("WeiCaoCaoShenDengAiTuoYu") && !CanPrint("WeiCaoCaoShenDianWeiCheXie") &&
                CanPrint("WeiCaoCaoSha") && CanPrint("WeiCaoCaoShan") && !CanPrint("WeiCaoCaoMouXunYuShiMouYin"),
                "Both ShiMou faces exclude rare cards and god generals without losing regular attack or skill cards");
            Hand.Clear();var initial=Enumerable.Range(0,8).Select(_=>Card("WeiCaoCaoSha")).ToArray();Hand.AddRange(initial);
            Choices.Enqueue(initial.Take(3).ToArray());Choices.Enqueue(initial.Skip(3).Take(2).ToArray());
            bool added=false;
            AfterDiscard=_=>{if(added)return;added=true;Hand.Add(Card("WeiCaoCaoSha"));Hand.Add(Card("WeiCaoCaoShan"));};
            selects=Selections;discards=Discards;
            await (Task)Method(shi.GetType(),"OnPlay").Invoke(shi,[Context,Record(shi)])!;
            Check(Hand.Count==5 && Selections==selects+2 && Discards==discards+5,"ShiMou rechecks after discard-triggered draws and asks the player for the remaining discards");
            Hand.Clear();initial=Enumerable.Range(0,6).Select(_=>Card("WeiCaoCaoSha")).ToArray();Hand.AddRange(initial);Choices.Enqueue([initial[0]]);
            AfterDiscard=discarded=>Hand.Add(discarded);selects=Selections;
            await (Task)Method(shi.GetType(),"OnPlay").Invoke(shi,[Context,Record(shi)])!;
            Check(Selections==selects+1 && Hand.Count==6,"A discard that exactly recreates the same hand cannot trap ShiMou in an infinite selection loop");
            AfterDiscard=null;Hand.Clear();
            var ming=Power("WeiCaoCaoMingFaPower");
            var upgrade=Card("WeiCaoCaoShan");upgrade.UpgradeInternal();upgrade.FinalizeUpgradeInternal();
            Method(ming.GetType(),"Add").Invoke(ming,[upgrade,3]);
            await ming.AfterPlayerTurnStart(Context,NewPlayer());Check(Played.Count==0,"Delayed replay ignores teammate start");
            await ming.AfterPlayerTurnStart(Context,Owner);
            Check(Played.Count==1 && Played[0].GetType()==upgrade.GetType() && ReplayCounts.Last()==2 && Played[0].IsUpgraded,"MingFa keeps the selected upgrade and uses native replay for X uses");
            Check(Played[0].BaseReplayCount==0,"MingFa's replay bonus does not persist on the generated card");
            await ming.AfterPlayerTurnStart(Context,Owner);Check(Played.Count==1,"Delayed replay resolves only once");
            var failedMing=Power("WeiCaoCaoMingFaPower");
            Method(failedMing.GetType(),"Add").Invoke(failedMing,[upgrade,3]);
            bool failed=false;FailPlay=true;
            try { await failedMing.AfterPlayerTurnStart(Context,Owner); }
            catch(InvalidOperationException) { failed=true; }
            finally { FailPlay=false; }
            Check(failed && Played.Last().BaseReplayCount==0,"MingFa removes its temporary replay count even when auto-play fails");

            Patch(typeof(CreatureCmd).GetMethods().Single(m=>m.Name=="SetCurrentHp"),nameof(RestoreHp));
            // Minimal enemy fixtures have no MonsterModel for the native combat listener traversal.
            Patch(typeof(Creature).GetProperty(nameof(Creature.CanReceivePowers))!.GetMethod!,nameof(AllowTestPowerTarget));
            var combat=(CombatState)Owner.Creature.CombatState!;
            combat.CurrentSide=CombatSide.Player;
            var enemies=(List<Creature>)typeof(CombatState).GetField("_enemies",F)!.GetValue(combat)!;
            var first=new Creature(null!,20,20){CombatState=combat,CombatId=101};
            var survivor=new Creature(null!,65,80){CombatState=combat,CombatId=102};
            enemies.AddRange([first,survivor]);
            Owner.Creature.SetCurrentHpInternal(70);
            var wuMei=Card("WeiCaoCaoZhouXuanWuMei");
            await wuMei.BeforeSideTurnStart(Context,CombatSide.Player,[Owner.Creature],combat);
            // Opening hooks may change HP before the innate card auto-plays.
            Owner.Creature.SetCurrentHpInternal(91);
            survivor.SetCurrentHpInternal(58);
            await (Task)Method(wuMei.GetType(),"OnPlay").Invoke(wuMei,[Context,Record(wuMei)])!;
            var dream=InstalledGongTu; // The shared Apply stub exposes the installed power.
            Check(dream.GetType()==Type("WeiCaoCaoWuMeiPower") && !dream.ShouldTakeExtraTurn(Owner),
                "WuMei grants its follow-up only after the fake turn settles");
            Owner.Creature.SetCurrentHpInternal(55);
            survivor.SetCurrentHpInternal(39);
            enemies.Remove(first);
            var summoned=new Creature(null!,7,7){CombatState=combat,CombatId=103};enemies.Add(summoned);
            var other=NewPlayer();
            await dream.AfterTakingExtraTurn(other);
            await dream.AfterSideTurnEndLate(Context,CombatSide.Player,[other.Creature]);
            Check(Restored.Count==0 && !dream.ShouldTakeExtraTurn(other),"A teammate cannot trigger WuMei's rewind or turn");
            await dream.AfterSideTurnEndLate(Context,CombatSide.Player,[Owner.Creature]);
            Check(Restored.Count==2 && Restored[Owner.Creature]==70 && Restored[survivor]==65 &&
                !Restored.ContainsKey(first) && !Restored.ContainsKey(summoned),
                "WuMei restores pre-draw HP at the end of the first fake turn, before the extra-turn query");
            Check(dream.ShouldTakeExtraTurn(Owner),"The restored first turn still queues an immediate player follow-up");
            await dream.AfterTakingExtraTurn(Owner);
            Check(!dream.ShouldTakeExtraTurn(Owner),"WuMei grants only one follow-up turn");
            Restored.Clear();
            Owner.Creature.SetCurrentHpInternal(49);
            survivor.SetCurrentHpInternal(32);
            await dream.AfterSideTurnEndLate(Context,CombatSide.Player,[Owner.Creature]);
            Check(Restored.Count==0 && Owner.Creature.CurrentHp==49 && survivor.CurrentHp==32,
                "HP changes in the second player turn are not rewound");

            var kangLi=Power("WeiCaoCaoKangLiPower");
            draws=Drawn;
            await kangLi.AfterDamageReceived(Context,Owner.Creature,Damage(),ValueProp.Move,survivor,null);
            await kangLi.AfterDamageGiven(Context,Owner.Creature,Damage(),ValueProp.Move,survivor,sha);
            Check(Drawn==draws+4,"KangLi draws two unconditionally after receiving or dealing damage");
            enemies.Clear();

            var yin=Power("WeiCaoCaoQuanMouYinPower",amount:3);
            var damageMethod=typeof(CreatureCmd).GetMethods().Where(m=>m.Name=="Damage"&&m.GetParameters().Any(p=>p.ParameterType==typeof(IEnumerable<Creature>))&&m.GetParameters().Any(p=>p.ParameterType==typeof(decimal))).OrderByDescending(m=>m.GetParameters().Length).First();
            Patch(damageMethod,nameof(PropagateDamage));
            NestedDamage=()=>yin.AfterDamageReceived(Context,Owner.Creature,Damage(),ValueProp.Unpowered,null,null);
            await yin.AfterDamageReceived(Context,Owner.Creature,Damage(),ValueProp.Move,Owner.Creature,sha);
            Check(DamageCalls==1,"QuanMou Yin cannot recurse through its own splash damage");
            await yin.AfterDamageReceived(Context,Owner.Creature,Damage(),ValueProp.Move,Owner.Creature,sha);
            Check(DamageCalls==2,"Recursion guard clears for the next real hit");
            var yang=Power("WeiCaoCaoQuanMouYangPower",amount:3);
            var state=(CombatState)Owner.Creature.CombatState!;
            var allies=(List<Creature>)typeof(CombatState).GetField("_allies",F)!.GetValue(state)!;allies.Add(Owner.Creature);allies.Add(NewPlayer().Creature);
            Patch(typeof(CreatureCmd).GetMethods().Single(m=>m.Name=="GainBlock"&&m.GetParameters()[1].ParameterType==typeof(decimal)),nameof(PropagateBlock));
            NestedBlock=()=>yang.AfterBlockGained(Owner.Creature,3,ValueProp.Unpowered,null);
            await yang.AfterBlockGained(Owner.Creature,5,ValueProp.Move,null);
            Check(BlockCalls==2,"QuanMou Yang grants each player once and cannot recursively grant block");
            var shan=Card("WeiCaoCaoShan");shan.EnergyCost.SetUntilPlayed(0);
            shan.EnergyCost.EndOfTurnCleanup();Check(shan.EnergyCost.GetResolved()==0,"Next-play-free survives end of turn");
            shan.EnergyCost.AfterCardPlayedCleanup();Check(shan.EnergyCost.GetResolved()==1,"Next-play-free clears after playing");
            harmony.CreateClassProcessor(Type("WeiCaoCaoJunShanPierce")).Patch();Check(true,"JunShan binds the real stable/beta damage entrypoints");
        }
        finally{harmony.UnpatchAll(harmony.Id);TestMode.IsOn=old;}
        await CheckXianMouAndBoXuan(db);
        Console.WriteLine($"CAOCAO_WEI_DESIGN_OK assertions={Checks} models=49 progression=true delayedReplay=true recursionGuard=true");
    }

    private static CardModel[] ViewedCards = [];
    private static CardSelectorPrefs GridPreferences;
    private static readonly List<(Creature Target, decimal Amount, ValueProp Props)> SelfDamage = [];

    private static async Task CheckXianMouAndBoXuan(Dictionary<ModelId, AbstractModel> db)
    {
        Owner = NewPlayer();
        Choices.Clear();
        var player = Owner.Creature;
        var drawPile = Cards(Owner.PlayerCombatState!.DrawPile);
        var powers = (List<PowerModel>)typeof(Creature).GetField("_powers", F)!.GetValue(player)!;
        var liJie = Type("LiJiePower");
        if (!db.ContainsKey(ModelDb.GetId(liJie))) db[ModelDb.GetId(liJie)] = (PowerModel)Activator.CreateInstance(liJie)!;
        var rules = Type("WeiCaoCaoDesignRules");
        var yang = Card("WeiCaoCaoMouGuoJiaXianMouYang");
        var yin = Card("WeiCaoCaoMouGuoJiaXianMouYin");
        var harmony = new Harmony("Yjcm.tests.caocao.xianmou-boxuan");
        void Patch(MethodInfo method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(Method(typeof(WeiDesignCases), prefix)));
        Task PlayXianMou(CardModel card, CardPlay play) => (Task)Method(card.GetType(), "OnPlay").Invoke(card, [Context, play])!;
        try
        {
            Patch(Method(typeof(CardSelectCmd), "FromSimpleGrid"), nameof(SelectGrid));
            Patch(typeof(CardSelectCmd).GetMethods().Single(m=>m.Name=="FromCombatPile"&&m.GetParameters().Length==5), nameof(SelectCombatPile));
            Patch(Method(rules, "Hand"), nameof(SelectHand));
            Patch(Method(rules, "Draw"), nameof(DrawCards));
            Patch(Method(rules, "Play"), nameof(PlayCard));
            Patch(Method(rules, "Convert").MakeGenericMethod(yang.GetType()), nameof(SkipConversion));
            Patch(Method(rules, "Convert").MakeGenericMethod(yin.GetType()), nameof(SkipConversion));
            Patch(typeof(CombatManager).GetProperty("IsOverOrEnding")!.GetMethod!, nameof(NotEnding));
            Patch(typeof(CardPileCmd).GetMethod("Add", [typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)])!, nameof(MoveToHand));
            Patch(typeof(CardCmd).GetMethod("Discard", [typeof(PlayerChoiceContext), typeof(IEnumerable<CardModel>)])!, nameof(DiscardCards));
            Patch(typeof(PowerCmd).GetMethods().Single(m => m.Name == "Apply" && !m.IsGenericMethod && m.GetParameters()[1].ParameterType == typeof(PowerModel)), nameof(TrackAppliedPower));
            Patch(typeof(PowerCmd).GetMethod("Remove", [typeof(PowerModel)])!, nameof(RemoveTrackedPower));
            var damage = typeof(CreatureCmd).GetMethods().Single(m => m.Name == "Damage" &&
                m.GetParameters() is { Length: 6 } p && p[1].ParameterType == typeof(Creature) && p[2].ParameterType == typeof(decimal));
            Patch(damage, nameof(TrackSelfDamage));
            Patch(typeof(PlayerCmd).GetMethods().Single(m => m.Name == "GainEnergy" && m.GetParameters().Length == 2), nameof(GainEnergy));

            var top = Enumerable.Range(0, 7).Select(_ => Card("WeiCaoCaoSha")).ToArray();
            drawPile.AddRange(top);
            Choices.Enqueue([top[2]]);
            await PlayXianMou(yang, Record(yang));
            var counter = powers.Single(p => p.GetType().Name == "WeiCaoCaoXianMouCountPower");
            Check(counter.Amount == 1 && !(bool)Type("WeiCaoCaoXianMouCountPower").GetProperty("IsVisibleInternal", F)!.GetValue(counter)!, "XianMou starts a hidden shared count");
            Check(ViewedCards.SequenceEqual(top.Take(5)) && GridPreferences.MaxSelect == 1 && Hand.SequenceEqual([top[2]]), "Yang views the first five cards and gains at most its play count");
            Check(powers.Count == 1 && SelfDamage.Count == 0, "Gaining the full count does not grant pending draw or deal damage");

            typeof(PowerModel).GetField("_amount", F)!.SetValue(counter, 5);
            Choices.Enqueue(drawPile.Take(5).ToArray());
            await PlayXianMou(yang, Record(yang));
            var pending = powers.Single(p => p.GetType().Name == "WeiCaoCaoXianMouDrawPower");
            Check(counter.Amount == 6 && GridPreferences.MaxSelect == 5 && Hand.Count == 6, "All five cards are still short of the sixth use");
            Type("WeiCaoCaoDesignPower").GetField("_choiceSource", F)!.SetValue(pending, yang);
            int previousDraws = Drawn;
            var blockedHit = new DamageResult(player, ValueProp.Unpowered) { BlockedDamage = 6 };
            await pending.AfterDamageReceived(Context, player, blockedHit, ValueProp.Unpowered, null, null);
            Check(Drawn == previousDraws && powers.Contains(pending), "Blocked damage does not consume the pending draw");
            await pending.AfterDamageReceived(Context, player, Damage(), ValueProp.Unpowered, null, null);
            Check(Drawn == previousDraws + 2 && !powers.Contains(pending), "The next actual damage consumes pending draw and draws two");

            Hand.Add(Card("WeiCaoCaoSha"));
            Choices.Enqueue(Hand.ToArray());
            var enemy = new Creature(null!, 30, 30) { CombatState = player.CombatState };
            var yinPlay = Record(yin);
            typeof(CardPlay).GetProperty("Target")!.SetValue(yinPlay, enemy);
            await PlayXianMou(yin, yinPlay);
            Check(counter.Amount == 7 && Hand.Count == 0 && enemy.Powers.Single(p => p.GetType() == liJie).Amount == 7, "Yin shares the uncapped count and applies temporary strength loss by cards discarded");
            Check(SelfDamage.SequenceEqual([(player, 6m, ValueProp.Unpowered)]), "Discarding the full count deals six blockable damage to the player");

            var boXuan = Power("WeiCaoCaoBoXuanPower", amount: 2);
            Type("WeiCaoCaoDesignPower").GetField("_choiceSource", F)!.SetValue(boXuan, yang);
            var played = Card("WeiCaoCaoSha");
            var sameType = Card("WeiCaoCaoXingCaoRenDiFeng");
            var sameCostAndLength = Card("WeiCaoCaoShan");
            var different = Card("WeiCaoCaoXiaHouXuanBoXuan");
            drawPile.Clear();
            drawPile.AddRange([different, sameType, sameCostAndLength]);
            Choices.Enqueue([sameType]);
            previousDraws = Drawn;
            int previousEnergy = EnergyGained;
            await boXuan.AfterCardPlayed(Context, Record(played));
            Check(ViewedCards.SequenceEqual([sameType, sameCostAndLength]) && Drawn == previousDraws + 1 && EnergyGained == previousEnergy + 1 && ReferenceEquals(Played.Last(), sameType),
                "Different bottom cards independently match the played card and unlock all three rewards");

            drawPile.Clear();
            drawPile.AddRange([played, different, Card("WeiCaoCaoXiaHouXuanBoXuan")]);
            previousDraws = Drawn;
            previousEnergy = EnergyGained;
            int previousPlays = Played.Count;
            await boXuan.AfterCardPlayed(Context, Record(played));
            Check(GridPreferences.MaxSelect == 0 && Drawn == previousDraws && EnergyGained == previousEnergy && Played.Count == previousPlays,
                "Identical bottom cards and a matching top card cannot trigger a reward by themselves");

            drawPile.Clear();
            drawPile.Add(sameCostAndLength);
            Choices.Enqueue([sameCostAndLength]);
            await boXuan.AfterCardPlayed(Context, Record(played));
            Check(GridPreferences.MaxSelect == 1 && EnergyGained == previousEnergy + 1 && Played.Count == previousPlays + 1, "A single remaining bottom card can satisfy BoXuan");

            var twoMatches = new[] { Card("WeiCaoCaoSha"), Card("WeiCaoCaoSha") };
            drawPile.Clear();
            drawPile.AddRange(twoMatches);
            Choices.Enqueue([twoMatches[0]]);
            previousDraws = Drawn;
            previousEnergy = EnergyGained;
            previousPlays = Played.Count;
            await boXuan.AfterCardPlayed(Context, Record(played));
            Check(GridPreferences.MaxSelect == 1 && Drawn == previousDraws + 1 && EnergyGained == previousEnergy + 1 && Played.Count == previousPlays + 1,
                "Two matches for each BoXuan condition still grant exactly one draw, one play and one energy");

            Choices.Enqueue([]);
            previousDraws = Drawn;
            previousEnergy = EnergyGained;
            previousPlays = Played.Count;
            await boXuan.AfterCardPlayed(Context, Record(played));
            Check(GridPreferences.MinSelect == 0 && GridPreferences.MaxSelect == 1 &&
                Drawn == previousDraws + 1 && EnergyGained == previousEnergy + 1 && Played.Count == previousPlays,
                "Skipping BoXuan declines the free play and still resolves the draw and energy");

            var zhanYi = Power("WeiCaoCaoZhanYiPower");
            Hand.AddRange([Card("WeiCaoCaoSha"), Card("WeiCaoCaoShan")]);
            Choices.Enqueue([Card("WeiCaoCaoChoiceSkill")]);
            await zhanYi.AfterPlayerTurnStart(Context, Owner);
            Check(ViewedCards.Select(card => card.Type).SequenceEqual([CardType.Attack, CardType.Skill, CardType.Power]) &&
                Hand.Count == 1 && Hand[0].Type == CardType.Attack &&
                (int)zhanYi.GetType().GetProperty("DiscardedType")!.GetValue(zhanYi)! == (int)CardType.Skill,
                "ZhanYi selects and discards a type at turn start using its out-of-pile choice source");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool SelectGrid(IEnumerable<CardModel> __1, CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
    {
        // Capture the actual options and limits after the shared selector clamps the request.
        ViewedCards = __1.ToArray();
        GridPreferences = prefs;
        __result = Task.FromResult<IEnumerable<CardModel>>(prefs.MaxSelect > 0 && Choices.Count > 0 ? Choices.Dequeue() : []);
        return false;
    }

    private static bool SelectCombatPile(CardPile pile, CardSelectorPrefs prefs, Func<CardModel,bool> filter, ref Task<IEnumerable<CardModel>> __result) =>
        SelectGrid(pile.Cards.Where(filter),prefs,ref __result);

    private static bool MoveToHand(CardModel card, ref Task<CardPileAddResult> __result)
    {
        Cards(Owner.PlayerCombatState!.DrawPile).Remove(card);
        Hand.Add(card);
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card });
        return false;
    }

    private static bool TrackAppliedPower(PowerModel power, Creature target, decimal amount, ref Task __result)
    {
        var powers = (List<PowerModel>)typeof(Creature).GetField("_powers", F)!.GetValue(target)!;
        var current = powers.FirstOrDefault(p => p.GetType() == power.GetType());
        if (current == null)
        {
            current = power;
            typeof(PowerModel).GetField("_owner", F)!.SetValue(current, target);
            powers.Add(current);
        }
        typeof(PowerModel).GetField("_amount", F)!.SetValue(current, current.Amount + (int)amount);
        __result = Task.CompletedTask;
        return false;
    }

    private static bool RemoveTrackedPower(PowerModel power, ref Task __result)
    {
        ((List<PowerModel>)typeof(Creature).GetField("_powers", F)!.GetValue(power.Owner)!).Remove(power);
        __result = Task.CompletedTask;
        return false;
    }

    private static bool TrackSelfDamage(Creature target, decimal amount, ValueProp props, ref Task<IEnumerable<DamageResult>> __result)
    {
        SelfDamage.Add((target, amount, props));
        __result = Task.FromResult(Enumerable.Empty<DamageResult>());
        return false;
    }
    private static string? CustomPortraitSafe(this CardModel c)=>c.GetType().GetProperty("CustomPortraitPath")?.GetValue(c) as string;
    private static bool InstallGongTu(PowerModel power,Creature target,decimal amount,ref Task __result)
    {
        typeof(PowerModel).GetField("_owner",F)!.SetValue(power,target);
        typeof(PowerModel).GetField("_amount",F)!.SetValue(power,(int)amount);
        ((List<PowerModel>)typeof(Creature).GetField("_powers",F)!.GetValue(target)!).Add(power);
        InstalledGongTu=power;__result=Task.CompletedTask;return false;
    }
    private static bool KeepPower(PowerModel power,ref Task<int> __result){__result=Task.FromResult(power.Amount);return false;}
    private static bool SelectType(ref Task<CardType?> __result){__result=Task.FromResult(Types.Dequeue());return false;}
    private static bool GainEnergy(decimal amount,ref Task __result){EnergyGained+=(int)amount;__result=Task.CompletedTask;return false;}
    private static bool RecordBossVoice(bool isYang){BossVoices.Add(isYang);return false;}
    private static bool RecordVoice(string name){Voices.Add(name);return false;}
    private static bool SelectHand(bool releaseSelectionOnConfirm,ref Task<IReadOnlyList<CardModel>> __result)
    {
        Selections++;
        ReleaseSelectionOnConfirm=releaseSelectionOnConfirm;
        __result=Task.FromResult<IReadOnlyList<CardModel>>(Choices.Count>0?Choices.Dequeue():[]);
        return false;
    }
    private static bool DrawCards(int count,bool freeUntilPlayed,ref Task<IReadOnlyList<CardModel>> __result){Drawn+=count;FreeUntilPlayed=freeUntilPlayed;__result=Task.FromResult<IReadOnlyList<CardModel>>([]);return false;}
    private static bool Consume(ref Task<bool> __result){if(UseMilitary)MilitarySpends++;__result=Task.FromResult(UseMilitary);return false;}
    private static bool ShowTurnNotice(){TurnNotices++;return false;}
    private static bool PlayCard(CardModel card,ref Task __result){Played.Add(card);ReplayCounts.Add(card.BaseReplayCount);__result=FailPlay?Task.FromException(new InvalidOperationException("fixture failure")):Task.CompletedTask;return false;}
    private static bool NotEnding(ref bool __result){__result=false;return false;}
    private static bool RemovePower(ref Task __result){__result=Task.CompletedTask;return false;}
    private static bool AllowTestPowerTarget(ref bool __result)
    {
        __result = true;
        return false;
    }
    private static bool RestoreHp(Creature creature,decimal amount,ref Task __result){Restored[creature]=amount;__result=Task.CompletedTask;return false;}
    private static bool DiscardCards(IEnumerable<CardModel> cards,ref Task __result){foreach(var c in cards.ToArray()){Hand.Remove(c);Discards++;AfterDiscard?.Invoke(c);}__result=Task.CompletedTask;return false;}
    private static bool EmptyPool(ref IEnumerable<CardModel> __result){__result=[];return false;}
    private static bool SkipConversion(ref Task __result){__result=Task.CompletedTask;return false;}
    private static bool PropagateDamage(ref Task<IEnumerable<DamageResult>> __result)
    {
        DamageCalls++;if(DamageCalls>10)throw new Exception("Recursive damage");
        async Task<IEnumerable<DamageResult>> Forward(){if(NestedDamage!=null)await NestedDamage();return [];}
        __result=Forward();return false;
    }
    private static bool PropagateBlock(ref Task<decimal> __result)
    {
        BlockCalls++;if(BlockCalls>10)throw new Exception("Recursive block");
        async Task<decimal> Forward(){if(NestedBlock!=null)await NestedBlock();return 3;}
        __result=Forward();return false;
    }
}
