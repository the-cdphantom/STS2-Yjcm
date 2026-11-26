using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _checks, _selections, _discards, _drawn, _spends, _attacks;
    private static int _replaysDuringAutoPlay;
    private static decimal _damage;
    private static CardModel? _choice;
    private static Func<Task>? _nestedDiscard;
    private static bool _skipSelection;
    private static Type PowerType = null!;
    private static PlayerChoiceContext Context = null!;
    private static readonly List<string> Events = [];

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        _checks++;
    }
    private static async Task Main(string[] args)
    {
        string dependencies = Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            string path = Path.Combine(dependencies, name.Name + ".dll");
            return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
        };
        var mod = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var huJiaType = mod.GetType("Yjcm.WeiCaoCaoHuJia", true)!;
        var duoYueType = mod.GetType("Yjcm.WeiCaoCaoDuoYue", true)!;
        var relicEffect = mod.GetType("Yjcm.WeiCaoCaoJunHeTurnEffect", true)!.GetMethod("Resolve", Flags)!;
        PowerType = mod.GetType("Yjcm.WeiCaoCaoJunHePower", true)!;
        var owner = Player();
        var teammate = Player();
        CheckSharedSettings(mod, teammate);
        CheckMerchant(mod, Path.GetFullPath(args[2]));
        CheckRestSiteAndHands(mod, Path.GetFullPath(args[2]));
        CheckWuZhugeliangBuffs(mod, Path.GetFullPath(args[2]), owner);
        CheckOrbFallbackScenes(mod, Path.GetFullPath(args[2]));
        CheckReviewCompatibility(mod, owner, teammate);
        var enemy = new Creature(null!, 1000, 1000);
        Context = new ThrowingPlayerChoiceContext();
        var duo = Card(duoYueType, owner);
        var huJia = Card(huJiaType, owner);
        var plays = duoYueType.GetProperty("PlaysThisTurn")!;
        var onPlay = duoYueType.GetMethod("OnPlay", Flags)!;
        var destination = duoYueType.GetMethod("GetResultLocationForCardPlay", Flags) ??
            duoYueType.GetMethod("GetResultPileTypeForCardPlay", Flags)!;
        int grants = 0;
        Task RelicTurn(Player player) => (Task)relicEffect.Invoke(null, [Context, owner, player, null, (Action)(() => grants++)])!;
        Check(duo.Type == CardType.Attack && duo.Rarity == CardRarity.Basic && duo.EnergyCost.Canonical == 1, "DuoYue starter metadata");
        Check(huJia.Type == CardType.Skill && huJia.Rarity == CardRarity.Basic && huJia.EnergyCost.Canonical == 1, "HuJia starter metadata");
        Check(huJia.CanonicalKeywords.Contains(CardKeyword.Exhaust) && !duo.CanonicalKeywords.Contains(CardKeyword.Exhaust), "Hu Jia has Exhaust; Duo Yue does not");
        var candidate = huJiaType.GetMethod("IsCandidate", Flags)!;
        Check((bool)candidate.Invoke(null, [duo, huJia])!, "HuJia should find the other Wei general");
        Check(!(bool)candidate.Invoke(null, [huJia, huJia])!, "HuJia must not discover itself recursively");
        Check(!(bool)candidate.Invoke(null, [Card(typeof(StrikeIronclad), owner), huJia])!, "HuJia must reject non-generals");
        var harmony = new Harmony("Yjcm.tests.caocao-starters");
        void Patch(MethodInfo target, string prefix) => harmony.Patch(target, prefix: new HarmonyMethod(typeof(Program).GetMethod(prefix, Flags)!));
        try
        {
            Patch(mod.GetType("Yjcm.WeiCaoCaoAudio", true)!.GetMethod("Play", Flags)!, nameof(Skip));
            var selectSound = mod.GetType("Yjcm.WeiCaoCaoSelectionAudio", true)!.GetMethod("Prefix", Flags)!;
            Check((bool)selectSound.Invoke(null, ["other-mod-or-vanilla-sound"])!, "Do not suppress other character sounds");
            Check(!(bool)selectSound.Invoke(null, ["Yjcm.WeiCaoCao.character_select"])!, "Selection WAV must replace native sound, not double-play");
            Patch(typeof(CardSelectCmd).GetMethod("FromHand")!, nameof(Select));
            Patch(typeof(CardSelectCmd).GetMethod("FromHandForDiscard")!, nameof(Select));
            Patch(typeof(CardCmd).GetMethod("Discard", [typeof(PlayerChoiceContext), typeof(IEnumerable<CardModel>)])!, nameof(Discard));
            Patch(typeof(CardPileCmd).GetMethods().Single(m => m.Name == "Draw" && m.GetParameters().Length == 4), nameof(Draw));
            Patch(PowerType.GetMethod("Gain")!, nameof(Gain));
            Patch(typeof(PowerCmd).GetMethod("ModifyAmount")!, nameof(Spend));
            Patch(typeof(AttackCommand).GetMethod("Execute")!, nameof(Attack));

            var attack = Card(typeof(StrikeIronclad), owner);
            var skill = Card(typeof(DefendIronclad), owner);
            var status = Card(typeof(Dazed), owner);
            var curse = Card(typeof(Regret), owner);
            Hand(owner).AddRange([attack, Card(typeof(StrikeIronclad), owner), skill, status, curse]);
            _choice = attack;
            await RelicTurn(teammate);
            Check(_selections == 0 && Amount(owner) == 0, "Teammate turn must not activate JunHe");
            await RelicTurn(owner);
            Check(Amount(owner) == 3 && Hand(owner).Count == 5 && _discards == 0, "JunHe must include the selected card, add one, and leave every card in hand");
            await RelicTurn(owner);
            Check(Amount(owner) == 6, "JunHe must accumulate across turns");
            _choice = status;
            await RelicTurn(owner);
            Check(Amount(owner) == 8, "Unplayable status cards remain valid type-count selections");
            _choice = curse;
            await RelicTurn(owner);
            Check(Amount(owner) == 10, "Curse card selection must count itself plus one");
            _skipSelection=true;await RelicTurn(owner);_skipSelection=false;
            Check(Amount(owner)==10 && grants==4,"Skipping JunHe must not grant resource or play trigger audio");
            Hand(owner).Clear();
            int beforeSelect = _selections;
            await RelicTurn(owner);
            Check(_selections == beforeSelect && Amount(owner) == 10, "Empty hand must not open a stuck selector");
            Check(grants == 4, "Only actual grants should flash/play trigger audio");
            SetAmount(owner,6);

            async Task Play(Creature target, int index = 0, int count = 1)
            {
                _choice = null;
                Hand(owner).Clear();
                Hand(owner).Add(Card(typeof(DefendIronclad), owner));
                typeof(CardModel).GetProperty("CurrentTarget", Flags)!.SetValue(duo, target);
                var location = destination.Invoke(duo, null)!;
                var resultPile = location is PileType pile ? pile :
                    (PileType)location.GetType().GetField("pileType")!.GetValue(location)!;
                Events.Clear();
                await (Task)onPlay.Invoke(duo, [Context, PlayRecord(duo, target, index, count)])!;
                int number = (int)plays.GetValue(duo)!;
                Check(Events[0] == "discard", "DuoYue must discard before drawing or attacking");
                Check(resultPile == (target == owner.Creature && number < 3 ? PileType.Hand : PileType.Discard), "Return only on the first two self plays");
            }
            await duo.BeforeCombatStart();
            await Play(owner.Creature);
            Check(_drawn == 1 && Amount(owner) == 6 && _spends == 0 && _attacks == 0, "First self play: draw one, retain all JunHe");
            Check(CurrentDraw(duo) == 2, "Card description must show next draw as two");
            await Play(owner.Creature);
            Check(_drawn == 3 && CurrentDraw(duo) == 3, "Second self play: draw two, preview next three");
            await Play(owner.Creature);
            Check(_drawn == 6 && _attacks == 0 && Amount(owner) == 6, "Third self play: draw three without attacking/spending");
            await duo.AfterPlayerTurnStartEarly(Context, teammate);
            Check((int)plays.GetValue(duo)! == 3, "Teammate start must not reset this card");
            await duo.AfterPlayerTurnStartEarly(Context, owner);
            Check((int)plays.GetValue(duo)! == 0 && CurrentDraw(duo) == 1 && Amount(owner) == 6, "Own turn reset must clear play count but preserve JunHe");

            await Play(owner.Creature);
            await Play(owner.Creature);
            int spentBefore = _spends;
            await Play(enemy);
            Check(_damage == 36 && _attacks == 1 && _spends == spentBefore + 1 && Amount(owner) == 5, "Third enemy play must deal 3*6*2, spend exactly one and not return");
            await duo.BeforeCombatStart();
            await Play(enemy);
            Check(_damage == 12 && Amount(owner) == 4, "First enemy play must count itself: 1*6*2");
            SetAmount(owner, 0);
            await duo.AfterPlayerTurnStartEarly(Context, owner);
            await Play(enemy);
            Check(_damage == 6 && Amount(owner) == 0, "No JunHe: normal damage with no penalty");

            duo.UpgradeInternal();
            Check(duo.DynamicVars["Factor"].BaseValue == 8 && duo.EnergyCost.Canonical == 1, "DuoYue upgrade changes only multiplier six to eight");
            await duo.AfterPlayerTurnStartEarly(Context, owner);
            await Play(owner.Creature);
            await Play(owner.Creature);
            SetAmount(owner, 1);
            await Play(enemy);
            Check(_damage == 48 && Amount(owner) == 0, "Upgraded third enemy play: 3*8*2 and consume last resource");
            await duo.AfterPlayerTurnStartEarly(Context, owner);
            SetAmount(owner, 2);
            Check(Multiplier(duo, enemy, owner.Creature, duo, ValueProp.Move) == 2, "Available resource must preview doubled damage");
            Check(Multiplier(duo, owner.Creature, owner.Creature, duo, ValueProp.Move) == 1, "Self mode must not receive damage amplification");
            Check(Multiplier(duo, enemy, teammate.Creature, duo, ValueProp.Move) == 1, "Damage multiplier must not leak to teammates");
            Check(Multiplier(duo, enemy, owner.Creature, skill, ValueProp.Move) == 1, "Damage multiplier must not leak to other cards");
            Check(Multiplier(duo, enemy, owner.Creature, duo, ValueProp.Unpowered) == 1, "Raw unpowered damage must remain unchanged");
            Check(Amount(owner) == 2, "Hovering and preview must never consume JunHe");

            var ancient = Card(mod.GetType("Yjcm.WeiCaoCaoJieDuoYue", true)!, owner);
            Check(ancient.Rarity == CardRarity.Ancient && ancient.EnergyCost.Canonical == 1, "Ancient DuoYue metadata");
            Check(CurrentDraw(ancient) == 2, "Ancient DuoYue first play previews two draws");
            plays.SetValue(ancient, 2);
            Check(CurrentDraw(ancient) == 4, "Ancient DuoYue third play previews four draws");
            ancient.UpgradeInternal();
            Check(ancient.DynamicVars["Factor"].BaseValue == 8 && Multiplier(ancient, enemy, owner.Creature, ancient, ValueProp.Move) == 3,
                "Ancient DuoYue upgrades to factor eight and previews triple JunHe damage");

            int before = _drawn;
            _nestedDiscard = () => duo.AfterPlayerTurnStartEarly(Context, teammate);
            await Play(owner.Creature);
            Check(_drawn == before + 1 && Amount(owner) == 2, "Discard side effects must finish in same context before own draw");
            var copy = Card(duoYueType, owner);
            Check((int)plays.GetValue(copy)! == 0 && CurrentDraw(copy) == 1, "Different copies must have independent counters");
            var currentCopy = Card(duoYueType, owner);
            plays.SetValue(currentCopy, 2);
            RoundTrip(currentCopy, copy);
            Check((int)plays.GetValue(copy)! == 2, "Native save must preserve this card's turn count");
            Check(typeof(SavedProperties).Assembly != null, "Use pinned game's save model");
            huJia.Owner = null!;
            huJia.UpgradeInternal();
            Check(huJia.EnergyCost.GetResolved() == 0, "HuJia upgrade must be free");

            var ningPao = Card(mod.GetType("Yjcm.WeiCaoCaoWeiCaoZhangNingPao", true)!, owner);
            var kuiPan = Card(mod.GetType("Yjcm.WeiCaoCaoWeiCaoZhangKuiPan", true)!, owner);
            Check(ningPao.Rarity == CardRarity.Rare && ningPao.Type == CardType.Skill && ningPao.EnergyCost.Canonical == 1,
                "NingPao is a one-energy rare skill");
            Check(kuiPan.Rarity == CardRarity.Rare && kuiPan.Type == CardType.Attack && kuiPan.EnergyCost.Canonical == 3 &&
                kuiPan.DynamicVars.Damage.BaseValue == 6, "KuiPan is a three-energy rare attack for six damage");
            ningPao.Owner = null!;
            kuiPan.Owner = null!;
            ningPao.UpgradeInternal();
            kuiPan.UpgradeInternal();
            Check(ningPao.EnergyCost.GetResolved() == 0 && kuiPan.EnergyCost.GetResolved() == 2, "Both Cao Zhang upgrades reduce cost by one");
            Patch(typeof(CardCmd).GetMethod("AutoPlay")!, nameof(CaptureAutoPlay));
            var extraReplay = mod.GetType("Yjcm.WeiCaoCaoFuPlay", true)!.GetMethod("PlayWithExtraReplays", Flags)!;
            var chosenAttack = Card(typeof(StrikeIronclad), owner);
            chosenAttack.BaseReplayCount = 1;
            await (Task)extraReplay.Invoke(null, [Context, chosenAttack, enemy, 2])!;
            Check(_replaysDuringAutoPlay == 3 && chosenAttack.BaseReplayCount == 1,
                "KuiPan adds teammates' replays to existing replays for one native autoplay only");

            var guiXin = Card(mod.GetType("Yjcm.WeiCaoCaoGuiXinXingTai", true)!, owner);
            var xieTianZi = Card(mod.GetType("Yjcm.WeiCaoCaoXieTianZi", true)!, owner);
            Check(guiXin.Rarity == CardRarity.Rare && guiXin.Type == CardType.Power && guiXin.EnergyCost.Canonical == 3,
                "GuiXin is a three-energy rare power");
            Check(xieTianZi.Rarity == CardRarity.Ancient && xieTianZi.Type == CardType.Skill && xieTianZi.EnergyCost.Canonical == 3,
                "XieTianZi is a three-energy ancient skill");
            guiXin.Owner = null!;
            xieTianZi.Owner = null!;
            guiXin.UpgradeInternal();
            xieTianZi.UpgradeInternal();
            Check(guiXin.Keywords.Contains(CardKeyword.Innate) && xieTianZi.EnergyCost.GetResolved() == 2,
                "GuiXin upgrades to Innate while XieTianZi upgrades to two energy");
            var intentSupport = mod.GetType("Yjcm.WeiCaoCaoMonsterIntentSupport", true)!;
            var buffType = intentSupport.GetNestedType("Buff", BindingFlags.NonPublic)!;
            var buffs = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(buffType))!;
            var roar = new MoveState("ROAR_MOVE", _ => throw new InvalidOperationException("Do not perform the monster action"), new BuffIntent());
            var raider = (BruteRubyRaider)RuntimeHelpers.GetUninitializedObject(typeof(BruteRubyRaider));
            var block = (decimal)intentSupport.GetMethod("Read", Flags)!.Invoke(null, [raider, roar, Array.Empty<Creature>(), buffs])!;
            Check(block == 0 && buffs.Count == 1 && Equals(buffType.GetProperty("Type")!.GetValue(buffs[0]), typeof(StrengthPower)) &&
                Equals(buffType.GetProperty("Amount")!.GetValue(buffs[0]), 3m), "Read the current buff intent without performing its move");

            string root = args[2];
            var cards = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mod/Experimental/WeiCaoCao/localization/cards.json"))).RootElement;
            string description = cards.GetProperty("YJCM_CARD_WEI_CAO_CAO_DUO_YUE.description").GetString()!;
            Check(description.Contains("（{CurrentDraw}张）") && description.Contains("\n[gold]军合[/gold]：伤害翻倍。") && !description.Contains("\\n"), "Approved concise description with gold resource keyword and real newline");
            Check(File.Exists(Path.Combine(root, "mod/Experimental/WeiCaoCao/images/jun_he.png")), "Generated relic must be in runtime assets");
            Console.WriteLine($"CAOCAO_STARTERS_OK assertions={_checks} draw=1/2/3 damage=18/36/48 inclusiveResource=true contextPreserved=true");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        MonsterIntentCases.Run(mod, Path.GetFileName(dependencies).StartsWith("beta-", StringComparison.Ordinal));
        await FuLeCases.Run(mod);
        await WeiDesignCases.Run(mod);
    }

    private static void CheckSharedSettings(Assembly mod, Player player)
    {
        var sweep = mod.GetType("Yjcm.SweepModePatch", true)!.GetMethod("IsModCharacter", Flags)!;
        var scope = mod.GetType("Yjcm.WuZhugeliangSettings", true)!.GetMethod("MatchesCharacterScope", Flags)!;
        var characterField = typeof(Player).GetField("<Character>k__BackingField", Flags)!;
        Check(!(bool)sweep.Invoke(null, [player])!, "Vanilla-only combat cannot trigger Yjcm sweep");
        foreach (var name in new[] { "WuZhugeliang", "WeiDongZhuo", "WeiCaoCao" })
        {
            var type = mod.GetType("Yjcm." + name, true)!;
            characterField.SetValue(player, RuntimeHelpers.GetUninitializedObject(type));
            Check((bool)sweep.Invoke(null, [player])!, $"{name} can control sweep");
            Check((bool)scope.Invoke(null, ["own_mod_characters", type])!, $"{name} qualifies for Yjcm-only shared relics");
            Check(!(bool)scope.Invoke(null, ["no_characters", type])!, $"{name} cannot receive disabled shared relics");
        }
        characterField.SetValue(player, new Ironclad());
    }

    private static void CheckMerchant(Assembly mod, string root)
    {
        string scene = Path.Combine(root, "mod/Experimental/WeiCaoCao/merchant.tscn");
        string script = Path.Combine(root, "mod/Experimental/WeiCaoCao/merchant.gd");
        Check(File.Exists(scene), "WeiCaoCao merchant scene must be in the source/pack tree");
        Check(File.Exists(script), "WeiCaoCao merchant script must be in the source/pack tree");
        Check(File.ReadAllText(scene).Contains("merchant.gd"), "Merchant scene must reference its own script");
        string characterSource = File.ReadAllText(
            Path.Combine(root, "mod/src/Experimental/WeiCaoCao/WeiCaoCao.cs"));
        Check(characterSource.Contains("CustomMerchantAnimPath")
              && characterSource.Contains("res://Experimental/WeiCaoCao/merchant.tscn"),
            "WeiCaoCao must override the native Ironclad merchant animation path");
        Check(characterSource.Contains("MerchantAnimPath:")
              && characterSource.Contains("res://Experimental/WeiCaoCao/merchant.tscn"),
            "WeiCaoCao asset profile must expose the merchant scene");
    }

    private static void CheckRestSiteAndHands(Assembly mod, string root)
    {
        string imageRoot = Path.Combine(root, "mod/Experimental/WeiCaoCao/images");
        foreach (string name in new[]
        {
            "weicaocao_hand_point.png", "weicaocao_hand_rock.png",
            "weicaocao_hand_paper.png", "weicaocao_hand_scissors.png",
        })
        {
            Check(File.Exists(Path.Combine(imageRoot, name)),
                $"WeiCaoCao multiplayer hand must exist: {name}");
        }

        string restImage = Path.Combine(imageRoot, "weicaocao_rest_site.png");
        Check(File.Exists(restImage), "WeiCaoCao v3 rest-site image must exist");
        Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(restImage))) ==
              "BF2F57A9A63F5BC80EAD4BBA5C6A8B78ADBAD1699F97652F06B8F21626AFE12C",
            "WeiCaoCao rest-site image must be the approved v3 asset");

        string scene = Path.Combine(root, "mod/Experimental/WeiCaoCao/rest_site.tscn");
        Check(File.Exists(scene), "WeiCaoCao rest-site scene must exist");
        string sceneText = File.ReadAllText(scene);
        foreach (string node in new[]
        {
            "ControlRoot", "Visuals", "Hitbox", "SelectionReticle",
            "ThoughtBubbleLeft", "ThoughtBubbleRight",
        })
        {
            Check(sceneText.Contains(node), $"Rest-site scene must expose {node}");
        }
        Check(sceneText.Contains("res://Experimental/WeiCaoCao/images/weicaocao_rest_site.png"),
            "Rest-site scene must use the approved CaoCao image");

        string source = File.ReadAllText(
            Path.Combine(root, "mod/src/Experimental/WeiCaoCao/WeiCaoCao.cs"));
        Check(source.Contains("RestSiteAnimPath: RestSiteScenePath")
              && source.Contains("CustomRestSiteAnimPath")
              && source.Contains("WeiCaoCaoRestSitePathPatch"),
            "WeiCaoCao must override and hard-fallback its rest-site path");
        Check(source.Contains("ArmPointingTexturePath")
              && source.Contains("ArmRockTexturePath")
              && source.Contains("ArmPaperTexturePath")
              && source.Contains("ArmScissorsTexturePath"),
            "WeiCaoCao must register all four multiplayer hand textures");
    }

    private static void CheckWuZhugeliangBuffs(Assembly mod, string root, Player owner)
    {
        static string Description(string file, string key)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.GetProperty(key).GetString()!;
        }

        string cards = Path.Combine(root, "mod/Yjcm/localization/zhs/cards.json");
        string relics = Path.Combine(root, "mod/Yjcm/localization/zhs/relics.json");
        string powers = Path.Combine(root, "mod/Yjcm/localization/zhs/powers.json");
        string caoCaoCards = Path.Combine(root, "mod/Experimental/WeiCaoCao/localization/cards.json");

        Check(Description(relics, "YJCM_RELIC_YJCM_QING_SHI.description").Contains("每项每回合限一次")
              && Description(relics, "YJCM_RELIC_YJCM_QING_SHI.description").Contains("摸3"),
            "QingShi description must describe per-option use and draw 3/disable");
        Check(Description(relics, "YJCM_RELIC_YJCM_JIE_QING_SHI.description").Contains("每种牌名限一次")
              && Description(relics, "YJCM_RELIC_YJCM_JIE_QING_SHI.description").Contains("3点能量"),
            "Refined QingShi description must describe per-name limit and 3 energy");
        Check(Description(cards, "YJCM_CARD_ZHI_ZHE.description").Contains("保留")
              && !Description(cards, "YJCM_CARD_ZHI_ZHE.description").Contains("固有")
              && !Description(cards, "YJCM_CARD_ZHI_ZHE.description").Contains("升级后"),
            "ZhiZhe description must not duplicate the native Innate label");
        Check(Description(cards, "YJCM_CARD_JIN_CUI_XING_TAI.description").Contains("摸至7")
              && Description(powers, "JIN_CUI_POWER.description").Contains("摸至7"),
            "JinCui card and power descriptions must mention hand/energy to 7");
        Check(Description(cards, "YJCM_CARD_ZHANG_YI_MU_RUI.description") ==
              "造成{Damage}点伤害。你的回合开始时，自动打出此牌。",
            "ZhangYi description must stay concise at turn start");
        Check(Description(cards, "YJCM_CARD_WO_LONG_ZHU_GE_LIANG_KAN_PO.description").Contains("{Block}"),
            "KanPo description must use the dynamic block value");
        Check(Description(cards, "YJCM_CARD_PANG_FENG_YI_YI_TONG.description").Contains("弃牌堆"),
            "YiTong description must mention draw and discard piles");
        Check(Description(cards, "YJCM_CARD_PANG_HONG_PING_ZHI_YANG.description").Contains("选择")
              && Description(cards, "YJCM_CARD_PANG_HONG_PING_ZHI_YANG.description").Contains("技能牌"),
            "PingZhi Yang must describe selecting a skill card");
        Check(Description(cards, "YJCM_CARD_PANG_HONG_PING_ZHI_YIN.description").Contains("选择")
              && Description(cards, "YJCM_CARD_PANG_HONG_PING_ZHI_YIN.description").Contains("攻击牌"),
            "PingZhi Yin must describe selecting an attack card");
        Check(Description(caoCaoCards, "YJCM_CARD_WEI_CAO_CAO_DUO_YUE.description").Contains("指定自己")
              && Description(caoCaoCards, "YJCM_CARD_WEI_CAO_CAO_DUO_YUE.description").Contains("指定敌人")
              && Description(caoCaoCards, "YJCM_CARD_WEI_CAO_CAO_DUO_YUE.description").Contains("至多两次")
              && Description(caoCaoCards, "YJCM_CARD_WEI_CAO_CAO_JIE_DUO_YUE.description").Contains("指定自己")
              && Description(caoCaoCards, "YJCM_CARD_WEI_CAO_CAO_JIE_DUO_YUE.description").Contains("至多两次"),
            "DuoYue descriptions must state the self/enemy branches and at-most-two returns");

        string designCards = Path.Combine(root, "mod/Experimental/WeiCaoCao/localization/wei-design-cards.json");
        string designPowers = Path.Combine(root, "mod/Experimental/WeiCaoCao/localization/wei-design-powers.json");
        Check(Description(designCards, "YJCM_CARD_WEI_CAO_CAO_XIA_HOU_XUAN_BO_XUAN.description")
                  .Contains("分别与刚使用的牌比较")
              && Description(designCards, "YJCM_CARD_WEI_CAO_CAO_XIA_HOU_XUAN_BO_XUAN.description")
                  .Contains("类型相同")
              && Description(designCards, "YJCM_CARD_WEI_CAO_CAO_XIA_HOU_XUAN_BO_XUAN.description")
                  .Contains("费用相同")
              && Description(designCards, "YJCM_CARD_WEI_CAO_CAO_XIA_HOU_XUAN_BO_XUAN.description")
                  .Contains("牌名字数相同"),
            "BoXuan card description must state three independent comparisons with the played card");
        Check(Description(designPowers, "YJCM_POWER_WEI_CAO_CAO_BO_XUAN_POWER.description")
                  .Contains("分别与刚使用的牌比较")
              && Description(designPowers, "YJCM_POWER_WEI_CAO_CAO_BO_XUAN_POWER.description")
                  .Contains("1点能量"),
            "BoXuan power description must match the rewritten comparison");

        var zhiZhe = Card(mod.GetType("Yjcm.ZhiZhe", true)!, owner);
        Check(zhiZhe.EnergyCost.Canonical == 0, "ZhiZhe must cost 0");
        Check(zhiZhe.CanonicalKeywords.Contains(CardKeyword.Exhaust)
              && zhiZhe.CanonicalKeywords.Contains(CardKeyword.Retain),
            "ZhiZhe must keep Exhaust and Retain");
        zhiZhe.UpgradeInternal();
        Check(zhiZhe.Keywords.Contains(CardKeyword.Innate),
            "ZhiZhe upgrade must add Innate");

        var kanPo = Card(mod.GetType("Yjcm.WoLongZhuGeLiangKanPo", true)!, owner);
        Check(kanPo.DynamicVars.Block.BaseValue == 10m, "KanPo base block must be 10");
        kanPo.UpgradeInternal();
        Check(kanPo.DynamicVars.Block.BaseValue == 15m, "KanPo upgraded block must be 15");

        string zhangYiSource = File.ReadAllText(
            Path.Combine(root, "mod/src/Patches/YjcmPatches.cs"));
        Check(zhangYiSource.Contains("PileType.Draw or PileType.Discard or PileType.Hand"),
            "ZhangYi source must auto-play from all three piles");
        string yiTongSource = File.ReadAllText(
            Path.Combine(root, "mod/src/Cards/PangFengYiYiTong.cs"));
        Check(yiTongSource.Contains("PileType.Draw.GetPile(Owner).Cards")
              && yiTongSource.Contains("PileType.Discard.GetPile(Owner).Cards"),
            "YiTong source must search draw and discard piles");
        string pingZhiSource = File.ReadAllText(
            Path.Combine(root, "mod/src/Cards/PangHongPingZhiYang.cs"));
        Check(pingZhiSource.Contains("CardSelectCmd.FromHand")
              && pingZhiSource.Contains("ping_zhi.attack_prompt"),
            "PingZhi must select a hand card instead of auto-playing randomly");

        foreach (string name in new[] { "QingShiDamageOption", "QingShiEnergyOption", "QingShiDrawOption" })
        {
            var optionType = mod.GetType("Yjcm." + name, true)!;
            if (!ModelDb.Contains(optionType)) ModelDb.Inject(optionType);
        }

        var qingShi = (RelicModel)Activator.CreateInstance(mod.GetType("Yjcm.YjcmQingShi", true)!)!;
        typeof(AbstractModel).GetProperty("IsMutable")!.SetValue(qingShi, true);
        qingShi.Owner = owner;
        var optionMethod = qingShi.GetType().BaseType!.GetMethod("CreateOptions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        int Options() => ((System.Collections.ICollection)optionMethod.Invoke(qingShi, null)!).Count;
        Check(Options() == 3, "QingShi must offer three options initially");
        var optionModels = ((System.Collections.IEnumerable)optionMethod.Invoke(qingShi, null)!)
            .Cast<object>()
            .Select(item => (CardModel)item.GetType().GetProperty("Model")!.GetValue(item)!)
            .ToArray();
        foreach (var option in optionModels)
        {
            Check(option.PortraitPath.Contains("zhi_zhe.png"),
                $"{option.GetType().Name} must use the ZhiZhe portrait");
        }
        var usedOptions = qingShi.GetType().BaseType!.GetField("_usedOptions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        usedOptions.SetValue(qingShi, 1);
        Check(Options() == 2, "QingShi must hide an already-used option");
        usedOptions.SetValue(qingShi, 7);
        Check(Options() == 0, "QingShi must hide every option after all three were used");
        usedOptions.SetValue(qingShi, 1);
        _ = qingShi.BeforeSideTurnStart(null!, CombatSide.Player, [owner.Creature], null!);
        Check(Options() == 3, "QingShi option usage must reset at turn start");
    }

    private static void CheckOrbFallbackScenes(Assembly mod, string root)
    {
        var equipmentBase = mod.GetType("Yjcm.EquipmentOrbModel", true)!;
        var orbTypes = mod.GetTypes()
            .Where(type => !type.IsAbstract && equipmentBase.IsAssignableFrom(type))
            .ToArray();
        Check(orbTypes.Length >= 20,
            "Every registered equipment orb must have a documented fallback scene");
        foreach (var type in orbTypes)
        {
            string id = "yjcm_orb_" +
                Regex.Replace(type.Name, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();
            string path = Path.Combine(root, "mod/scenes/orbs/orb_visuals", id + ".tscn");
            Check(File.Exists(path), $"{type.Name} fallback scene must exist: {id}.tscn");
            if (!File.Exists(path)) continue;
            string text = File.ReadAllText(path);
            Check(text.Contains("SpineSkeleton")
                  && text.Contains("preview_animation = \"idle_loop\"")
                  && text.Contains("skeleton_data_res = ExtResource"),
                $"{type.Name} fallback scene must contain a real idle_loop SpineSkeleton");
            var scaleMatch = Regex.Match(text, @"scale = Vector2\(([0-9.]+),");
            Check(scaleMatch.Success, $"{type.Name} fallback scene must set an icon scale");
            if (!scaleMatch.Success) continue;
            double scale = double.Parse(scaleMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            Check(scale >= 0.09, $"{type.Name} icon scale must not collapse to invisible");
            if (id.Contains("empty", StringComparison.Ordinal))
                Check(Math.Abs(scale - 0.5) < 0.000001, $"{type.Name} empty-slot icon must use 0.5 scale");
            if (type.Name.Contains("ZhangBaSheMaoOrb", StringComparison.Ordinal))
                Check(Math.Abs(scale - 1.283333) < 0.00001,
                    $"{type.Name} must match the original ZhangBa scale");
        }
    }

    private static void CheckReviewCompatibility(Assembly mod, Player owner, Player teammate)
    {
        int before = _checks;
        Type Model(string name) => mod.GetType("Yjcm." + name, true)!;
        var tag = (CardTag)Model("YjcmCardTags").GetField("WuJiang", Flags)!.GetValue(null)!;
        var god = (CardTag)Model("YjcmCardTags").GetField("ShenJiang", Flags)!.GetValue(null)!;
        var bonus = Model("ZhangLuBuDaoFan").GetMethod("GetShaBonus", Flags)!;
        decimal Bonus(CardModel card, Creature? dealer, ValueProp props = ValueProp.Move) =>
            (decimal)bonus.Invoke(null, [owner, props, dealer, card])!;
        var sha = Card(Model("WeiCaoCaoSha"), owner);
        var deck = Cards(owner.Deck);
        deck.AddRange([sha, Card(Model("WeiCaoCaoHuJia"), owner), Card(Model("WeiCaoCaoShenZhaoYunLongHun"), owner)]);
        foreach (var name in new[] { "Sha", "WeiDongZhuoSha", "WeiCaoCaoSha" })
            Check(Bonus(Card(Model(name), owner), owner.Creature) == 2, $"{name} counts both general cards, once each");
        Check(Bonus(sha, null) == 2, "BuDaoFan falls back to the owning card when the dealer is absent");
        Check(Bonus(Card(Model("WeiCaoCaoSha"), teammate), teammate.Creature) == 0, "BuDaoFan does not boost a teammate's Sha");
        Check(Bonus(sha, owner.Creature, ValueProp.Move | ValueProp.Unpowered) == 0, "BuDaoFan does not boost unpowered damage");
        Check(Bonus(Card(Model("WeiCaoCaoWuSeSha"), owner), owner.Creature) == 0, "BuDaoFan only boosts the ordinary Sha family");
        deck.Clear();

        var api = mod.GetType("Yjcm.Api.YjcmPublicApi", true)!.GetMethod("IsYjcmCharacter")!;
        foreach (var name in new[] { "WuZhugeliang", "WeiDongZhuo", "WeiCaoCao" })
            Check((bool)api.Invoke(null, [RuntimeHelpers.GetUninitializedObject(Model(name))])!, $"Public API recognizes {name}");
        Check(!(bool)api.Invoke(null, [owner.Character])! && !(bool)api.Invoke(null, [null])!,
            "Public API rejects an original-game character and null");

        // Render localization keys in isolation. Exercise both production postfixes with native hover-tip objects.
        var harmony = new Harmony("Yjcm.tests.caocao-compatibility");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(LocString), nameof(LocString.GetFormattedText)),
                prefix: new HarmonyMethod(typeof(Program).GetMethod(nameof(UseLocKey), Flags)!));
            var factionPostfix = Model("WuJiangExtraHoverTipsPatch").GetMethod("Postfix")!;
            var keywordPostfix = Model("LeBuSiShuHoverTipsPatch").GetMethod("Postfix")!;
            IEnumerable<IHoverTip> Apply(MethodInfo postfix, CardModel card, params IHoverTip[] existing)
            {
                object[] args = [card, existing];
                postfix.Invoke(null, args);
                return (IEnumerable<IHoverTip>)args[1];
            }

            var sentinel = new HoverTip(new LocString("test", "existing_hint"));
            var designType = Model("WeiCaoCaoDesignCard");
            var copies = mod.GetTypes()
                .Where(type => !type.IsAbstract && type.IsSubclassOf(Model("WeiCaoCaoCard")) && !type.IsSubclassOf(designType))
                .Select(type => Card(type, owner))
                .Where(card => card.Tags.Contains(tag) && card.Tags.Contains(god))
                .ToArray();
            Check(copies.Length == 12, "Test must include all twelve copied god cards");
            foreach (var copy in copies)
            {
                var tips = Apply(factionPostfix, copy, sentinel).ToArray();
                Check(tips.Length == 2 && tips[0].Equals(sentinel) && tips[1].Id.Contains("YJCM_HOVER_WU_JIANG_SHEN.description"),
                    $"{copy.GetType().Name} appends exactly one god faction tip and keeps its other tips");
            }
            foreach (var name in new[] { "WeiCaoCaoSha", "WeiCaoCaoHuJia", "WeiCaoCaoShenDengAiTuoYu", "WeiCaoCaoShenDianWeiCheXie" })
                Check(Apply(factionPostfix, Card(Model(name), owner), sentinel).SequenceEqual(new IHoverTip[] { sentinel }),
                    $"{name} must not acquire an extra faction tip");

            var exhaust = HoverTipFactory.FromKeyword(CardKeyword.Exhaust);
            foreach (var name in new[] { "LeBuSiShu", "WeiDongZhuoLeBuSiShu", "WeiCaoCaoLeBuSiShu" })
            {
                var card = Card(Model(name), owner);
                Check(Apply(keywordPostfix, card, sentinel, exhaust).SequenceEqual(new IHoverTip[] { sentinel }),
                    $"{name} hides the implicit Exhaust tip only");
                card.AddKeyword(CardKeyword.Exhaust);
                Check(Apply(keywordPostfix, card, sentinel, exhaust).SequenceEqual(new IHoverTip[] { sentinel, exhaust }),
                    $"{name} keeps the tooltip when it genuinely Exhausts");
            }
            Check(Apply(keywordPostfix, sha, sentinel, exhaust).SequenceEqual(new IHoverTip[] { sentinel, exhaust }),
                "The LeBuSiShu filter does not affect other cards");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine($"CAOCAO_COMPATIBILITY_OK assertions={_checks - before}");
    }

    private static bool UseLocKey(LocString __instance, ref string __result)
    {
        __result = __instance.LocEntryKey;
        return false;
    }

    private static bool Skip() => false;
    private static List<CardModel> Hand(Player player) => Cards(player.PlayerCombatState!.Hand);
    private static List<CardModel> Cards(CardPile pile) => (List<CardModel>)typeof(CardPile).GetField("_cards", Flags)!.GetValue(pile)!;
    private static Player Player()
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var combat = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        typeof(Player).GetProperty("PlayerCombatState")!.SetValue(player, combat);
        typeof(Player).GetField("<Creature>k__BackingField", Flags)!.SetValue(player, new Creature(player, 1000, 1000));
        typeof(Player).GetField("<Character>k__BackingField", Flags)!.SetValue(player, new Ironclad());
        typeof(Player).GetField("<Deck>k__BackingField", Flags)!.SetValue(player, new CardPile(PileType.Deck));
        typeof(Player).GetField("_relics", Flags)!.SetValue(player, new List<RelicModel>());
        foreach (var (name, type) in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            typeof(PlayerCombatState).GetField("<" + name + ">k__BackingField", Flags)!.SetValue(combat, new CardPile(type));
        return player;
    }
    private static CardModel Card(Type type, Player owner)
    {
        var card = (CardModel)Activator.CreateInstance(type)!;
        typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(card, true);
        card.Owner = owner;
        return card;
    }
    private static CardPlay PlayRecord(CardModel card, Creature target, int index, int count)
    {
        var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
        foreach (var (name, value) in new (string, object?)[] { ("Card", card), ("Target", target), ("PlayIndex", index), ("PlayCount", count), ("ResultPile", PileType.Discard), ("Resources", default(ResourceInfo)), ("IsAutoPlay", false) })
            typeof(CardPlay).GetProperty(name)!.SetValue(play, value);
        typeof(CardPlay).GetProperty("Player")?.SetValue(play, card.Owner);
        return play;
    }
    private static int CurrentDraw(CardModel card)
    {
        var loc = new LocString("cards", "probe");
        card.GetType().GetMethod("AddExtraArgsToDescription", Flags)!.Invoke(card, [loc]);
        return Convert.ToInt32(loc.Variables["CurrentDraw"]);
    }
    private static bool Select(PlayerChoiceContext context, Player player, CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
    {
        Check(ReferenceEquals(context, Context), "Selection must keep native synchronized context");
        Check(prefs.MinSelect is 0 or 1 && prefs.MaxSelect == 1, "Manual selector must allow the optional relic choice and required discard choice");
        _selections++;
        __result = Task.FromResult<IEnumerable<CardModel>>(_skipSelection?[]:[_choice ?? Hand(player).First()]);
        return false;
    }
    private static bool Discard(PlayerChoiceContext choiceContext, IEnumerable<CardModel> cards, ref Task __result)
    {
        Check(ReferenceEquals(choiceContext, Context), "Discard context changed");
        foreach (var card in cards) { Hand(card.Owner).Remove(card); _discards++; }
        Events.Add("discard");
        var nested = _nestedDiscard; _nestedDiscard = null;
        __result = nested?.Invoke() ?? Task.CompletedTask;
        return false;
    }
    private static bool Draw(PlayerChoiceContext choiceContext, decimal count, ref Task<IEnumerable<CardModel>> __result)
    {
        Check(ReferenceEquals(choiceContext, Context), "Draw context changed");
        _drawn += (int)count;
        Events.Add("draw:" + count);
        __result = Task.FromResult(Enumerable.Empty<CardModel>());
        return false;
    }
    private static List<PowerModel> Powers(Creature owner) => (List<PowerModel>)typeof(Creature).GetField("_powers", Flags)!.GetValue(owner)!;
    private static bool CaptureAutoPlay(CardModel card, ref Task __result)
    {
        _replaysDuringAutoPlay = card.BaseReplayCount;
        __result = Task.CompletedTask;
        return false;
    }
    private static int Amount(Player player) => Powers(player.Creature).FirstOrDefault(p => p.GetType() == PowerType)?.Amount ?? 0;
    private static void SetAmount(Player player, int amount)
    {
        var list = Powers(player.Creature);
        list.RemoveAll(p => p.GetType() == PowerType);
        if (amount <= 0) return;
        var power = (PowerModel)Activator.CreateInstance(PowerType)!;
        typeof(AbstractModel).GetProperty("IsMutable", Flags)!.SetValue(power, true);
        typeof(PowerModel).GetField("_owner", Flags)!.SetValue(power, player.Creature);
        typeof(PowerModel).GetField("_amount", Flags)!.SetValue(power, amount);
        list.Add(power);
    }
    private static bool Gain(PlayerChoiceContext context, Creature owner, int amount, ref Task __result)
    {
        Check(ReferenceEquals(context, Context), "Gain context changed");
        SetAmount(owner.Player!, Amount(owner.Player!) + amount);
        __result = Task.CompletedTask;
        return false;
    }
    private static bool Spend(PlayerChoiceContext choiceContext, PowerModel power, decimal offset, ref Task<int> __result)
    {
        Check(ReferenceEquals(choiceContext, Context), "Spend context changed");
        Check(power.Amount >= -offset && offset == -1, "Auto boost must spend exactly one available resource");
        _spends++;
        int next = power.Amount + (int)offset;
        SetAmount(power.Owner.Player!, next);
        Events.Add("spend");
        __result = Task.FromResult(next);
        return false;
    }
    private static decimal Multiplier(CardModel card, Creature target, Creature dealer, CardModel source, ValueProp props)
    {
        var method = card.GetType().GetMethod("ModifyDamageMultiplicative")!;
        return (decimal)method.Invoke(card, method.GetParameters().Length == 6
            ? [target, 100m, props, dealer, source, null] : [target, 100m, props, dealer, source])!;
    }
    private static bool Attack(AttackCommand __instance, PlayerChoiceContext choiceContext, ref Task<AttackCommand> __result)
    {
        Check(ReferenceEquals(choiceContext, Context), "Attack context changed");
        var card = (CardModel)__instance.ModelSource!;
        var target = (Creature)typeof(AttackCommand).GetField("_singleTarget", Flags)!.GetValue(__instance)!;
        decimal raw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", Flags)!.GetValue(__instance)!;
        _damage = raw * Multiplier(card, target, card.Owner.Creature, card, ValueProp.Move);
        _attacks++;
        Events.Add("attack");
        __result = Task.FromResult(__instance);
        return false;
    }
    private static void RoundTrip(AbstractModel from, AbstractModel to)
    {
        var cache = typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") ??
            typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        var inject = cache.GetMethod("InjectTypeIntoCache");
        (inject ?? cache.GetMethod("CacheSavedPropertiesForTypeDebug")!).Invoke(null, [from.GetType()]);
        var gate = inject == null ? cache.GetField("_initialized", Flags) : null;
        bool old = gate != null && (bool)gate.GetValue(null)!;
        if (gate != null) gate.SetValue(null, true);
        try
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.FromInternal(from, null), options), options)!.FillInternal(to);
        }
        finally { if (gate != null) gate.SetValue(null, old); }
    }
}
