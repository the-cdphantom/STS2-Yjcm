// Generated equipment producer: keeps the original slot/effects, creates the clone token.
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

#nullable enable

namespace Yjcm;

/// <summary>
/// 丈八蛇矛（武器）：每回合开始时可以弃置两张手牌，然后将一张无色杀加入手牌，
/// 并选择一张攻击牌获得重放1（Spiral 附魔）。
/// 装备球占据武器栏，由 <see cref="EquipCmd.ChannelEquipment"/> 同类替换。
/// </summary>
[RegisterOrb]
public sealed class WeiCaoCaoZhangBaSheMaoOrb : EquipmentOrbModel
{
    public override EquipmentSlotType SlotType => EquipmentSlotType.Weapon;

    public override Color DarkenedColor => new("8A9A5B");

    private const string IconRes = "res://Yjcm/Images/Powers/zhang_ba_she_mao.png";

    // 弃牌提示（min=0，选 0 张直接确认即跳过，与维谷/贤贶的可跳过弃牌一致）
    private static readonly LocString SelectDiscardPrompt = new("cards", "zhang_ba_she_mao.select_discard");

    // 回合开始球触发可能发生在玩家正常摸牌之前（例如尽瘁的观星暂停了流程），
    // 因此先记下触发次数，等 AfterPlayerTurnStart 确认摸牌完成后再执行效果。
    private int _pendingTurnStartTriggers;

    public override OrbAssetProfile AssetProfile => new(IconPath: IconRes, VisualsScenePath: null);

    protected override Node2D? TryCreateOrbSprite() => EquipmentOrbSprite.CreateNormalized(IconRes);

    public override async Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
    {
        await base.AfterTurnStartOrbTrigger(choiceContext);

        // 没有尽瘁时，回合设置已经完成，保持原本的立即触发时机。
        // 尽瘁会在摸牌前打开观星选择，此时必须等 AfterPlayerTurnStart。
        if (Owner?.Creature.Powers.OfType<JinCuiPower>().Any() != true)
        {
            await TriggerTurnStartEffect(choiceContext);
            return;
        }

        _pendingTurnStartTriggers++;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStart(choiceContext, player);

        if (player != Owner || _pendingTurnStartTriggers <= 0)
            return;

        var triggerCount = _pendingTurnStartTriggers;
        _pendingTurnStartTriggers = 0;
        for (var i = 0; i < triggerCount; i++)
            await TriggerTurnStartEffect(choiceContext);
    }

    private async Task TriggerTurnStartEffect(PlayerChoiceContext choiceContext)
    {

        try
        {
            // 弃置两张手牌（minCount=0 允许跳过：选 0 张直接确认则整段跳过）
            // source 传 null：选择完成后 NPlayerHand.AfterCardsSelected 立即清空
            // 选中卡容器并把卡移回手牌，不依赖 source.ExecutionFinished 延迟触发
            //（若传 this，ExecutionFinished 由 RitsuLib 包装延迟触发，选中卡会
            //  残留在容器上，点击即抛 "Only valid when in Select Mode" 并显卡住）。
            var selected = await CardSelectCmd.FromHandForDiscard(
                choiceContext, Owner,
                new CardSelectorPrefs(SelectDiscardPrompt, 0, 2),
                null, null);
            if (selected == null || selected.Count() < 2) return;

            foreach (var card in selected)
                await CardCmd.Discard(choiceContext, card);

            // 将一张无色杀加入手牌
            var sha = Owner.Creature.CombatState.CreateCard<WeiCaoCaoWuSeSha>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(sha, PileType.Hand, Owner, CardPilePosition.Random);

            // 选择一张攻击牌获得重放1（该牌必须尚未附魔）
            // source 同样传 null（理由同上：立即清容器，防残留卡点击报错）
            var attacks = await CardSelectCmd.FromHand(
                choiceContext, Owner,
                new CardSelectorPrefs(new("cards", "zhang_ba_she_mao.select_attack"), 1, 1),
                c => c.Type == CardType.Attack && c.Enchantment == null, null);
            var attack = attacks.FirstOrDefault();
            if (attack == null) return;

            // Spiral 附魔 = 游戏内「重放」：打出时额外执行 Times 次（默认1）
            // 注意：CardCmd.Enchant 内部会做 CanEnchant 检查，而 Spiral 的 CanEnchant
            // 限定 Basic 稀有度 + Strike/Defend 标签，普通攻击牌会抛 "Cannot enchant"。
            // 因此直接走 EnchantInternal + ModifyCard（与 CardCmd.Enchant 内部一致，
            // 只是跳过 CanEnchant），让任意攻击牌都能获得重放附魔。
            var spiral = ModelDb.Enchantment<Spiral>().ToMutable();
            attack.EnchantInternal(spiral, 1m);
            spiral.ModifyCard();
            attack.FinalizeUpgradeInternal();
        }
        catch (NotImplementedException)
        {
            // 非交互上下文（如遗物触发），跳过选牌
        }
        catch (Exception ex)
        {
            // 防御性兜底：任何选牌/附魔异常都不外泄，
            // 避免中断回合开始流程导致卡住（0.1.55 曾因选牌交互异常卡在选择界面）。
            GD.Print($"[WuZhugeliang] 丈八蛇矛触发异常，已安全跳过本次效果：{ex}");
        }
    }
}
