param([ValidateSet('stable','beta')][string]$Variant='beta',[ValidateSet('release','caocao')][string]$Flavor='caocao')
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'tools/config.ps1')
$repo=$script:RepositoryRoot
$version=if($Variant -eq 'stable'){'0.111.0'}else{'0.114.0'}
$log=Join-Path $repo "artifacts/logs/caocao-starter-tests-$Variant.log"
& dotnet build (Join-Path $repo 'tests/CaoCaoTests/CaoCaoTests.csproj') -c Release "-p:Variant=$Variant" -v quiet *> $log
if($LASTEXITCODE -ne 0){Get-Content $log -Tail 25;throw 'Caocao test build failed'}
$testDll=Join-Path $repo "artifacts/bin/CaoCaoTests/$Variant/Release/net9.0/CaoCaoTests.dll"
$stage=if($Flavor -eq 'release'){'artifacts/staged'}else{'artifacts/caocao/staged'}
$modDll=Join-Path $repo "$stage/Yjcm.$version.dll"
& dotnet $testDll $modDll (Join-Path $repo "dependencies/$Variant-$version") $repo 2>&1 | Tee-Object -FilePath $log -Append
Assert-YjcmNativeExit 'Caocao starter behavior checks failed'
