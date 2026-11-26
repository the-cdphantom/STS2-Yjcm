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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

// Invoke the production resolver and WenLan hooks; stub only engine UI/pile VFX,
// resource boundary and combat-command dispatch (there is no Godot scene here).
internal static class FuLeCases
{
    private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static Assembly Mod=null!;
    private static Type Fu=null!, Play=null!;
    private static Player Owner=null!;
    private static PlayerChoiceContext Context=null!;
    private static readonly Dictionary<string,CardModel> Models=[];
    private static int Checks,Energy,Spends,Selections,Generated,Assigned,HandChoices;
    private static string Pick="";
    private static CardModel? Played;
    private static PileType Result=PileType.Discard;
    private static bool CancelTarget;
    private static bool SelectOne;
    private static Func<Task>? DuringGeneration;
    private static int HandVisualRepairs;
    private static CardModel? FloatingCard;
    private static void Check(bool b,string s) {if(!b)throw new Exception(s);Checks++;}
    private static MethodInfo Method(Type t,string n)=>t.GetMethod(n,F)!;
    private static object Invoke(Type t,string n,params object?[] a)=>Method(t,n).Invoke(null,a)!;
    private static CardModel New(string name)
    {
        var type=Mod.GetType("Yjcm."+name,true)!;
        var c=ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();c.Owner=Owner;return c;
    }
    private static object Enchant(CardModel c,string alias)
    {
        var e=ModelDb.GetById<EnchantmentModel>(ModelDb.GetId(Fu)).ToMutable();
        Fu.GetProperty("AlternateEntry")!.SetValue(e,alias);
        c.EnchantInternal(e,1);c.AddKeyword(CardKeyword.Retain);return e;
    }
    private static List<CardModel> Cards(CardPile p)=>(List<CardModel>)typeof(CardPile).GetField("_cards",F)!.GetValue(p)!;
    private static List<CardModel> Hand=>Cards(Owner.PlayerCombatState!.Hand);
    private static CardPlay Record(CardModel card,int index=0,int count=1)
    {
        var p=(CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
        foreach(var (n,v) in new (string,object?)[]{("Card",card),("Player",Owner),("Target",null),("PlayIndex",index),("PlayCount",count),("ResultPile",PileType.Discard),("Resources",default(ResourceInfo)),("IsAutoPlay",false)})
            typeof(CardPlay).GetProperty(n)?.SetValue(p,v);
        return p;
    }
    public static async Task Run(Assembly mod)
    {
        Mod=mod;Fu=mod.GetType("Yjcm.WeiCaoCaoFu",true)!;Play=mod.GetType("Yjcm.WeiCaoCaoFuPlay",true)!;
        Owner=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var pcs=(PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        typeof(Player).GetProperty("PlayerCombatState")!.SetValue(Owner,pcs);
        var creature=new Creature(Owner,100,100){CombatState=new CombatState()};
        typeof(Player).GetField("<Creature>k__BackingField",F)!.SetValue(Owner,creature);
        typeof(Player).GetField("<Character>k__BackingField",F)!.SetValue(Owner,new Ironclad());
        typeof(Player).GetField("<Deck>k__BackingField",F)!.SetValue(Owner,new CardPile(PileType.Deck));
        foreach(var (name,type) in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})
            typeof(PlayerCombatState).GetField("<"+name+">k__BackingField",F)!.SetValue(pcs,new CardPile(type));
        Context=new ThrowingPlayerChoiceContext();
        var db=(Dictionary<ModelId,AbstractModel>)typeof(ModelDb).GetField("_contentById",F)!.GetValue(null)!;
        foreach(var type in mod.GetTypes().Where(t=>!t.IsAbstract&&t.Name.StartsWith("WeiCaoCao")&&(t.IsSubclassOf(typeof(CardModel))||t==Fu)))
        {
            var id=ModelDb.GetId(type);
            var model=db.TryGetValue(id,out var current)?current:(AbstractModel)Activator.CreateInstance(type)!;db[id]=model;
            if(model is CardModel card)Models[card.Id.Entry]=card;
        }
        string sha=New("WeiCaoCaoSha").Id.Entry,shan=New("WeiCaoCaoShan").Id.Entry,jiu=New("WeiCaoCaoJiu").Id.Entry;
        var powerType=mod.GetType("Yjcm.WeiCaoCaoWenLan",true)!;
        string[] Union(string[] a,string[] b)=>(string[])Invoke(powerType,"UnionIfMatched",a,b);
        Check(Union([sha,shan],[sha,jiu]).Length==3,"Sha/Shan + Sha/Jiu must generate exactly three names");
        Check(Union([sha,shan],[shan,jiu]).Length==3,"A Fu B + B Fu C must match alias against original name");
        Check(Union([sha,shan],[jiu,sha]).Length==3,"Original name must match the other card's alias");
        Check(Union([sha,jiu],[shan,jiu]).Length==3,"Two aliases with the same name must also match");
        Check(Union([sha,shan],[shan,sha]).Length==2,"Identical pairs generate two unique names");
        Check(Union([sha,shan],[jiu]).Length==0,"No common name means optional enchantment");
        Check(Union([sha,shan],[]).Length==0&&Union([],[sha,shan]).Length==0,"Normal cards break consecutive Fu plays");
        Check(Union([sha,shan],[sha.Replace("WEI_CAO_CAO_",""),jiu]).Length==3,"Clone names must match independent of character prefix");
        var fule=New("WeiCaoCaoLeCaoZhiFuLe");
        Check(fule.EnergyCost.Canonical==0&&fule.Type==CardType.Power&&fule.Rarity==CardRarity.Uncommon&&fule.MaxUpgradeLevel==0,"FuLe metadata");
        Check(fule.CanonicalKeywords.ToHashSet().SetEquals([CardKeyword.Innate]),"FuLe auto-start without unrequested Exhaust text/keyword");

        var binding=new Harmony("Yjcm.tests.fule-bindings");
        try
        {
            foreach(string name in new[]{"WeiCaoCaoFuDeferCost","WeiCaoCaoFuChoosePlay","WeiCaoCaoFuPlayable","WeiCaoCaoFuChooserTarget","WeiCaoCaoFuCardText","WeiCaoCaoGodEffects","WeiCaoCaoGodBodySelection","WeiCaoCaoGodJudgmentDisplay"})
            {
                binding.CreateClassProcessor(mod.GetType("Yjcm."+name,true)!).Patch();
                Check(true,"Production Harmony binding "+name);
            }
            var effects=mod.GetType("Yjcm.CardEffectVfx",true)!;
            Check(Invoke(effects,"Resolve","WeiCaoCaoShenHuangZhongLieCang",CardType.Attack)!=null,"God attack effect must resolve through Cao Cao's presentation bridge");
            Check(Invoke(effects,"Resolve","WeiCaoCaoShenPangTongFengLiaoYang",CardType.Skill)!=null,"God skill effect must resolve through Cao Cao's presentation bridge");
            Check(Invoke(effects,"Resolve","WeiCaoCaoSha",CardType.Attack)==null,"Common cards must not acquire an unrelated god effect");
            foreach (var (name, folder, clip) in new[]
            {
                ("WeiCaoCaoMouGuoJiaXianMouYang", "谋郭嘉阳", "jineng"),
                ("WeiCaoCaoMouGuoJiaXianMouYin", "谋郭嘉阴", "jineng"),
                ("WeiCaoCaoMouHuangQuanQiaoDui", "谋黄权", "gongji"),
                ("WeiCaoCaoZhouXuanWuMei", "周宣", "jineng"),
                ("WeiCaoCaoXiaHouXuanBoXuan", "夏侯玄", "jineng"),
                ("WeiCaoCaoShenDengAiTuoYu", "神邓艾", "gongji")
            })
            {
                var card = New(name);
                var effect = Invoke(effects, "Resolve", name, card.Type);
                Check(effect is ValueTuple<string,string,string> bundle && bundle.Item1 == folder && bundle.Item2 == clip &&
                    bundle.Item3.EndsWith(Path.Combine("Spine", "WeiCaoCao", "CardEffects"), StringComparison.Ordinal),
                    "Wrong Cao Cao effect asset or animation: " + name);
            }
            var judgments=mod.GetType("Yjcm.ShenCaiJudgmentPower",true)!;
            foreach(var pileType in new[]{PileType.Draw,PileType.Hand,PileType.Discard,PileType.Exhaust,PileType.Play})
            {
                var card=New("WeiCaoCaoShenZhangFeiShenCai");
                var pile=PileTypeExtensions.GetPile(pileType,Owner);
                Cards(pile).Add(card);
                Invoke(judgments,"SyncCardVariables",Owner,4);
                Check(card.DynamicVars["Judge"].BaseValue==4,"Cao Cao ShenCai display must update in "+pileType);
                Cards(pile).Clear();
            }
        }
        finally{binding.UnpatchAll(binding.Id);}

        var harmony=new Harmony("Yjcm.tests.fule");
        void Patch(MethodInfo target,string prefix)=>harmony.Patch(target,prefix:new HarmonyMethod(Method(typeof(FuLeCases),prefix)));
        bool oldTestMode=TestMode.IsOn;TestMode.IsOn=true;
        try
        {
            Patch(Method(Fu,"Resolve"),nameof(ResolveAlias));
            Patch(typeof(CardModel).GetMethod("CanPlay",[typeof(UnplayableReason).MakeByRefType(),typeof(AbstractModel).MakeByRefType()])!,nameof(CanPlay));
            Patch(typeof(CardModel).GetMethod("IsValidTarget")!,nameof(ValidTarget));
            Patch(typeof(CardModel).GetMethod("SpendResources")!,nameof(Spend));
            Patch(typeof(CardModel).GetMethod("OnPlayWrapper")!,nameof(Wrapper));
            Patch(typeof(CardSelectCmd).GetMethod("FromSimpleGrid")!,nameof(Select));
            Patch(typeof(CardSelectCmd).GetMethod("FromHand")!,nameof(SelectHand));
            Patch(typeof(CombatManager).GetProperty("IsOverOrEnding")!.GetMethod!,nameof(NotEnding));
            Patch(Method(Play,"SelectTarget"),nameof(Target));
            Patch(Method(Play,"RestoreHandVisual"),nameof(RestoreVisual));
            Patch(typeof(CardCmd).GetMethod("Enchant",[typeof(EnchantmentModel),typeof(CardModel),typeof(decimal)])!,nameof(EnchantCommand));
            Patch(typeof(CardPile).GetMethods().Single(m=>m.Name=="AddInternal"&&m.GetParameters().Length==3),nameof(AddRaw));

            bool Eligible(CardModel c,CardModel source)=>(bool)Invoke(Fu,"IsAliasCandidate",c,source);
            foreach(string starter in new[]{"WeiCaoCaoSha","WeiCaoCaoShan","WeiCaoCaoHuJia","WeiCaoCaoDuoYue"})
                Check(!Eligible(New(starter),fule),"Fu alias must exclude starter "+starter);
            Check(!Eligible(fule,New("WeiCaoCaoSha")),"Fu alias must also exclude FuLe itself");
            Check(!Eligible(fule,fule),"Fu cannot give the same original name");
            Check(!Eligible(New("WeiCaoCaoWuSeSha"),fule),"Fu must not discover token cards");
            var godTypes=Models.Values.Where(c=>c.GetType().Name.StartsWith("WeiCaoCaoShen")&&c.Rarity!=CardRarity.Token).ToArray();
            Check(godTypes.Length==14,"Cao Cao must include fourteen regular god-general cards after the Wei expansion");
            foreach(var god in godTypes) Check(Eligible(god,fule),"God-general card must be eligible for Fu alias "+god.GetType().Name);
            for(int n=1;n<=5;n++)
            {
                var values=new object?[]{New("WeiCaoCaoLieCangOption"+n),0};
                Method(mod.GetType("Yjcm.WeiCaoCaoGodBodySelection",true)!,"Postfix").Invoke(null,values);
                Check((int)values[1]! == n,"God Huang Zhong must retain body-selection point "+n);
            }
            var cloneMap=mod.GetType("Yjcm.WeiCaoCaoCloneMap",true)!;
            foreach(string name in new[]{"Sha","TieSuoLianHuan","ShenHuangZhongLieCang","ShenPangTongFengLiaoYin"})
                Check((Type)Invoke(cloneMap,"CloneType",mod.GetType("Yjcm.WeiDongZhuo"+name,true)!)==mod.GetType("Yjcm.WeiCaoCao"+name,true),"Generated/converted cards must retain Cao Cao identity: "+name);

            async Task PlayOnce(CardModel c,string pick,int energy,PileType result=PileType.Discard,bool cancel=false)
            {
                foreach(var p in Owner.Piles)Cards(p).Clear();Hand.Add(c);
                Pick=pick;Energy=energy;Spends=0;Played=null;Result=result;CancelTarget=cancel;
                HandVisualRepairs=0;FloatingCard=c;
                await (Task)Invoke(Play,"Resolve",Context,c,null,false,default(ResourceInfo),false);
            }
            var original=New("WeiCaoCaoSha");var fu=Enchant(original,Models.Values.Single(c=>c.GetType().Name=="WeiCaoCaoJueDou").Id.Entry);
            string costly=(string)Fu.GetProperty("AlternateEntry")!.GetValue(fu)!;
            await PlayOnce(original,costly,3);
            Check(Played?.Id.Entry==costly&&Energy==1&&Spends==1,"Alias must spend its 2 energy exactly once");
            Check(Owner.PlayerCombatState!.DiscardPile.Cards.Single()==original&&original.Enchantment==fu,"Alias play returns the original physical card and Fu to discard");
            Check(HandVisualRepairs==0,"Discarded card visuals must never be returned to the hand");
            await PlayOnce(original,original.Id.Entry,3);
            Check(ReferenceEquals(Played,original)&&Energy==2&&Spends==1,"Original branch must retain its own 1 cost and actual instance");
            await PlayOnce(original,costly,1);
            Check(ReferenceEquals(Played,original)&&Energy==0,"Unaffordable alias cannot be selected");
            await PlayOnce(original,costly,3,PileType.Exhaust);
            Check(Owner.PlayerCombatState!.ExhaustPile.Cards.Single()==original,"Chosen Exhaust branch must exhaust the physical card");
            Check(HandVisualRepairs==0,"Exhausted card visuals must not be returned");
            await PlayOnce(original,costly,3,PileType.None);
            Check(original.Pile==null&&original.HasBeenRemovedFromState,"Chosen Power/None branch removes the physical card");
            original=New("WeiCaoCaoSha");Enchant(original,costly);
            await PlayOnce(original,costly,3,cancel:true);
            Check(Energy==3&&Spends==0&&Hand.Contains(original),"Cancel target selection must not spend energy or lose a card");
            Check(HandVisualRepairs==1&&FloatingCard==null,"Cancelling target selection must explicitly return the lifted card visual");
            await PlayOnce(original,original.Id.Entry,3,cancel:true);
            Check(HandVisualRepairs==1&&FloatingCard==null&&Energy==3,"Original-face target cancellation must also clear the floating visual");
            await PlayOnce(original,original.Id.Entry,3);
            Check(ReferenceEquals(Played,original)&&Spends==1,"A cancelled Fu card must remain playable on the next attempt");
            await PlayOnce(original,costly,3,PileType.Hand);
            Check(HandVisualRepairs==1&&FloatingCard==null&&Hand.Single()==original,"A real return-to-hand must reconcile its original-face visual");
            await PlayOnce(original,costly,3,PileType.Play);
            Check(HandVisualRepairs==0&&original.Pile?.Type==PileType.Play,"Intentionally suspended Play cards must be left untouched");

            void Upgrade(CardModel c) {c.UpgradeInternal();c.FinalizeUpgradeInternal();}
            CardModel Preview(CardModel c)=>(CardModel)Invoke(Play,"AliasPreview",c);
            var dianLunDraw=New("WeiCaoCaoSha");Enchant(dianLunDraw,costly);
            int costRefreshes=0;
            dianLunDraw.EnergyCostChanged+=()=>costRefreshes++;
            Invoke(Play,"MakeFreeUntilPlayed",dianLunDraw);
            Check(costRefreshes==1 && dianLunDraw.EnergyCost.GetResolved()==0 && Preview(dianLunDraw).EnergyCost.GetResolved()==0,
                "DianLun immediately refreshes the hand cost and makes the alternate face free too");
            Check(!dianLunDraw.EnergyCost.EndOfTurnCleanup(),"DianLun's discount lasts until play across turns");
            await PlayOnce(dianLunDraw,costly,0,cancel:true);
            Check(Spends==0 && Preview(dianLunDraw).EnergyCost.GetResolved()==0,"Cancelling Fu preserves the DianLun discount");
            await PlayOnce(dianLunDraw,costly,0);
            Check(Played?.Id.Entry==costly && Spends==1 && Energy==0,"The chosen alternate can actually be played without energy");
            Check(dianLunDraw.EnergyCost.GetResolved()==1 && Preview(dianLunDraw).EnergyCost.GetResolved()==2,
                "The discount is consumed by playing the alternate face once");

            var upgraded=New("WeiCaoCaoSha");Enchant(upgraded,shan);
            Check(Preview(upgraded).CurrentUpgradeLevel==0&&Preview(upgraded).DynamicVars.Block.BaseValue==5,"Base Fu must offer the base alternate face");
            Upgrade(upgraded);
            var enhanced=Preview(upgraded);
            Check(enhanced.CurrentUpgradeLevel==1&&enhanced.DynamicVars.Block.BaseValue==8,"Upgrading an already-enchanted card must immediately upgrade the alternate");
            Check(Preview(upgraded).DynamicVars.Block.BaseValue==8,"Repeated previews must not stack the alternate's upgrade");
            await PlayOnce(upgraded,shan,3);
            Check(Played?.CurrentUpgradeLevel==1&&Played.DynamicVars.Block.BaseValue==8,"Actual alternate play must use upgraded effect values");
            Check(Owner.PlayerCombatState!.DiscardPile.Cards.Single()==upgraded&&upgraded.CurrentUpgradeLevel==1,"Upgraded original physical card must survive alternate play");
            var preupgraded=New("WeiCaoCaoSha");Upgrade(preupgraded);Enchant(preupgraded,shan);
            Check(Preview(preupgraded).CurrentUpgradeLevel==1,"Enchanting a card which is already upgraded must offer the upgraded alternate");
            var freeAlias=New("WeiCaoCaoSha");Upgrade(freeAlias);
            string huJia=Models.Values.Single(c=>c.GetType().Name=="WeiCaoCaoHuJia").Id.Entry;
            Enchant(freeAlias,huJia);
            Check(Preview(freeAlias).EnergyCost.GetResolved()==0,"Upgrade discount must be visible in the alternate preview");
            await PlayOnce(freeAlias,huJia,0);
            Check(Played?.Id.Entry==huJia&&Played.CurrentUpgradeLevel==1&&Energy==0&&Spends==1,"Zero-cost upgraded alternate must be playable with zero energy");
            var noUpgrade=New("WeiCaoCaoSha");Upgrade(noUpgrade);Enchant(noUpgrade,fule.Id.Entry);
            Check(Preview(noUpgrade).CurrentUpgradeLevel==0,"Alternate cards with no upgrades must remain base instead of throwing");
            var alternateDuo=New("WeiCaoCaoSha");Upgrade(alternateDuo);Enchant(alternateDuo,New("WeiCaoCaoDuoYue").Id.Entry);
            Check(Preview(alternateDuo).DynamicVars["Factor"].BaseValue==8,"Upgraded Fu DuoYue must inherit its upgraded damage factor");
            var alternateSheFu=New("WeiCaoCaoSha");Enchant(alternateSheFu,New("WeiCaoCaoCheZhouSheFu").Id.Entry);
            await alternateSheFu.Enchantment!.AfterFlush(Context,Owner,[],[alternateSheFu]);
            Check((int)Fu.GetProperty("AlternateRetainedTurns")!.GetValue(alternateSheFu.Enchantment)! ==1,"Fu SheFu must grow when its physical source is retained");
            var sheFuPreview=Preview(alternateSheFu);
            Check((decimal)sheFuPreview.GetType().GetProperty("CurrentDamage",F)!.GetValue(sheFuPreview)! ==12,"Alternate SheFu preview inherits physical retain count");
            Fu.GetProperty("AlternateReturnedThisTurn")!.SetValue(alternateSheFu.Enchantment,true);
            Check((bool)Preview(alternateSheFu).GetType().GetProperty("Returned")!.GetValue(Preview(alternateSheFu))!,"Alternate SheFu remembers its one return per turn");
            await alternateSheFu.Enchantment.AfterPlayerTurnStartEarly(Context,Owner);
            Check(!(bool)Fu.GetProperty("AlternateReturnedThisTurn")!.GetValue(alternateSheFu.Enchantment)! && (int)Fu.GetProperty("AlternateRetainedTurns")!.GetValue(alternateSheFu.Enchantment)! ==1,"New turn resets return limit without clearing retained damage");
            Fu.GetProperty("AlternatePlaysThisTurn")!.SetValue(original.Enchantment,2);
            var copy=(EnchantmentModel)original.Enchantment!.ClonePreservingMutability();
            Check((string)Fu.GetProperty("AlternateEntry")!.GetValue(copy)! == costly,"Fu clone must preserve alias");
            var restored=ModelDb.GetById<EnchantmentModel>(ModelDb.GetId(Fu)).ToMutable();
            Fu.BaseType!.GetProperty("AddedRetain",F)!.SetValue(original.Enchantment,true);
            Method(typeof(Program),"RoundTrip").Invoke(null,[original.Enchantment,restored]);
            Check((string)Fu.GetProperty("AlternateEntry")!.GetValue(restored)! == costly&&(int)Fu.GetProperty("AlternatePlaysThisTurn")!.GetValue(restored)! == 2,"Native save round-trip must preserve alias and per-card count");
            var restoredCard=New("WeiCaoCaoSha");restoredCard.EnchantInternal(restored,1);
            Method(Fu,"OnEnchant").Invoke(restored,null);
            Check(restoredCard.Keywords.Contains(CardKeyword.Retain),"Loaded Fu must restore Retain");

            var power=(PowerModel)Activator.CreateInstance(powerType)!;
            typeof(AbstractModel).GetProperty("IsMutable",F)!.SetValue(power,true);
            typeof(PowerModel).GetField("_owner",F)!.SetValue(power,Owner.Creature);
            Patch(Method(mod.GetType("Yjcm.WeiCaoCaoAudio",true)!,"Play"),nameof(Skip));
            Patch(Method(Fu,"Assign"),nameof(Assign));
            Patch(typeof(CardPileCmd).GetMethod("AddGeneratedCardToCombat")!,nameof(AddGenerated));
            var a=New("WeiCaoCaoSha");Enchant(a,shan);var b=New("WeiCaoCaoSha");Enchant(b,jiu);
            int Count()=>(int)powerType.GetProperty("CardsInGroup")!.GetValue(power)!;
            string Previous()=>(string)powerType.GetProperty("PreviousNames")!.GetValue(power)!;
            async Task Use(CardModel card) {var record=Record(card);await power.BeforeCardPlayed(record);await power.AfterCardPlayed(Context,record);}
            Generated=Assigned=HandChoices=0;
            // Deliberately use different CardPlay objects for the activation's two
            // hooks. Exclusion must depend on the card, not reference identity.
            await power.BeforeCardPlayed(Record(fule));await power.AfterCardPlayed(Context,Record(fule));
            Check(Count()==0&&Generated==0&&HandChoices==0,"FuLe activation must never occupy the first pair slot");
            Enchant(fule,sha);
            await Use(fule);
            Check(Count()==0,"Even an enchanted FuLe is excluded by actual card type");
            await Use(a);
            Check(Count()==1&&Generated==0&&HandChoices==0,"First card only records its names, with no WenLan settlement");
            string savedFirst=Previous();
            await Use(fule);
            Check(Count()==1&&Previous()==savedFirst,"Repeated FuLe neither counts nor destroys a recorded first card");
            await Use(b);
            Check(Generated==3&&Assigned==3,"WenLan must actually generate and enchant Sha, Shan and Jiu once each");
            Check(Count()==0&&Previous()=="","Giving cards must clear both pair count and recorded names");
            Generated=Assigned=0;
            await Use(a);
            Check(Count()==1&&Generated==0&&HandChoices==0,"After giving cards, one further card cannot trigger WenLan");
            var cross=New("WeiCaoCaoShan");Enchant(cross,jiu);
            await Use(cross);
            Check(Generated==3&&Assigned==3&&Count()==0,"A Fu B + B Fu C must actually give A/B/C and reset");

            Generated=Assigned=0;
            var normal=New("WeiCaoCaoShan");await Use(normal);
            Check(Count()==1&&Previous()==""&&HandChoices==0,"Normal card occupies a pair slot without any Fu names");
            await Use(a);
            Check(Generated==0&&HandChoices==1&&Count()==0&&Previous()=="","Normal plus Fu settles optional enchanting and resets even on skip");
            await Use(a);
            Check(HandChoices==1&&Count()==1,"A skipped optional enchant also requires two fresh cards");
            SelectOne=true;
            await Use(normal);
            SelectOne=false;
            Check(Assigned==1&&HandChoices==2&&Count()==0,"Refreshing an existing Fu must reset the complete pair");
            await Use(a);
            Check(Count()==1&&HandChoices==2&&Generated==0,"One card after refresh must not settle again");
            await Use(b);
            Check(Count()==0&&Generated==3,"The second fresh card may settle after a refresh");

            // Completed groups cannot leak nested auto-plays into the next group.
            await Use(a);Generated=0;
            DuringGeneration=async()=>
            {
                Check(Count()==2,"Hold the completed pair until settlement finishes");
                await Use(a);
                Check(Count()==2,"Nested play during WenLan cannot start another group");
            };
            await Use(b);
            Check(Count()==0&&Previous()==""&&Generated==3,"Nested settlement must end with a fully empty group");
            var repeated=Record(a,1,2);int choices=HandChoices;
            await power.BeforeCardPlayed(repeated);await power.AfterCardPlayed(Context,repeated);
            Check(Count()==0&&HandChoices==choices,"Native replay must not count as another card use");
            await Use(a);
            var restoredPower=(PowerModel)Activator.CreateInstance(powerType)!;
            typeof(AbstractModel).GetProperty("IsMutable",F)!.SetValue(restoredPower,true);
            Method(typeof(Program),"RoundTrip").Invoke(null,[power,restoredPower]);
            Check((int)powerType.GetProperty("CardsInGroup")!.GetValue(restoredPower)! == 1&&
                (string)powerType.GetProperty("PreviousNames")!.GetValue(restoredPower)! == Previous(),"Save must preserve one waiting card and all its names");
            powerType.GetProperty("CardsInGroup")!.SetValue(power,2);
            await Use(a);
            Check(Count()==1,"A restored in-flight flag without its pending play must recover instead of sticking");
            Console.WriteLine($"CAOCAO_FULE_OK assertions={Checks} selectedCost=true physicalCard=true pairReset=true fuleExcluded=true crossNameMatch=true save=true");
        }
        finally{harmony.UnpatchAll(harmony.Id);TestMode.IsOn=oldTestMode;}
    }
    private static bool ResolveAlias(string entry,ref CardModel? __result){__result=Models.GetValueOrDefault(entry);return false;}
    private static bool CanPlay(CardModel __instance,ref bool __result,ref UnplayableReason reason,ref AbstractModel? preventer)
    {__result=__instance.EnergyCost.GetResolved()<=Energy;reason=__result?UnplayableReason.None:UnplayableReason.BlockedByCardLogic;preventer=null;return false;}
    private static bool ValidTarget(ref bool __result){__result=true;return false;}
    private static bool Spend(CardModel __instance,ref Task<(int,int)> __result)
    {Spends++;int cost=__instance.EnergyCost.GetResolved();Energy-=cost;__result=Task.FromResult((cost,0));return false;}
    private static bool Wrapper(CardModel __instance,PlayerChoiceContext choiceContext,ResourceInfo resources,ref Task __result)
    {
        Check(ReferenceEquals(choiceContext,Context),"Play must keep the original synchronized context");Played=__instance;
        Check(resources.EnergySpent==__instance.EnergyCost.GetResolved(),"Native play must receive the chosen card's actual ResourceInfo");
        __instance.RemoveFromCurrentPile(true);if(Result!=PileType.None)Cards(Result.GetPile(Owner)).Add(__instance);
        __result=Task.CompletedTask;return false;
    }
    private static bool Select(PlayerChoiceContext context,IReadOnlyList<CardModel> cardsIn,ref Task<IEnumerable<CardModel>> __result)
    {Check(ReferenceEquals(context,Context),"Choice context changed");Selections++;__result=Task.FromResult<IEnumerable<CardModel>>([cardsIn.FirstOrDefault(c=>c.Id.Entry==Pick)??cardsIn[0]]);return false;}
    private static bool SelectHand(PlayerChoiceContext context,CardSelectorPrefs prefs,ref Task<IEnumerable<CardModel>> __result)
    {Check(ReferenceEquals(context,Context),"WenLan must use actual choice context");HandChoices++;__result=Task.FromResult<IEnumerable<CardModel>>(SelectOne?[Hand[0]]:[]);return false;}
    private static bool NotEnding(ref bool __result){__result=false;return false;}
    private static bool Target(ref Task<(bool,Creature?)> __result){__result=Task.FromResult<(bool,Creature?)>((!CancelTarget,null));return false;}
    private static bool RestoreVisual(CardModel source)
    {
        Check(source.Pile?.Type==PileType.Hand,"Visual repair must not modify gameplay card locations");
        HandVisualRepairs++;if(ReferenceEquals(FloatingCard,source))FloatingCard=null;return false;
    }
    private static bool EnchantCommand(EnchantmentModel enchantment,CardModel card,ref EnchantmentModel? __result)
    {card.EnchantInternal(enchantment,1);card.AddKeyword(CardKeyword.Retain);__result=enchantment;return false;}
    private static bool AddRaw(CardPile __instance,CardModel card,int index){Cards(__instance).Insert(Math.Clamp(index,0,Cards(__instance).Count),card);return false;}
    private static bool Skip()=>false;
    private static bool Assign(CardModel card){Assigned++;Enchant(card,Models.Keys.First(e=>e!=card.Id.Entry));return false;}
    private static bool AddGenerated(CardModel card,ref Task<CardPileAddResult> __result)
    {
        Generated++;Hand.Add(card);
        var nested=DuringGeneration;DuringGeneration=null;
        __result=FinishGenerated(card,nested);return false;
    }
    private static async Task<CardPileAddResult> FinishGenerated(CardModel card,Func<Task>? nested)
    {if(nested!=null)await nested();return new CardPileAddResult{success=true,cardAdded=card};}
}
