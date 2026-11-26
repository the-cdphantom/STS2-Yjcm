using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using Yjcm.Extensions;

namespace Yjcm;

public abstract class QingShiOption : CardModel
{
    protected QingShiOption(CardType type) : base(0, type, CardRarity.Token, TargetType.None, shouldShowInCardLibrary: false) { }
    public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override string PortraitPath => "zhi_zhe.png".CardImagePath();
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class QingShiDamageOption() : QingShiOption(CardType.Attack);

[RegisterCard(typeof(TokenCardPool))]
public sealed class QingShiEnergyOption() : QingShiOption(CardType.Power);

[RegisterCard(typeof(TokenCardPool))]
public sealed class QingShiDrawOption() : QingShiOption(CardType.Skill);
