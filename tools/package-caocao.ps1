param()
. (Join-Path $PSScriptRoot 'config.ps1')
$repo=$script:RepositoryRoot
& pwsh -NoProfile -File (Join-Path $repo 'tools/package.ps1')
Assert-YjcmNativeExit 'Release package failed'
$config=Get-YjcmConfiguration
$dist=Join-Path $repo 'artifacts/caocao/dist/yjcm'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
foreach ($retiredName in @('Yjcm.0.107.1.dll','Yjcm.0.113.0.dll')) {
    $retiredDll = Join-Path $dist $retiredName
    if (Test-Path -LiteralPath $retiredDll -PathType Leaf) { Remove-Item -LiteralPath $retiredDll }
}
Get-ChildItem -LiteralPath (Join-Path $repo 'artifacts/dist/yjcm') -Force | Copy-Item -Destination $dist -Recurse -Force
& $config.GodotPath --headless --path (Join-Path $repo 'mod') --export-pack 'Windows Desktop' (Join-Path $dist 'Yjcm.pck') --quit 2>&1 | Out-File (Join-Path $repo 'artifacts/logs/caocao-export.log') -Encoding utf8
Assert-YjcmNativeExit 'Caocao Godot export failed'
& node (Join-Path $repo 'tools/normalize-pack.mjs') (Join-Path $dist 'Yjcm.pck') --include-caocao
Assert-YjcmNativeExit 'Caocao PCK normalization failed'
& $config.GodotPath --headless --path (Join-Path $repo 'mod') --log-file (Join-Path $repo 'artifacts/logs/caocao-layout-pack-test.log') --script (Join-Path $repo 'tests/check_caocao_layout.gd') -- "--pack=$(Join-Path $dist 'Yjcm.pck')" 2>&1 | Out-Host
Assert-YjcmNativeExit 'Caocao layout/frame regression failed'
foreach($file in @('Yjcm.dll','Yjcm.0.111.0.dll','Yjcm.0.114.0.dll')){
    Copy-Item -LiteralPath (Join-Path $repo "artifacts/caocao/staged/$file") -Destination $dist -Force
}
$experimental=Join-Path $repo 'assets/experimental/WeiCaoCao'
Get-ChildItem -LiteralPath $experimental -Force | Copy-Item -Destination $dist -Recurse -Force
$manifest=Get-Content -LiteralPath (Join-Path $repo 'mod/mod_manifest.json') -Raw | ConvertFrom-Json
$manifest.name+='（曹操开发版）'
$manifest.description+=' 当前为曹操开发测试包，请勿作为正式版发布。'
$manifest | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $dist 'mod_manifest.json') -Encoding utf8NoBOM
@{flavor='caocao';publishable=$false;developmentOnlyFiles=@(Get-ChildItem -LiteralPath $experimental -Recurse -File | ForEach-Object {Get-YjcmRuntimeRelativePath ([IO.Path]::GetRelativePath($experimental,$_.FullName))})} |
    ConvertTo-Json -Depth 6 | Set-Content (Join-Path $dist 'yjcm_build_profile.json.txt') -Encoding utf8NoBOM
Convert-YjcmRuntimeMetadata $dist
& pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao_delivery.ps1')
Assert-YjcmNativeExit 'Caocao package verification failed'
Write-Output "CAOCAO_PACKAGE_OK destination=$dist"
