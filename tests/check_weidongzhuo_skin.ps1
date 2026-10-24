param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$version=if($Variant -eq 'stable'){'0.107.1'}else{'0.111.0'}
foreach($name in @('GodotSharp','0Harmony','sts2','STS2-RitsuLib')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $repo "dependencies/$Variant-$version/$name.dll"))
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $repo "artifacts/staged/Yjcm.$version.dll"))
$flags=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
$script:checks=0
function Require($condition,[string]$message){if(!$condition){throw $message};$script:checks++}
$settings=$assembly.GetType('Yjcm.WuZhugeliangSettings',$true)
$data=[Activator]::CreateInstance($assembly.GetType('Yjcm.WuZhugeliangSettingsData',$true))
Require ($data.WeiDongZhuoSkin -eq 'classic') 'New and old settings must default to classic'
$normalize=$settings.GetMethod('NormalizeWeiDongZhuoSkin',$flags)
foreach($value in @($null,'','unknown','classic')) {
    Require ($normalize.Invoke($null,[object[]]@($value)) -eq 'classic') 'Invalid skin must normalize to classic'
}
Require ($normalize.Invoke($null,@('tai_shan_beng_shi')) -eq 'tai_shan_beng_shi') 'TaiShan skin key missing'
Require ($settings.GetField('_registrationState',$flags).GetValue($null) -eq 0) 'Tests must not write live settings'
$set=$settings.GetMethod('SetWeiDongZhuoSkin',$flags)
$setWu=$settings.GetMethod('SetWuZhugeliangSkin',$flags)
$setWu.Invoke($null,@($true))
$set.Invoke($null,@($true))
Require ($settings.GetProperty('UseTaiShanBengShiSkin').GetValue($null)) 'Selecting TaiShan must take effect immediately'
Require ($settings.GetProperty('UseMouZhugeliangSkin').GetValue($null)) 'Dong Zhuo skin must not overwrite Wu skin'
$set.Invoke($null,@($false))
Require (!$settings.GetProperty('UseTaiShanBengShiSkin').GetValue($null)) 'Switching back to classic must take effect'
Require ($settings.GetProperty('UseMouZhugeliangSkin').GetValue($null)) 'Classic switch must not overwrite Wu skin'
$setWu.Invoke($null,@($false))
$type=$assembly.GetType('Yjcm.WeiDongZhuo',$true)
$folder=$type.GetMethod('SkinAssetFolder',$flags)
Require ($folder.Invoke($null,@($true)) -eq 'WeiDongZhuoTaiShan') 'New skin must have an isolated asset folder'
Require ($folder.Invoke($null,@($false)) -eq 'WeiDongZhuo') 'Default assets must remain unchanged'
$scope=$assembly.GetType('Yjcm.WeiDongZhuoSkinSelectPatch',$true).GetMethod('IsWeiDongZhuo',$flags)
Require ($scope.Invoke($null,@($type))) 'Dong Zhuo skin selector missing'
Require (!$scope.Invoke($null,@($assembly.GetType('Yjcm.WuZhugeliang',$true)))) 'Selector must not appear for Wu Zhugeliang'
Require (!$scope.Invoke($null,@([MegaCrit.Sts2.Core.Models.Characters.Ironclad]))) 'Selector must not appear for vanilla characters'
Require (!$scope.Invoke($null,[object[]]@($null))) 'No selection must not crash or show a selector'
Require ($type.GetProperty('CustomMerchantAnimPath').DeclaringType -eq $type) 'Merchant must refresh selected skin assets'
$root=Join-Path $repo 'assets/loose/Spine/WeiDongZhuoTaiShan'
$manifest=Get-Content -LiteralPath (Join-Path $root 'skin.json') -Raw|ConvertFrom-Json
Require ($manifest.name -eq '泰山崩石' -and $manifest.version -eq '4.2.43') 'Skin name or Spine version mismatch'
Require ($manifest.assets.Count -eq 3 -and $manifest.hiddenCombatSlots.Count -eq 6) 'Expected lobby background, lobby body, body-only combat'
foreach($asset in $manifest.assets) {
    $directory=Join-Path $root $asset.folder
    foreach($name in @("$($asset.base).skel","$($asset.base).atlas")+$asset.pages) {
        Require (Test-Path -LiteralPath (Join-Path $directory $name)) "Skin resource missing: $name"
    }
    Require ($asset.duration -gt 6 -and $asset.focus.width -gt 0 -and $asset.focus.height -gt 0) 'Invalid idle animation bounds'
}
$skinSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Characters/WeiDongZhuo.cs') -Raw
Require ($skinSource.Contains('weidongzhuo_taishan_skin') -and $skinSource.Contains('taishan_select_bg.tscn')) 'Skin runtime/lobby route missing'
$selectSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Patches/WeiDongZhuoSkinSelectPatch.cs') -Raw
Require ($selectSource.Contains('泰山崩石') -and $selectSource.Contains('panel.Visible = false')) 'Named selector must hide for other characters'
$settingsSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Settings/WuZhugeliangSettings.cs') -Raw
Require ($settingsSource.Contains('settings.WeiDongZhuoSkin') -and $settingsSource.Contains('WeiDongZhuoSkinBinding.Save()')) 'Selection must persist through the shared settings store'
Require ($null -eq $assembly.GetType('Yjcm.CharacterAttackAnimationPatch')) 'Withdrawn attack animation must not return'
$playerSkinSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Characters/PlayerSkinSelection.cs') -Raw
foreach($required in @('RegisterPerPlayer<PlayerSkinSelection>','RunSavedDataLobbyStagingEvent','RunSavedDataPreparingEvent','Priority.First','LocalContext.IsMe(player)','Selections.TryGet(run, player.NetId','RunSavedDataWritePolicy.AlwaysWhenRegistered')) {
    Require ($playerSkinSource.Contains($required)) "Player skin sync/lifecycle missing: $required"
}
$nativeTargets=@(
    @([MegaCrit.Sts2.Core.Entities.Creatures.Creature],'CreateVisuals'),
    @([MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby],'SetReady'),
    @([MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby],'CleanUp'),
    @([MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom],'AfterRoomIsLoaded')
)
foreach($target in $nativeTargets) {
    Require ($null -ne $target[0].GetMethod($target[1],$flags)) "Native skin target missing: $($target[1])"
}
$merchantPlayers=[MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom].GetField('_players',$flags)
Require ($merchantPlayers.FieldType -eq [Collections.Generic.List[MegaCrit.Sts2.Core.Entities.Players.Player]]) 'Merchant owner order field changed'
$visualSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Patches/WuZhugeliangMeshVisualsPatch.cs') -Raw
Require ($visualSource.Contains('GetMeta(PlayerSkins.MouSkinMeta') -and !$visualSource.Contains('WuZhugeliangSettings.UseMouZhugeliangSkin')) 'Combat must read owner metadata, never the observer preference'
$runtimeSource=Get-Content -LiteralPath (Join-Path $repo 'mod/WeiDongZhuo/scripts/spine_runtime.gd') -Raw
Require ($runtimeSource.Contains('skin_meta(host,') -and !$runtimeSource.Contains('host.get_tree().root.get_meta')) 'Dong Zhuo runtime must prefer node-local skin data'
Write-Output "WEIDONGZHUO_SKIN_OK variant=$Variant assertions=$script:checks"
