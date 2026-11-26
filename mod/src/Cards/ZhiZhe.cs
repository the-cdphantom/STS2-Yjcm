using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Godot;

using STS2RitsuLib.Interop.AutoRegistration;

namespace Yjcm;

/// <summary>
/// 智哲：0费技能牌，消耗。选择一张手牌复制并添加保留。升级后固有。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "STS001")]
[RegisterCard(typeof(WuZhugeliangCardPool))]
public sealed class ZhiZhe : WuZhugeliangCard
{
	public ZhiZhe() : base(0, CardType.Skill, CardRarity.Basic, TargetType.None)
	{
	}

	public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];

	protected override IEnumerable<DynamicVar> CanonicalVars => [];

	// 卡牌音效
	private static readonly string VoicePath = Path.Combine(
		Path.GetDirectoryName(typeof(ZhiZhe).Assembly.Location)!,
		"Voice", "Bank", "Assets", "zhi_zhe.wav");

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		VoicePlayer.PlayWav(VoicePath);

		var owner = cardPlay.Card.Owner;
		if (owner == null) return;

		// 选择一张手牌（排除自己）
		var selectedCards = await CardSelectCmd.FromHand(
			choiceContext,
			owner,
			new CardSelectorPrefs(new("cards", "zhizhe.select_prompt"), 1, 1),
			card => card != this,
			this
		);

		var selected = selectedCards.FirstOrDefault();
		if (selected == null) return;

		// 通过 CombatState 创建战斗中的副本，添加保留
		var clone = owner.Creature.CombatState.CloneCard(selected);
		CardCmd.ApplyKeyword(clone, CardKeyword.Retain);
		await CardPileCmd.Add(clone, PileType.Hand, CardPilePosition.Random, this, false);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}
}
