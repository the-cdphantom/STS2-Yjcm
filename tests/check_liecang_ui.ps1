param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=if($Variant -eq 'stable') {'0.111.0'} else {'0.114.0'}
foreach($name in @('GodotSharp','0Harmony','sts2','STS2-RitsuLib','STS2-RitsuLib.Shared','STS2-RitsuLib.Ui','STS2-RitsuLib.Settings','STS2-RitsuLib.Runtime')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $root "dependencies\$Variant-$version\$name.dll"))
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $root "artifacts\staged\Yjcm.$version.dll"))
$static=[Reflection.BindingFlags]'Static,NonPublic,Public'
$instance=[Reflection.BindingFlags]'Instance,NonPublic,Public'
$script:checks=0
function Assert-Ui($value,[string]$message) {
    if(!$value) {throw $message}; $script:checks++
}
$patch=$assembly.GetType('Yjcm.LieCangBodySelectionPatch',$true)
$points=$patch.GetField('Points',$static).GetValue($null)
Assert-Ui ($points.Count -eq 5) 'Expected five anatomical hit points'
$expected=@('中枢','气海','力烽','地机','天冲')
for($i=0;$i -lt 5;$i++) {
    Assert-Ui ($points[$i].Item2 -eq $expected[$i]) 'Hit point name/order mismatch'
    Assert-Ui ($points[$i].Item3.X -gt 38 -and $points[$i].Item3.X -lt 265) 'Hit point outside panel'
}
Assert-Ui ($points[0].Item1 -eq 3 -and $points[1].Item1 -eq 4 -and $points[2].Item1 -eq 1 -and $points[3].Item1 -eq 2 -and $points[4].Item1 -eq 5) 'Option/effect mapping mismatch'
Assert-Ui ($points[4].Item3.Y -lt $points[0].Item3.Y) 'Tian Chong must be above Zhong Shu'
Assert-Ui ($points[1].Item3.X -lt $points[2].Item3.X) 'Qi Hai must be left of Li Feng'
Assert-Ui ($points[3].Item3.Y -gt $points[1].Item3.Y) 'Di Ji must be lowest'
$native=[MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NSimpleCardSelectScreen]
$base=[MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardGridSelectionScreen]
Assert-Ui ($base.GetField('_cards',$instance).FieldType -eq [System.Collections.Generic.IReadOnlyList[MegaCrit.Sts2.Core.Models.CardModel]]) 'Native card list API changed'
Assert-Ui ($native.GetField('_selectedCards',$instance).FieldType -eq [System.Collections.Generic.HashSet[MegaCrit.Sts2.Core.Models.CardModel]]) 'Native selected card API changed'
Assert-Ui ($null -ne $native.GetMethod('CompleteSelection',$instance)) 'Native completion API changed'
Assert-Ui ($null -ne $base.GetProperty('FocusedControlFromTopBar',$instance)) 'Native focus API changed'
$source=Get-Content -LiteralPath (Join-Path $root 'mod\src\Patches\LieCangBodySelectionPatch.cs') -Raw
Assert-Ui ($source -notmatch '#if GAME_BETA') 'Body-selection UI must be shared by both variants'
Assert-Ui ($source.Contains('if (card == null) continue;')) 'Locked Tian Chong must not be displayed'
Assert-Ui ($source.Contains('CompleteMethod.Invoke(screen, null)')) 'Must use native completion and network choice pipeline'
Assert-Ui ($source.Contains('if (completed) return;')) 'Missing duplicate-click guard'
$atlasSource=Join-Path $root '素材\各类特效\神黄忠爆头\skill_yiwu.webp'
# The user has reorganized the effect sources; retain and compare the already-imported UI atlas.
if(-not (Test-Path -LiteralPath $atlasSource)) { $atlasSource=Join-Path $root 'mod\Yjcm\Images\ui\lie_cang_atlas.webp' }
$atlas=Get-FileHash -LiteralPath $atlasSource
foreach($project in @('mod')) {
    Assert-Ui ((Get-FileHash -LiteralPath (Join-Path $root "$project\Yjcm\Images\ui\lie_cang_atlas.webp")).Hash -eq $atlas.Hash) 'Atlas changed from provided source'
    $cardSource=Get-Content -LiteralPath (Join-Path $root "$project\src\Cards\ShenHuangZhongLieCang.cs") -Raw
    Assert-Ui ($cardSource.Contains('await CardSelectCmd.FromSimpleGrid(')) 'Original multiplayer selector must be retained'
    Assert-Ui ($cardSource.Contains('OfType<LieCangMarkerPower>().Any()')) 'Original Tian Chong unlock condition must be retained'
}
Write-Output "LIECANG_UI_OK variant=$Variant assertions=$script:checks"
