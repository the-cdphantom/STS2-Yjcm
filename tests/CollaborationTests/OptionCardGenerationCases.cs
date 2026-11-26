using System.Reflection;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

internal static class OptionCardGenerationCases
{
    public static void Run(Assembly mod, Action<bool, string> check)
    {
        var designChoice = mod.GetType("Yjcm.WeiCaoCaoDesignChoice", true)!;
        var optionTypes = new List<Type>(
            mod.GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(designChoice)));

        foreach (string name in new[]
        {
            "HongCeShangCe", "HongCeZhongCe", "HongCeXiaCe",
            "BeiZhiCard",
            "WuSeSha", "WeiDongZhuoWuSeSha", "WeiCaoCaoWuSeSha",
            "ShenPangTongFengLiaoYin", "WeiDongZhuoShenPangTongFengLiaoYin",
            "WeiCaoCaoShenPangTongFengLiaoYin",
            "PangHongPingZhiYin",
            "WeiCaoCaoMouSiMaShiChenLue",
            "WeiCaoCaoLieCangOption1", "WeiCaoCaoLieCangOption2",
            "WeiCaoCaoLieCangOption3", "WeiCaoCaoLieCangOption4", "WeiCaoCaoLieCangOption5",
            "WeiCaoCaoZhanJueOption1", "WeiCaoCaoZhanJueOption2",
            "LieCangOption1", "LieCangOption2", "LieCangOption3", "LieCangOption4", "LieCangOption5",
            "ZhanJueOption1", "ZhanJueOption2",
            "QingBeiAttack", "QingBeiSkill", "QingBeiPowerOption", "QingBeiSkip",
            "QingShiDamageOption", "QingShiEnergyOption", "QingShiDrawOption",
            "SlotOptionWeapon", "SlotOptionArmor", "SlotOptionDefensiveHorse",
            "SlotOptionOffensiveHorse", "SlotOptionTreasure",
            "WeiDongZhuoLieCangOption1", "WeiDongZhuoLieCangOption2",
            "WeiDongZhuoLieCangOption3", "WeiDongZhuoLieCangOption4", "WeiDongZhuoLieCangOption5",
            "WeiDongZhuoZhanJueOption1", "WeiDongZhuoZhanJueOption2",
            "WeiDongZhuoZuoJunRetainOption", "WeiDongZhuoZuoJunFreeOption",
        })
        {
            var type = mod.GetType("Yjcm." + name, false);
            if (type != null) optionTypes.Add(type);
        }

        var options = optionTypes
            .Distinct()
            .Select(type => (CardModel)Activator.CreateInstance(type)!)
            .ToArray();
        check(options.Length > 0, "Option card scan must find the known option cards");
        foreach (var option in options)
        {
            check(!option.CanBeGeneratedInCombat,
                $"{option.GetType().Name} must not be generated randomly in combat");
            check(!option.ShouldShowInCardLibrary,
                $"{option.GetType().Name} must stay hidden from the card library");
        }

        var normal = new ShrugItOff();
        var filtered = CardFactory.FilterForCombat(options.Concat([normal])).ToArray();
        check(filtered.Contains(normal), "A normal combat card must still pass the native combat filter");
        check(options.All(option => !filtered.Contains(option)),
            "No option card may pass the native combat generation filter");
    }
}
