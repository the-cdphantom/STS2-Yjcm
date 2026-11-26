param([ValidateSet('stable','beta')][string]$Variant,[ValidateSet('release','caocao')][string]$Flavor='release')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$version=if($Variant -eq 'stable'){'0.111.0'}else{'0.114.0'}
foreach($file in @('GodotSharp','0Harmony','sts2','STS2-RitsuLib','STS2-RitsuLib.Shared','STS2-RitsuLib.Ui','STS2-RitsuLib.Settings','STS2-RitsuLib.Runtime')){[void][Reflection.Assembly]::LoadFrom((Join-Path $repo "dependencies/$Variant-$version/$file.dll"))}
$stage=if($Flavor -eq 'release'){'artifacts/staged'}else{'artifacts/caocao/staged'}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $repo "$stage/Yjcm.$version.dll"))
$types=@($assembly.GetTypes()|Where-Object Name -like '*WeiCaoCao*')
$flags=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
$character=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiCaoCao',$true))
$characterEntry=[STS2RitsuLib.Content.ModContentRegistry]::GetFixedPublicEntry('Yjcm',$character.GetType())
if($characterEntry -ne 'YJCM_CHARACTER_WEI_CAO_CAO'){throw "Character localization ID differs: $characterEntry"}
$deck=@($character.GetType().GetProperty('StartingDeckTypes',$flags).GetValue($character))
if($deck.Count -ne 10 -or @($deck|Where-Object Name -eq 'WeiCaoCaoSha').Count -ne 4 -or @($deck|Where-Object Name -eq 'WeiCaoCaoShan').Count -ne 4 -or @($deck|Where-Object Name -eq 'WeiCaoCaoHuJia').Count -ne 1 -or @($deck|Where-Object Name -eq 'WeiCaoCaoDuoYue').Count -ne 1){throw 'Caocao starter must be four Sha, four Shan, HuJia and DuoYue'}
$relics=@($character.GetType().GetProperty('StartingRelicTypes',$flags).GetValue($character))
if($relics.Count -ne 1 -or $relics[0].Name -ne 'WeiCaoCaoJunHe'){throw 'Caocao starter relic must be JunHe'}
$cards=@($types|Where-Object {!$_.IsAbstract -and $_.IsSubclassOf([MegaCrit.Sts2.Core.Models.CardModel])})
if($cards.Count -ne 102){throw "Expected 53 base models and 49 Wei-design models, got $($cards.Count)"}
$ids=@()
foreach($type in $cards){
    $card=[Activator]::CreateInstance($type)
    $attribute=$type.GetCustomAttributesData()|Where-Object {$_.AttributeType.Name -eq 'RegisterCardAttribute'}
    if($attribute.ConstructorArguments[0].Value.Name -ne 'WeiCaoCaoCardPool'){throw 'Wrong card pool'}
    $id=($attribute.NamedArguments|Where-Object MemberName -eq 'FullPublicEntry').TypedValue.Value
    if($id -notlike 'YJCM_CARD_WEI_CAO_CAO_*'){throw 'Wrong experimental card ID'}
    $ids+=$id
    $tags=$type.GetProperty('CanonicalTags',$flags).GetValue($card)
    if($type.Name -like 'WeiCaoCaoShen*' -and $card.Rarity -ne [MegaCrit.Sts2.Core.Entities.Cards.CardRarity]::Token){
        if(!$tags.Contains([Yjcm.YjcmCardTags]::ShenJiang)){throw "God-general faction tag missing: $type"}
    }elseif(!$type.IsSubclassOf($assembly.GetType('Yjcm.WeiCaoCaoDesignCard',$true)) -and $type.Name -notin @('WeiCaoCaoHuJia','WeiCaoCaoDuoYue','WeiCaoCaoJieDuoYue','WeiCaoCaoLeCaoZhiFuLe') -and $tags.Count -gt 1){throw 'Unexpected general card in common card set'}
}
if(@($ids|Sort-Object -Unique).Count -ne 102){throw 'Duplicate Caocao card IDs'}
$gods=@($cards|Where-Object {$_.Name -like 'WeiCaoCaoShen*' -and ([Activator]::CreateInstance($_)).Rarity -ne [MegaCrit.Sts2.Core.Entities.Cards.CardRarity]::Token})
if($gods.Count -ne 14){throw 'Expected fourteen regular god-general cards'}
$pool=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiCaoCaoCardPool',$true))
if($pool.CardFrameMaterialPath -ne 'card_frame_weicaocao'){throw 'Caocao frame missing'}
$sha=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiCaoCaoSha',$true))
$shan=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiCaoCaoShan',$true))
if($sha.DynamicVars.Damage.BaseValue -ne 6 -or $shan.DynamicVars.Block.BaseValue -ne 5){throw 'Common base card balance changed'}
$junHe=[Activator]::CreateInstance($assembly.GetType('Yjcm.WeiCaoCaoJunHe',$true))
if([STS2RitsuLib.Content.ModContentRegistry]::GetFixedPublicEntry('Yjcm',$junHe.GetType()) -ne 'YJCM_RELIC_WEI_CAO_CAO_JUN_HE'){throw 'JunHe relic localization ID differs'}
$jieJunHe=$assembly.GetType('Yjcm.WeiCaoCaoJieJunHe',$true)
if([STS2RitsuLib.Content.ModContentRegistry]::GetFixedPublicEntry('Yjcm',$jieJunHe) -ne 'YJCM_RELIC_WEI_CAO_CAO_JIE_JUN_HE'){throw 'JieJunHe relic localization ID differs'}
$junHePower=$assembly.GetType('Yjcm.WeiCaoCaoJunHePower',$true)
if([STS2RitsuLib.Content.ModContentRegistry]::GetFixedPublicEntry('Yjcm',$junHePower) -ne 'YJCM_POWER_WEI_CAO_CAO_JUN_HE_POWER'){throw 'JunHe resource localization ID differs'}
foreach($entry in @(@('WeiCaoCaoFu','YJCM_ENCHANTMENT_WEI_CAO_CAO_FU'),@('WeiCaoCaoWenLan','YJCM_POWER_WEI_CAO_CAO_WEN_LAN'))){
    if([STS2RitsuLib.Content.ModContentRegistry]::GetFixedPublicEntry('Yjcm',$assembly.GetType('Yjcm.'+$entry[0],$true)) -ne $entry[1]){throw "FuLe localization ID differs: $($entry[0])"}
}
Write-Output "CAOCAO_MODELS_OK variant=$Variant cards=$($cards.Count) starter=4+4+HuJia+DuoYue relic=JunHe"
