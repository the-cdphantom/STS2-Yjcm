param([ValidateSet('fix','test')][string]$Mode, [ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$devRoot=Split-Path $PSScriptRoot -Parent
$workspace=$devRoot
$suffix=if($Variant -eq 'stable') { '0.107.1' } else { '0.111.0' }
$bin=Join-Path $workspace "dependencies\$Variant-$suffix"
foreach($name in @('GodotSharp.dll','0Harmony.dll','sts2.dll','STS2-RitsuLib.dll')) { [void][Reflection.Assembly]::LoadFrom((Join-Path $bin $name)) }
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $devRoot "artifacts\staged\Yjcm.$suffix.dll"))
$modId=$assembly.GetType('Yjcm.MainFile',$true).GetField('ModId').GetRawConstantValue()
if($modId -ne 'Yjcm' -or $assembly.GetName().Name -ne 'Yjcm') { throw "Wrong mod/assembly identity: $modId/$($assembly.GetName().Name)" }
$loader=[Reflection.Assembly]::LoadFrom((Join-Path $devRoot 'artifacts\staged\Yjcm.dll'))
if($loader.GetName().Name -ne 'Yjcm.Loader') { throw 'Wrong loader assembly identity' }
if(@($loader.GetReferencedAssemblies()|Where-Object Name -eq '0Harmony').Count) { throw 'Loader must not patch content independently' }
$loaderType=$loader.GetType('Yjcm.Loader.LoaderBootstrap',$true)
if(!$loaderType.GetMethod('Initialize',[Reflection.BindingFlags]'Static,Public')) { throw 'Loader initializer missing' }
$loaderSource=Get-Content -LiteralPath (Join-Path $devRoot 'loader\LoaderBootstrap.cs') -Raw
if(!$loaderSource.Contains('InvokeRequiredBootstrap(real, "Yjcm.MainFile", "Initialize", "内容初始化")')) { throw 'Loader must invoke the unified content initializer' }
foreach($duplicate in @('Yjcm.WuZhugeliangSettings','Yjcm.WuZhugeliangTelemetry','new Harmony(')) {
    if($loaderSource.Contains($duplicate)) { throw "Loader retains duplicate initialization: $duplicate" }
}
$mainType=$assembly.GetType('Yjcm.MainFile',$true)
$privateStatic=[Reflection.BindingFlags]'Static,NonPublic'
if(!$mainType.GetMethod('InitializeCore',$privateStatic) -or !$mainType.GetField('_initialized',$privateStatic) -or !$mainType.GetField('_initializing',$privateStatic)) {
    throw 'Content initializer is not guarded by the expected idempotent state'
}
$mainSource=Get-Content -LiteralPath (Join-Path $devRoot 'mod\src\MainFile.cs') -Raw
if(!$mainSource.Contains('Api.PublicApi.Initialize([') -or !$mainSource.Contains('一将成名 Mod 加载成功，双向联动 API v1 已就绪。')) {
    throw 'Unified content initializer does not publish API readiness'
}
$types=$assembly.GetTypes()
$featureTypes=@($types | Where-Object { $_.FullName.StartsWith('Yjcm.WeiDongZhuo') })
if($Mode -eq 'fix' -and $featureTypes.Count -ne 0) { throw 'Production contains WeiDongZhuo types' }
foreach($name in @('Yjcm.KongMingDeng','Yjcm.SharedRelicScopePatch','Yjcm.PangFengYiYiTong','Yjcm.MouFaZhengXiXing')) {
    if(!$assembly.GetType($name)) { throw "Missing previous feature $name" }
}
foreach($name in @('Yjcm.WuZhugeliangSkinVisuals','Yjcm.WuZhugeliangSkinSelectPatch')) {
    if(!$assembly.GetType($name)) { throw "Missing common WuZhugeliang skin feature $name" }
}
if($Mode -eq 'test') {
    if(!$assembly.GetType('Yjcm.WeiDongZhuo')) { throw 'Test character missing' }
    foreach($name in @('WeiDongZhuoGuangYong','WeiDongZhuoJieGuangYong')) {
        if(!$assembly.GetType("Yjcm.$name")) { throw "Test relic missing: $name" }
    }
    $cards=@($featureTypes | Where-Object { !$_.IsAbstract -and $_.IsSubclassOf([MegaCrit.Sts2.Core.Models.CardModel]) })
    if($cards.Count -ne 90) { throw "Wrong independent card count $($cards.Count)" }
    $ids=@($cards | ForEach-Object {
        $attr=$_.GetCustomAttributesData() | Where-Object {$_.AttributeType.Name -eq 'RegisterCardAttribute'}
        ($attr.NamedArguments | Where-Object MemberName -eq 'FullPublicEntry').TypedValue.Value
    })
    if(@($ids | Sort-Object -Unique).Count -ne 90 -or @($ids | Where-Object {$_ -notmatch '^YJCM_CARD_'}).Count -gt 0) { throw 'Card IDs not unique or outside the mod namespace' }
    foreach($name in @(
        'WeiDongZhuoJuChui','WeiDongZhuoJieJuChui','WeiDongZhuoHaoShou',
        'WeiDongZhuoYuanYinMoShou','WeiDongZhuoWeiLvBuBaGuan','WeiDongZhuoWeiMaChaoJiZhan',
        'WeiDongZhuoWeiZhangLiaoPoRong','WeiDongZhuoXingDongZhuoWeiLin','WeiDongZhuoXingYuanShaoZongShi',
        'WeiDongZhuoWeiGongSunZanZhuiTao','WeiDongZhuoWeiMaTengXiaoBen','WeiDongZhuoWeiZhangLiaoYuXi','WeiDongZhuoNiuFuXiaoXi',
        'WeiDongZhuoWuPuDuanTi','WeiDongZhuoXuRongXiongHuo','WeiDongZhuoXuRongShaJue',
        'WeiDongZhuoXingYuanShaoXiaoYan','WeiDongZhuoXingYuanShaoJiaoWang','WeiDongZhuoMouZhangXiuFuXi',
        'WeiDongZhuoWangYueJianDao','WeiDongZhuoWangYueShiFeng','WeiDongZhuoMouXuYouMoYou',
        'WeiDongZhuoMouJuShouZuoJun','WeiDongZhuoDingYuanXianShuai','WeiDongZhuoMaTieQuXian',
        'WeiDongZhuoZhangHuanShuGuo','WeiDongZhuoLiJueGuoSiXiongSuan','WeiDongZhuoYuanTanYuanShangYuanXiNeiFa',
        'WeiDongZhuoMouDongChengDouWei','WeiDongZhuoMouJiaXuFuMouYang','WeiDongZhuoMouJiaXuFuMouYin',
        'WeiDongZhuoMouJiaXuSuShen','WeiDongZhuoMouJiaXuRuShi',
        'WeiDongZhuoLeCaiWenJiShuangQie','WeiDongZhuoLeCaiYongJiaoWei',
        'WeiDongZhuoLeDiaoChanTanBan',
        'WeiDongZhuoBaoNueXingTai',
        'WeiDongZhuoWuHuangFuSongLianJie','WeiDongZhuoWuHuangFuSongJiangXian',
        'WeiDongZhuoShenZhaoYunLongHun',
        'WeiDongZhuoDongZhuoHuoShaoLuoYang'
    )) {
        if(!$assembly.GetType("Yjcm.$name")) { throw "New card missing: $name" }
    }
    $comboTags=$assembly.GetType('Yjcm.WeiDongZhuoComboTags',$true)
    if(@($comboTags.GetCustomAttributesData() | Where-Object {$_.AttributeType.Name -eq 'RegisterOwnedCardTagAttribute'}).Count -ne 4) {
        throw 'Combo card tags not registered'
    }
    foreach($name in @('WeiDongZhuoMouJiaXuRuShi','WeiDongZhuoZuoJunRetainOption','WeiDongZhuoZuoJunFreeOption')) {
        $card=[Activator]::CreateInstance($assembly.GetType("Yjcm.$name",$true))
        if($card.ShouldShowInCardLibrary) { throw "Derived/option card leaked into card library: $name" }
        if($card.CanBeGeneratedInCombat) { throw "Derived/option card leaked into random generation: $name" }
    }
    foreach($name in @(
        'WeiDongZhuoLieCangOption1','WeiDongZhuoLieCangOption2','WeiDongZhuoLieCangOption3',
        'WeiDongZhuoLieCangOption4','WeiDongZhuoLieCangOption5',
        'WeiDongZhuoZhanJueOption1','WeiDongZhuoZhanJueOption2'
    )) {
        $card=[Activator]::CreateInstance($assembly.GetType("Yjcm.$name",$true))
        if($card.CanBeGeneratedInCombat) { throw "Choice card leaked into random generation: $name" }
    }
    $moYou=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiDongZhuoMouXuYouMoYou',$true))
    if($moYou.Rarity -ne [MegaCrit.Sts2.Core.Entities.Cards.CardRarity]::Uncommon) {
        throw 'MouXuYou rarity must be Uncommon'
    }
    foreach($name in @('WeiDongZhuoMouJiaXuSuShen','WeiDongZhuoShenZhaoYunLongHun')) {
        $card=[Activator]::CreateInstance($assembly.GetType("Yjcm.$name",$true))
        if($card.Rarity -ne [MegaCrit.Sts2.Core.Entities.Cards.CardRarity]::Uncommon) {
            throw "$name rarity must be Uncommon"
        }
    }
    foreach($name in @('LianJiePower','JiangXianPower','MoYouFreeNextPower')) {
        if(!$assembly.GetType("Yjcm.$name")) { throw "New power missing: $name" }
    }
    foreach($name in @('WeiDongZhuoWangYueJianDao','WeiDongZhuoWangYueShiFeng','WeiDongZhuoMaTieQuXian')) {
        $type=$assembly.GetType("Yjcm.$name",$true)
        $tip=@($type.GetProperties([Reflection.BindingFlags]'Instance,Public,NonPublic') |
            Where-Object { $_.Name -eq 'AdditionalHoverTips' -and $_.DeclaringType -eq $type })
        if($tip.Count -ne 1) { throw "Derived-card hover tip missing: $name" }
    }
    $ruShi=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiDongZhuoMouJiaXuRuShi',$true))
    if($ruShi.CanBeGeneratedInCombat) { throw 'RuShi must not enter random card generation' }
    $suShenType=$assembly.GetType('Yjcm.WeiDongZhuoMouJiaXuSuShen',$true)
    $suShenTip=@($suShenType.GetProperties([Reflection.BindingFlags]'Instance,Public,NonPublic') |
        Where-Object { $_.Name -eq 'AdditionalHoverTips' -and $_.DeclaringType -eq $suShenType })
    if($suShenTip.Count -ne 1) { throw 'SuShen derived RuShi hover tip missing' }
    $musicSource=Get-Content -LiteralPath (Join-Path $devRoot 'mod\src\NewCards\WeiDongZhuoMusicEnchantmentCards.cs') -Raw
    if($musicSource -match 'ForceApplyMusicEnchantment|EnchantInternal') { throw 'Music enchantment cards must not bypass native enchant eligibility' }
    if($musicSource -notmatch 'if \(!CanApplyMusicEnchantment\(card\)\)\s*continue;') { throw 'Music enchantment cards must skip ineligible hand cards before clearing enchantments' }
    foreach($name in @('HuJia','JiaoWei','TanBan')) {
        if(!$musicSource.Contains("ModelDb.Enchantment<$name>().CanEnchant(card)")) { throw "Music enchantment eligibility filter missing: $name" }
        $enchantment=[Activator]::CreateInstance($assembly.GetType("Yjcm.$name",$true))
        foreach($cardName in @('Greed','Wound')) {
            $card=[Activator]::CreateInstance([type]("MegaCrit.Sts2.Core.Models.Cards.$cardName"))
            if($enchantment.CanEnchant($card)) { throw "Music enchantment must reject ${cardName}: $name" }
        }
        $strike=[Activator]::CreateInstance([MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad])
        if(!$enchantment.CanEnchant($strike)) { throw "Music enchantment must still accept a normal card: $name" }
    }
}
Write-Output "ASSEMBLY_ISOLATION_OK mode=$Mode variant=$Variant featureTypes=$($featureTypes.Count)"
