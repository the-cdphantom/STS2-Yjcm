param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$workspace=Split-Path $PSScriptRoot -Parent
$dev=Split-Path $PSScriptRoot -Parent
$version=if($Variant -eq 'stable') {'0.111.0'} else {'0.114.0'}
foreach($name in @('GodotSharp','0Harmony','sts2','STS2-RitsuLib','STS2-RitsuLib.Shared','STS2-RitsuLib.Ui','STS2-RitsuLib.Settings','STS2-RitsuLib.Runtime')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $workspace "dependencies\$Variant-$version\$name.dll"))
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $dev "artifacts\staged\Yjcm.$version.dll"))
$static=[Reflection.BindingFlags]'Static,Public,NonPublic'
$type=$assembly.GetType('Yjcm.CardEffectVfx',$true)
$resolve=$type.GetMethod('Resolve',$static)
$scaleMethod=$type.GetMethod('GetScale',$static)
$mapping=$type.GetField('CardFolders',$static).GetValue($null)
$script:checks=0
function Assert-Effect($value,$description) { if(!$value) {throw $description}; $script:checks++ }
$catalog=Get-Content -LiteralPath (Join-Path $dev 'assets\loose\Spine\CardEffects\index.json') -Raw | ConvertFrom-Json
Assert-Effect ($catalog.Count -eq 18) 'Expected 18 effect bundles'
foreach($entry in $mapping.GetEnumerator()) {
    $model=$assembly.GetType("Yjcm.$($entry.Key)",$false)
    if(!$model) {$model=$assembly.GetType("Yjcm.WeiDongZhuo$($entry.Key)",$true)}
    Assert-Effect ([MegaCrit.Sts2.Core.Models.CardModel].IsAssignableFrom($model)) "Mapped type is not a card: $model"
    foreach($prefix in @('','WeiDongZhuo')) {
        foreach($cardType in @([MegaCrit.Sts2.Core.Entities.Cards.CardType]::Attack,[MegaCrit.Sts2.Core.Entities.Cards.CardType]::Skill,[MegaCrit.Sts2.Core.Entities.Cards.CardType]::Power)) {
            $resolved=$resolve.Invoke($null,@("$prefix$($entry.Key)",$cardType))
            Assert-Effect ($resolved.Item1 -eq $entry.Value) 'Folder mapping mismatch'
            $expected=if($cardType -eq [MegaCrit.Sts2.Core.Entities.Cards.CardType]::Attack) {'gongji'} else {'jineng'}
            Assert-Effect ($resolved.Item2 -eq $expected) 'Attack/skill/power animation mismatch'
        }
    }
}
Assert-Effect ($null -eq $resolve.Invoke($null,@('LieCangOption1',[MegaCrit.Sts2.Core.Entities.Cards.CardType]::Skill))) 'Option card must not trigger'
Assert-Effect ($null -eq $resolve.Invoke($null,@('Sha',[MegaCrit.Sts2.Core.Entities.Cards.CardType]::Attack))) 'Unmapped card must not trigger'
Assert-Effect ($null -eq $resolve.Invoke($null,@('ChenShiQingBei',[MegaCrit.Sts2.Core.Entities.Cards.CardType]::Status))) 'Status card must not trigger'
Assert-Effect ($resolve.Invoke($null,@('WeiDongZhuoZhuGeJingZuYin',[MegaCrit.Sts2.Core.Entities.Cards.CardType]::Attack)).Item2 -eq 'gongji') 'ZuYin must use the supplied attack animation'
Assert-Effect ($null -ne [MegaCrit.Sts2.Core.Combat.History.CombatHistory].GetMethod('CardPlayStarted')) 'Native history hook changed'
foreach($viewport in @(@(1920,1080),@(2560,1080),@(1080,1920))) {
    foreach($folder in @('谋贾诩阳和肃身','谋贾诩阴和入世','谋郭嘉阳','谋郭嘉阴','神赵云')) {
        $w=[single]1563.445; $h=[single]1037.271
        $scale=$scaleMethod.Invoke($null,@($folder,[single]$viewport[0],[single]$viewport[1],$w,$h))
        $expected=if($folder -eq '神赵云') {
            [Math]::Min([single]$viewport[0]*[single]0.72/$w,[single]$viewport[1]*[single]0.72/$h)
        } else {[Math]::Max([single]$viewport[0]/$w,[single]$viewport[1]/$h)}
        Assert-Effect ([Math]::Abs($scale-$expected) -lt 0.00001) 'Fullscreen/ordinary scaling mismatch'
    }
}
foreach($asset in $catalog) {
    Assert-Effect ($mapping.Values.Contains($asset.folder)) "Unmapped bundle: $($asset.folder)"
    Assert-Effect ($asset.version -eq '4.2.43') 'Spine API version mismatch'
    $root=Join-Path $dev "assets\loose\Spine\CardEffects\$($asset.folder)"
    foreach($name in @('chuchang2.skel','chuchang2.atlas')+$asset.pages) {
        Assert-Effect (Test-Path -LiteralPath (Join-Path $root $name)) "Missing asset: $root\$name"
    }
    foreach($clip in $asset.animations) {
        Assert-Effect ($clip.name -in @('gongji','jineng') -and $clip.duration -gt 0 -and $clip.duration -lt 10) 'Unexpected animation'
        Assert-Effect ($clip.focus.width -gt 0 -and $clip.focus.height -gt 0) 'Invalid framing'
    }
}
$source=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Visuals\CardEffectVfx.cs') -Raw
Assert-Effect ($source -notmatch '#if GAME_BETA') 'Shared VFX must be version-independent'
Assert-Effect (!$source.Contains('await ') -and !$source.Contains('Task.Run(')) 'VFX must not await gameplay or run off-thread'
Assert-Effect ($source.Contains('card.CurrentPlayIndex != 0')) 'Repeated execution guard missing'
Assert-Effect ($source.Contains('card.GetType().Assembly != typeof(CardEffectVfx).Assembly')) 'Other-mod isolation missing'
Assert-Effect ($source.Contains('CreateTimer(clip.Duration + 0.05f)')) 'Animation duration cleanup missing'
Assert-Effect ($source.Contains('viewportNode.SizeChanged -= Layout')) 'Viewport signal cleanup missing'
Assert-Effect ($source.Contains('if (!WuZhugeliangSettings.EnableEffects) return;')) 'Card effects total toggle missing'
foreach($file in @('Visuals\ShiWeiYanVfx.cs','Monsters\MouSimaYiSpineVisuals.cs')) {
    $visual=Get-Content -LiteralPath (Join-Path $workspace "mod\src\$file") -Raw
    Assert-Effect ($visual.Contains('WuZhugeliangSettings.Enable') -and $visual.Contains('Visible = false;')) "Total toggle/hide missing: $file"
}
$combo=Get-Content -LiteralPath (Join-Path $dev 'mod\src\Visuals\WeiDongZhuoComboVfx.cs') -Raw
Assert-Effect ($combo.Contains('if (!WuZhugeliangSettings.EnableEffects) return;') -and $combo.Contains('Visible = false;')) 'Combo total toggle/hide missing'
$settings=Get-Content -LiteralPath (Join-Path $workspace 'mod\src\Settings\WuZhugeliangSettings.cs') -Raw
Assert-Effect ($settings.Contains('public bool EnableEffects { get; set; } = true;')) 'Persistent default-on toggle missing'
Assert-Effect ($settings.Contains('public static bool EnableMouSimaYiEffects => EnableEffects;')) 'Boss must share total toggle'
Assert-Effect ($settings.Contains('特效总开关') -and !$settings.Contains('"enable_mou_sima_yi_effects"')) 'Total toggle UI missing or old individual toggle retained'
Write-Output "CARD_EFFECTS_OK variant=$Variant bundles=$($catalog.Count) mappedCards=$($mapping.Count) assertions=$script:checks"
