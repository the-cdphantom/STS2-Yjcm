param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$dev=Split-Path $PSScriptRoot -Parent
$workspace=$dev
$version=if($Variant -eq 'stable') {'0.107.1'} else {'0.111.0'}
$source=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Interop\PublicApi.cs') -Raw
if($source -match '#if GAME_BETA') { throw 'The JSON public API must be version-independent' }
foreach($file in @('Interop\YjcmPublicApi.cs','Interop\SunqianUniverseInterop.cs','Settings\WuZhugeliangSettings.cs','Visuals\CardEffectVfx.cs','Visuals\ShiWeiYanVfx.cs','Monsters\MouSimaYiSpineVisuals.cs')) {
    if((Get-Content -LiteralPath (Join-Path $workspace "mod\src\$file") -Raw) -match '#if GAME_BETA') { throw "Shared API/settings/visual must be version-independent: $file" }
}
# Only interface signatures are public; the partner implementation remains local/ignored.
$contract = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'fixtures/sunqian-api-signatures.json') -Raw | ConvertFrom-Json
$proxy = Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Interop\SunqianUniverseInterop.cs') -Raw
$pattern = 'public static (?:async\s+)?(?<return>[\w?<>]+) (?<name>\w+)\((?<args>[^)]*)\)'
$remoteSignatures = @{}
foreach($method in $contract.methods.PSObject.Properties) { $remoteSignatures[$method.Name] = $method.Value }
$reverseChecks = 0
foreach($m in [regex]::Matches($proxy,$pattern)) {
    $name=$m.Groups['name'].Value
    $signature=$m.Groups['return'].Value + '(' + ($m.Groups['args'].Value -replace '\s','') + ')'
    if($remoteSignatures[$name] -ne $signature) { throw "Sunqian commit signature mismatch: $name" }
    $reverseChecks++
}
if($reverseChecks -ne 17 -or $contract.commit -ne '5c631501' -or
   $contract.apiBlob -ne '69d6321b74fcdd43ebb8ef6952fd1e5d4d0aaa25' -or
   $contract.isReady -ne $true -or $contract.type -ne 'Squ.Api.SunqianUniversePublicApi, sunqian-universe' -or
   $proxy -notmatch 'Squ.Api.SunqianUniversePublicApi, sunqian-universe') { throw 'Sunqian proxy target/property drift' }
Write-Output "SUNQIAN_SIGNATURES_OK methods=$reverseChecks apiBlob=$($contract.apiBlob.Substring(0,8))"
$dreamInterop=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Interop\YiZhenQingMengInterop.cs') -Raw
foreach($required in @(
    'IsModReady && SunqianUniverseInterop.IsSupportingActorCharacter(player?.Character)',
    'SunqianUniverseInterop.CreateBurningHoverTip()',
    'SunqianUniverseInterop.ApplyBurning(',
    'target.Powers?.FirstOrDefault(SunqianUniverseInterop.IsBurningPower)',
    'HoverTipFactory.FromPower<HuoGongPower>()'
)) {
    if(-not $dreamInterop.Contains($required)) { throw "YiZhenQingMeng interop branch missing: $required" }
}
$dreamCard=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Cards\YiZhenQingMeng.cs') -Raw
foreach($required in @(
    'YiZhenQingMengInterop.EffectHoverTips(this)',
    'YiZhenQingMengInterop.LibraryHoverTips()',
    'YiZhenQingMengInterop.AddEffectText(description, this)',
    'YiZhenQingMengInterop.ApplyEffect(',
    'YiZhenQingMengInterop.FindEffect(enemy, useBurning)',
    'SunqianUniverseInterop.TriggerBurning(choiceContext, enemy, this)'
)) {
    if(-not $dreamCard.Contains($required)) { throw "YiZhenQingMeng card integration missing: $required" }
}
$dreamPower=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Powers\YiZhenQingMengPower.cs') -Raw
if(-not $dreamPower.Contains('YiZhenQingMengInterop.Affects(Owner.Player)') -or
   -not $dreamPower.Contains('YiZhenQingMengInterop.FindEffect(enemy, useBurning)') -or
   -not $dreamPower.Contains('PowerCmd.Remove(effect)')) {
    throw 'YiZhenQingMeng end-turn interop cleanup drift'
}
$linkHover=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Interop\SunqianLinkHover.cs') -Raw
foreach($required in @(
    'RunManager.Instance is not { IsInProgress: true }',
    'CombatManager.Instance is not { IsInProgress: true }',
    'LocalContext.IsMe(player)',
    'SunqianUniverseInterop.IsSupportingActorCharacter(player.Character)',
    'public static bool ShowsBurningKeyword()'
)) {
    if(-not $linkHover.Contains($required)) { throw "Sunqian link hover visibility missing: $required" }
}
$fanInteropEarly=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Interop\ZhuQueYuShanInterop.cs') -Raw
if(-not $dreamInterop.Contains('SunqianLinkHover.ShouldShow()') -or
   -not $fanInteropEarly.Contains('SunqianLinkHover.ShouldShow()') -or
   -not $dreamInterop.Contains('SunqianLinkHover.BodyKey') -or
   -not $fanInteropEarly.Contains('SunqianLinkHover.BodyKey') -or
   -not $dreamInterop.Contains('SunqianLinkHover.ShowsBurningKeyword()') -or
   -not $fanInteropEarly.Contains('SunqianLinkHover.ShowsBurningKeyword()')) {
    throw 'Interop notice and keyword tint must stay separated'
}
$fanInterop=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Interop\ZhuQueYuShanInterop.cs') -Raw
foreach($required in @(
    'IsModReady && SunqianUniverseInterop.IsSupportingActorCharacter(player?.Character)',
    'SunqianUniverseInterop.CreateBurningHoverTip()',
    'SunqianUniverseInterop.ApplyBurning(',
    'HoverTipFactory.FromPower<HuoGongPower>()'
)) {
    if(-not $fanInterop.Contains($required)) { throw "ZhuQueYuShan interop branch missing: $required" }
}
$fanCard=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Cards\ZhuQueYuShan.cs') -Raw
$fanClone=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Cards\Generated\WeiDongZhuoZhuQueYuShan.cs') -Raw
foreach($required in @(
    'ZhuQueYuShanInterop.EffectHoverTips(this)',
    'ZhuQueYuShanInterop.LibraryHoverTips()',
    'ZhuQueYuShanInterop.AddEffectText(description, this)'
)) {
    if(-not $fanCard.Contains($required) -or -not $fanClone.Contains($required)) {
        throw "ZhuQueYuShan card integration missing: $required"
    }
}
$fanOrb=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Orbs\ZhuQueYuShanOrb.cs') -Raw
if(-not $fanOrb.Contains('ZhuQueYuShanInterop.Affects(Owner)') -or
   -not $fanOrb.Contains('ZhuQueYuShanInterop.ApplyEffect(') -or
   -not $fanOrb.Contains('ZhuQueYuShanInterop.AddEffectText(__result, orb)') -or
   -not $fanOrb.Contains('ZhuQueYuShanInterop.EffectHoverTips(this)')) {
    throw 'ZhuQueYuShan weapon interop drift'
}
$cardsLoc=Get-Content -LiteralPath (Join-Path $workspace 'mod\Yjcm\localization\zhs\cards.json') -Raw | ConvertFrom-Json
$orbsLoc=Get-Content -LiteralPath (Join-Path $workspace 'mod\Yjcm\localization\zhs\orbs.json') -Raw | ConvertFrom-Json
$fanDescription=$cardsLoc.'YJCM_CARD_ZHU_QUE_YU_SHAN.description'
if($fanDescription -ne $cardsLoc.'YJCM_CARD_WEI_DONG_ZHUO_ZHU_QUE_YU_SHAN.description' -or
   ([regex]::Matches($fanDescription,'\{FanFireText\}').Count -ne 1) -or
   $cardsLoc.'YJCM_CARD_ZHU_QUE_YU_SHAN.fireAttackInlineText' -notmatch '火攻' -or
   $cardsLoc.'YJCM_CARD_ZHU_QUE_YU_SHAN.burningInlineText' -ne '[purple]灼烧[/purple]' -or
   $cardsLoc.'sunqian_burning_link.libraryHover' -ne '模组[purple]孙乾宇宙[/purple]角色使用此牌时，将给予[gold]灼烧[/gold]而非[gold]火攻[/gold]。' -or
   ([regex]::Matches($orbsLoc.'YJCM_ORB_ZHU_QUE_YU_SHAN_ORB.description','\{FanFireText\}').Count -ne 1)) {
    throw 'ZhuQueYuShan dynamic localization drift'
}
$dreamDescription=$cardsLoc.'YJCM_CARD_YI_ZHEN_QING_MENG.description'
if(([regex]::Matches($dreamDescription,'\{DreamFireText\}').Count -ne 4) -or
   $cardsLoc.'YJCM_CARD_YI_ZHEN_QING_MENG.fireAttackInlineText' -notmatch '火攻' -or
   $cardsLoc.'YJCM_CARD_YI_ZHEN_QING_MENG.burningInlineText' -ne '[purple]灼烧[/purple]' -or
   $cardsLoc.'sunqian_burning_link.hoverTitle' -ne '※模组联动') {
    throw 'YiZhenQingMeng dynamic localization drift'
}
$project=Join-Path $PSScriptRoot 'InteropTests\InteropTests.csproj'
$buildLog=Join-Path $PSScriptRoot "interop-$Variant.log"
& dotnet build $project -c Release "-p:GameApiVariant=$Variant" -v quiet *> $buildLog
if($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $buildLog -Tail 35; throw "Interop consumer build failed: $Variant" }
& dotnet (Join-Path $dev "artifacts/bin/InteropTests/$Variant/Release/net9.0/InteropTests.dll") (Join-Path $dev "artifacts\staged\Yjcm.$version.dll") (Join-Path $workspace "dependencies\$Variant-$version")
if($LASTEXITCODE -ne 0) { throw "Interop contract failed: $Variant" }
