using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Yjcm;

/// <summary>
/// 多人宝藏室手势贴图位置：武诸葛亮的四张手势图在原版手势节点中整体偏下，
/// 初始化后按贴图高度上移 15%。只处理武诸葛亮，不影响其他角色。
/// </summary>
[HarmonyPatch(typeof(NHandImage), "_Ready")]
public static class WuZhugeliangMultiplayerHandPositionPatch
{
    private const float MoveUpRatio = 0.15f;

    static void Postfix(NHandImage __instance)
    {
        if (__instance.Player?.Character is not WuZhugeliang)
            return;

        var textureRect = AccessTools.Field(typeof(NHandImage), "_textureRect")
            ?.GetValue(__instance) as TextureRect;
        var textureHeight = textureRect?.Texture?.GetHeight() ?? 0;
        if (textureRect == null || textureHeight <= 0)
            return;

        textureRect.Position += Vector2.Up * (textureHeight * MoveUpRatio);
    }
}

/// <summary>
/// 卡面数值跟随力量/敏捷动态变化
/// get_IntValue 覆盖：ToString() 等场景使用
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar), "get_IntValue")]
public static class DynamicVarIntValuePatch
{
    static bool Prefix(DynamicVar __instance, ref int __result)
    {
        // 返回PreviewValue（包含力量/敏捷修正），而非BaseValue
        __result = (int)__instance.PreviewValue;
        return false; // 跳过原方法
    }
}

/// <summary>
/// 卡面数值跟随力量/敏捷动态变化
/// GetBaseValueForIConvertible 覆盖：SmartFormat 渲染（{Damage}/{Block}）使用此路径
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar), "GetBaseValueForIConvertible")]
public static class DynamicVarConvertiblePatch
{
    static bool Prefix(DynamicVar __instance, ref decimal __result)
    {
        // 返回PreviewValue，使卡面 {Damage} 标签显示包含力量/敏捷修正后的值
        __result = __instance.PreviewValue;
        return false; // 跳过原方法
    }
}

/// <summary>
/// 尽瘁BGM停止：战斗结束/放弃时停止
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Rooms.CombatRoom), "OnCombatEnded")]
public static class JinCuiBgmStopCombatPatch
{
    static void Postfix() => JinCuiPower.StopBgm();
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Runs.RunManager), "Abandon")]
public static class JinCuiBgmStopAbandonPatch
{
    static void Postfix() => JinCuiPower.StopBgm();
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.NRun), "SetCurrentRoom")]
public static class JinCuiBgmStopRoomChangePatch
{
    static void Postfix(Godot.Control? node)
    {
        // 离开战斗房间时停止BGM（非战斗房间则停止）
        if (node == null || !node.GetType().Name.Contains("Combat"))
            JinCuiPower.StopBgm();
    }
}

/// <summary>
/// 转换技关键词自动消耗：拦截 GetResultPileTypeForCardPlay（0.107.1 返回 PileType），
/// 如果卡牌包含转换技关键词，强制返回消耗堆。
/// 注解挂载：Loader 选择版本程序集后调用真实 MainFile 的统一幂等入口，
/// 由 MainFile 挂载补丁，避免 Loader 与内容程序集重复 PatchAll。
/// </summary>
#if GAME_BETA
[HarmonyPatch(typeof(CardModel), "GetResultLocationForCardPlay")]
#else
[HarmonyPatch(typeof(CardModel), "GetResultPileTypeForCardPlay")]
#endif
public static class ZhuanHuanJiExhaustPatch
{
#if GAME_BETA
    static void Postfix(CardModel __instance, ref CardLocation __result)
#else
    static void Postfix(CardModel __instance, ref PileType __result)
#endif
    {
#if GAME_BETA
        if (__result.pileType == PileType.Discard && __instance.Keywords.Contains(YjcmKeywords.ZhuanHuanJi))
            __result.pileType = PileType.Exhaust;
#else
        if (__result == PileType.Discard && __instance.Keywords.Contains(YjcmKeywords.ZhuanHuanJi))
            __result = PileType.Exhaust;
#endif
    }
}



/// <summary>
/// 知机-穿甲：当伤害来源拥有知机穿甲能力时，伤害无视格挡。
/// 0.107.1 适配：Damage 6 参重载为 (ctx, target, amount, props, dealer, cardSource)，无 cardPlay。
/// 同时挂单目标与多目标重载：攻击牌走 AttackCommand → 多目标重载（targets 列表），两类都要覆盖。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
#if GAME_BETA
    typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay))]
#else
    typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
#endif
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
#if GAME_BETA
    typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]
#else
    typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
#endif
public static class ZhiJiPiercePatch
{
    static void Prefix(ref ValueProp props, CardModel? cardSource)
    {
        if (cardSource?.Owner?.Creature?.Powers?.OfType<ZhiJiPiercePower>().Any() == true)
            props |= ValueProp.Unblockable;
    }
}

/// <summary>
/// 青钢剑：装备青钢剑时，玩家的所有攻击牌无视格挡。
/// 通过检测武器栏中是否装有青钢剑球实现——装备/替换自动生效，无需生命周期管理。
/// 0.107.1 适配：同 ZhiJiPiercePatch；攻击牌走多目标重载，必须覆盖。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
#if GAME_BETA
    typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay))]
#else
    typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
#endif
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
#if GAME_BETA
    typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]
#else
    typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
#endif
public static class QingGangJianIgnoreBlockPatch
{
    static void Prefix(ref ValueProp props, CardModel? cardSource)
    {
        // 仅玩家的攻击牌生效
        if (cardSource?.Type != CardType.Attack) return;
        if (cardSource.Owner?.PlayerCombatState?.OrbQueue?.Orbs
                ?.OfType<QingGangJianOrb>().Any() == true)
            props |= ValueProp.Unblockable;
    }
}

/// <summary>
/// 暮锐：回合开始时，将弃牌堆中的暮锐牌自动打出。
///
/// 这个效果必须从 AfterPlayerTurnStart（一个会被战斗流程 await 的钩子）调用。
/// 之前通过 Harmony 给 CombatManager.SetupPlayerTurn 加 async void Postfix，
/// 原方法返回后游戏就会继续生成回合校验和，而暮锐还在异步执行，导致主机和
/// 客户端的牌堆/敌人生命值在多人模式下分叉。
/// </summary>
public static class MuRuiPatch
{
    internal static async Task AutoPlayAtTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead)
            return;

        var discardPile = PileTypeExtensions.GetPile(PileType.Discard, player);
        if (discardPile?.Cards == null) return;

        var muRuiCards = discardPile.Cards
            .Where(c => c is ZhangYiMuRui)
            .ToList();

        foreach (var card in muRuiCards)
        {
            // 前一张暮锐可能已经击杀目标；每次重新读取可攻击目标，避免向已失效
            // 的 Creature 发起自动出牌。
            var enemies = player.Creature.CombatState?.HittableEnemies.ToList();
            Creature? target = null;
            if (enemies != null && enemies.Count > 0)
                target = player.RunState.Rng.CombatCardSelection.NextItem(enemies);

            if (target != null)
                await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, false, false);
        }
    }
}

/// <summary>
/// 欧洛巴斯之触升级：情势 → 界情势（mod 自实现）。
/// 绕过 RitsuLib 的 TouchOfOrobasGetUpgradedStarterRelicPatch（该 patch 在 0.111.0
/// 未生效，导致图鉴 Starter 组"升级版"子分类不显示界情势、且持有情势获得
/// 欧洛巴斯之触后无法升级）。图鉴子分类与 SetupForPlayer 都调用
/// TouchOfOrobas.GetUpgradedStarterRelic，此补丁一并修复两处。
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
public static class OrobasRefinementPatch
{
    /// <summary>情势 → 界情势；其余交还游戏原逻辑（含原版起始遗物升级）</summary>
    static bool Prefix(RelicModel starterRelic, ref RelicModel __result)
    {
#if GAME_BETA
        MainFile.Logger.Info($"[升级] GetUpgradedStarterRelic 被调用, starter={starterRelic?.Id.Entry ?? "null"}, 是情势={starterRelic is YjcmQingShi}");
#else
#endif
        if (starterRelic is YjcmQingShi)
        {
            __result = ModelDb.Relic<YjcmJieQingShi>().ToMutable();
#if GAME_BETA
            MainFile.Logger.Info($"[升级] 情势→界情势: {__result.Id.Entry}");
#else
#endif
            return false;
        }
        return true;
    }
}

/// <summary>
/// 临时排查：图鉴遗物加载日志（确认界情势在 Starter 组的存在与数量）
/// </summary>
[HarmonyPatch(typeof(NRelicCollectionCategory), "LoadRelics")]
public static class RelicCollectionLogPatch
{
    static void Postfix(NRelicCollectionCategory __instance, RelicRarity relicRarity)
    {
        try
        {
            var cache = (System.Collections.Generic.IEnumerable<RelicModel>?)AccessTools.Field(typeof(NRelicCollectionCategory), "_relicModelCache").GetValue(__instance);
            if (cache == null) return;
            var list = cache.ToList();
            var jie = list.FirstOrDefault(r => r is YjcmJieQingShi);
            var qing = list.FirstOrDefault(r => r is YjcmQingShi);
            MainFile.Logger.Info($"[图鉴] LoadRelics rarity={relicRarity} 数量={list.Count} 含情势={qing != null} 含界情势={jie != null}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[图鉴] 日志失败: {ex.Message}");
        }
    }
}
