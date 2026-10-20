using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using Yjcm.Interop;

namespace Yjcm;

/// <summary>
/// 一枕清梦：2费稀有技能牌。触发所有敌人的火攻2次，回合结束敌人回复剩余火攻生命并失去火攻。
/// 龙套演员使用时，上述火攻全部替换为灼烧。升级后1费。
/// </summary>
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class YiZhenQingMeng : WuZhugeliangCard
{
    public YiZhenQingMeng()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    // ※模组联动只作为额外悬停提示，不写入牌面。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [..YiZhenQingMengInterop.EffectHoverTips(this),
         HoverTipFactory.FromPower<XingPower>(),
         ..YiZhenQingMengInterop.LibraryHoverTips()];

    private static readonly string VoicePath = Path.Combine(
        Path.GetDirectoryName(typeof(YiZhenQingMeng).Assembly.Location)!,
        "Voice", "Bank", "Assets", "yi_zhen_qing_meng.wav");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        VoicePlayer.PlayWav(VoicePath);
        bool useBurning = YiZhenQingMengInterop.Affects(Owner);

        // 龙套演员改为给予灼烧；其他角色保持原火攻。
        foreach (var enemy in CombatState.HittableEnemies)
            await YiZhenQingMengInterop.ApplyEffect(
                choiceContext, enemy, 7, Owner.Creature, this, useBurning);

        // 触发所有敌人的对应能力2次。灼烧交由孙乾宇宙执行完整的伤害与熄灭流程。
        foreach (var enemy in CombatState.HittableEnemies)
        {
            if (useBurning)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (!await SunqianUniverseInterop.TriggerBurning(choiceContext, enemy, this)) break;
                }
                continue;
            }

            var effect = YiZhenQingMengInterop.FindEffect(enemy, useBurning);
            if (effect == null || effect.Amount <= 0) continue;

            for (int i = 0; i < 2; i++)
            {
                if (effect.Amount <= 0) break;
                await CreatureCmd.Damage(choiceContext, enemy, effect.Amount,
#if GAME_BETA
                    ValueProp.Unblockable | ValueProp.Unpowered, this, cardPlay);
#else
                    ValueProp.Unblockable | ValueProp.Unpowered, this);
#endif
                effect.SetAmount(effect.Amount - 1);
            }
        }

        // 获得1颗星
        await XingPower.Gain(choiceContext, Owner.Creature, 1, Owner.Creature, this);

        // 回合结束时敌人回复并失去对应能力。
        await PowerCmd.Apply<YiZhenQingMengPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        YiZhenQingMengInterop.AddEffectText(description, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1); // 2→1
    }
}
