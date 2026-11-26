param([switch]$SkipReleaseBuild)
. (Join-Path $PSScriptRoot 'config.ps1')
$repo=$script:RepositoryRoot
& node (Join-Path $repo 'tools/generate_cards.mjs') --write
Assert-YjcmNativeExit 'Common card generation failed'
& node (Join-Path $repo 'tools/generate_caocao_cards.mjs') --write
Assert-YjcmNativeExit 'Caocao card generation failed'
if(!$SkipReleaseBuild){
    & pwsh -NoProfile -File (Join-Path $repo 'tools/build.ps1') -Variant all
    Assert-YjcmNativeExit 'Release verification failed'
}
$stage=Join-Path $repo 'artifacts/caocao/staged'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'artifacts/staged/Yjcm.dll') -Destination $stage -Force
foreach($v in @('stable','beta')){
    $version=if($v -eq 'stable'){'0.111.0'}else{'0.114.0'}
    $log=Join-Path $repo "artifacts/logs/caocao-build-$v.log"
    & dotnet build (Join-Path $repo 'mod/Yjcm.csproj') -c Release "-p:Variant=$v" -p:BuildFlavor=caocao -v quiet *> $log
    if($LASTEXITCODE -ne 0){Get-Content $log -Tail 25;throw "Caocao build failed: $v"}
    Copy-Item -LiteralPath (Join-Path $repo "artifacts/caocao/bin/Yjcm/$v/Release/Yjcm.dll") -Destination (Join-Path $stage "Yjcm.$version.dll") -Force
    foreach($mode in @('release','caocao')){
        & pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao.ps1') -Variant $v -Flavor $mode
        Assert-YjcmNativeExit "Caocao isolation regression failed: $v/$mode"
    }
    & pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao_starters.ps1') -Variant $v
    Assert-YjcmNativeExit "Caocao starter mechanics regression failed: $v"
    @{sourceFingerprint=Get-YjcmSourceFingerprint -Flavor caocao;flavor='caocao';testsPassed=$true;dllSha256=(Get-FileHash (Join-Path $stage "Yjcm.$version.dll")).Hash} |
        ConvertTo-Json | Set-Content (Join-Path $stage "$v.build.json") -Encoding utf8NoBOM
}
Write-Output "CAOCAO_BUILD_OK stage=$stage"
