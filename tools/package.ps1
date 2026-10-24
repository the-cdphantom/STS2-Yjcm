param([string]$GodotPath)
. (Join-Path $PSScriptRoot 'config.ps1')
$repo = $script:RepositoryRoot
$fingerprint=Get-YjcmSourceFingerprint
foreach($v in @('stable','beta')) {
    $version=if($v -eq 'stable'){'0.107.1'}else{'0.111.0'}
    $stamp=Get-Content -LiteralPath (Join-Path $repo "artifacts/staged/$v.build.json") -Raw | ConvertFrom-Json
    if($stamp.sourceFingerprint -ne $fingerprint -or $stamp.dllSha256 -ne (Get-FileHash -LiteralPath (Join-Path $repo "artifacts/staged/Yjcm.$version.dll")).Hash) { throw "Stale or modified build: $v. Run tools/build.ps1 first." }
}
if (-not $GodotPath) { $GodotPath = (Get-YjcmConfiguration).GodotPath }
if (-not $GodotPath -or -not (Test-Path -LiteralPath $GodotPath)) { throw 'Set GodotPath in build.local.json or pass -GodotPath (Godot 4.5.1 Mono).' }
$dist = Join-Path $repo 'artifacts/dist/yjcm'
foreach($retired in Get-YjcmRetiredRuntimePaths (Join-Path $repo 'assets/loose')) {
    if(Test-Path -LiteralPath $retired){throw "Retired attack prototype still in source assets: $retired"}
}
New-Item -ItemType Directory -Path $dist -Force | Out-Null
# A pipeline also waits for Godot's GUI executable; plain redirection can return early.
& $GodotPath --headless --editor --path (Join-Path $repo 'mod') --quit 2>&1 | Out-File -LiteralPath (Join-Path $repo 'artifacts/logs/godot-import.log') -Encoding utf8
Assert-YjcmNativeExit 'Godot import failed'
& $GodotPath --headless --path (Join-Path $repo 'mod') --export-pack 'Windows Desktop' (Join-Path $dist 'Yjcm.pck') --quit 2>&1 | Out-File -LiteralPath (Join-Path $repo 'artifacts/logs/godot-export.log') -Encoding utf8
Assert-YjcmNativeExit 'Godot export failed'
& node (Join-Path $repo 'tools/normalize-pack.mjs') (Join-Path $dist 'Yjcm.pck')
Assert-YjcmNativeExit 'Unified PCK normalization failed'
foreach ($name in @('Yjcm.dll','Yjcm.0.107.1.dll','Yjcm.0.111.0.dll')) {
    Copy-Item -LiteralPath (Join-Path $repo "artifacts/staged/$name") -Destination (Join-Path $dist $name) -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'mod/mod_manifest.json') -Destination (Join-Path $dist 'mod_manifest.json') -Force
foreach ($folder in @('Voice','Spine','MeshAnimation')) {
    $source = Join-Path $repo "assets/loose/$folder"
    New-Item -ItemType Directory -Path (Join-Path $dist $folder) -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination (Join-Path $dist $folder) -Recurse -Force
}
foreach($retired in Get-YjcmRetiredRuntimePaths $dist) {
    if(Test-Path -LiteralPath $retired -PathType Leaf){Remove-Item -LiteralPath $retired}
}
New-Item -ItemType Directory -Path (Join-Path $dist '联动接口') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'docs/interop') -Force | Copy-Item -Destination (Join-Path $dist '联动接口') -Recurse -Force
& node (Join-Path $repo 'tests/audit_packs.mjs')
Assert-YjcmNativeExit 'PCK audit failed'
& $GodotPath --headless --path (Join-Path $repo 'mod') --log-file (Join-Path $repo 'artifacts/logs/portrait-pack-test.log') --script (Join-Path $repo 'tests/check_weidongzhuo_icon.gd') -- "--pack=$(Join-Path $dist 'Yjcm.pck')" | Out-Host
Assert-YjcmNativeExit 'Packaged Dong Zhuo portrait regression failed'
& $GodotPath --headless --path (Join-Path $repo 'mod') --log-file (Join-Path $repo 'artifacts/logs/taishan-layout-pack-test.log') --script (Join-Path $repo 'tests/check_weidongzhuo_skin_layout.gd') -- "--pack=$(Join-Path $dist 'Yjcm.pck')" | Out-Host
Assert-YjcmNativeExit 'Packaged TaiShan select layout regression failed'
& $GodotPath --headless --path (Join-Path $repo 'mod') --log-file (Join-Path $repo 'artifacts/logs/player-skin-pack-test.log') --script (Join-Path $repo 'tests/check_player_skin_metadata.gd') -- "--pack=$(Join-Path $dist 'Yjcm.pck')" | Out-Host
Assert-YjcmNativeExit 'Packaged per-player skin metadata regression failed'
& pwsh -NoProfile -File (Join-Path $repo 'tests/check_delivery.ps1')
Assert-YjcmNativeExit 'Package delivery audit failed'
Write-Output "PACKAGE_OK destination=$dist"
