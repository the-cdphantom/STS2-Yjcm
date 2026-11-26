param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$version=if($Variant -eq 'stable'){'0.111.0'}else{'0.114.0'}
foreach($name in @('GodotSharp','0Harmony','sts2','STS2-RitsuLib','STS2-RitsuLib.Shared','STS2-RitsuLib.Ui','STS2-RitsuLib.Settings','STS2-RitsuLib.Runtime')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $repo "dependencies/$Variant-$version/$name.dll"))
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $repo "artifacts/staged/Yjcm.$version.dll"))
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:checks=0
function Require($condition,[string]$message){if(!$condition){throw $message};$script:checks++}
Require ($null -eq $assembly.GetType('Yjcm.CharacterAttackAnimationPatch')) 'Withdrawn attack trigger patch must not compile'
Require ($null -eq $assembly.GetType('Yjcm.CharacterAttackPose')) 'Withdrawn mesh pose must not compile'
$mesh=$assembly.GetType('Yjcm.WuZhugeliangMeshAnimation',$true)
Require ($null -ne $mesh.GetMethod('Play',$flags)) 'Original Laya idle playback must remain'
Require ($null -eq $mesh.GetMethod('PlayAttack',$flags)) 'Mesh attack entry must be removed'
Require ($null -eq $mesh.GetMethod('CancelAttack',$flags)) 'Mesh attack state must be removed'
Require ($null -eq $mesh.GetField('_attackTime',$flags)) 'Mesh attack timer must be removed'
foreach($file in @('mod/src/Characters/WuZhugeliang/WuZhugeliangMeshAnimation.cs','mod/src/Patches/WuZhugeliangSkinPatch.cs','mod/WeiDongZhuo/scripts/spine_runtime.gd','mod/WeiDongZhuo/scripts/combat_body.gd')) {
    $source=Get-Content -LiteralPath (Join-Path $repo $file) -Raw
    Require ($source -notmatch 'yjcm_attack|_attack\.skel|CharacterAttackPose|PlayAttack|attack_clip') "Withdrawn attack loader remains: $file"
}
$runtime=Get-Content -LiteralPath (Join-Path $repo 'mod/WeiDongZhuo/scripts/spine_runtime.gd') -Raw
Require ($runtime.Contains('load_from_file", base + ".skel"')) 'Dong Zhuo must load original skeleton'
$skin=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Patches/WuZhugeliangSkinPatch.cs') -Raw
Require ($skin.Contains('Path.Combine(folderPath, fileName + ".skel")')) 'Mou skin must load original skeleton'
foreach($type in @('Yjcm.CardEffectVfx','Yjcm.WeiDongZhuoComboVfx','Yjcm.ShiWeiYanVfx')) {
    Require ($null -ne $assembly.GetType($type)) "Unrelated card/combo effects must remain: $type"
}
. (Join-Path $repo 'tools/config.ps1')
$retired=@(Get-YjcmRetiredRuntimePaths (Join-Path $repo 'assets/loose'))
Require ($retired.Count -eq 2) 'Only the two prototype skeletons may be retired'
foreach($file in $retired) {
    Require (!(Test-Path -LiteralPath $file)) 'Withdrawn skeleton must not ship'
    Require (Test-Path -LiteralPath (Join-Path (Split-Path $file -Parent) 'character.skel')) 'Original skeleton must remain'
}
Write-Output "CHARACTER_ATTACKS_REMOVED_OK variant=$Variant assertions=$script:checks"
