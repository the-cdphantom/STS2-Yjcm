param([ValidateSet('stable','beta','all')][string]$Variant='all',[switch]$SkipTests)
. (Join-Path $PSScriptRoot 'config.ps1')
$repo = $script:RepositoryRoot
& node (Join-Path $repo 'tools/generate_caocao_cards.mjs')
Assert-YjcmNativeExit 'Caocao generator drift'
$variants = if ($Variant -eq 'all') { @('stable','beta') } else { @($Variant) }
$logs = Join-Path $repo ('artifacts/logs/build_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$stage = Join-Path $repo 'artifacts/staged'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach ($retiredName in @('Yjcm.0.107.1.dll','Yjcm.0.113.0.dll')) {
    $retiredPath = Join-Path $stage $retiredName
    if (Test-Path -LiteralPath $retiredPath) { Remove-Item -LiteralPath $retiredPath }
}
& dotnet build (Join-Path $repo 'loader/YjcmLoader.csproj') -c Release -v quiet *> (Join-Path $logs 'loader.log')
Assert-YjcmNativeExit 'Loader build failed; see loader.log'
Copy-Item -LiteralPath (Join-Path $repo 'artifacts/bin/YjcmLoader/stable/Release/net9.0/Yjcm.Loader.dll') -Destination (Join-Path $stage 'Yjcm.dll') -Force
foreach ($v in $variants) {
    $version = if ($v -eq 'stable') { '0.111.0' } else { '0.114.0' }
    $snapshotRoot = Join-Path $repo "dependencies/$v-$version"
    $snapshot = Get-Content -LiteralPath (Join-Path $snapshotRoot 'manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $snapshot.files) {
        if ((Get-FileHash -LiteralPath (Join-Path $snapshotRoot $file.name)).Hash -ne $file.sha256) { throw "Dependency changed: $v/$($file.name)" }
    }
    $project=Join-Path $repo 'mod/Yjcm.csproj'
    $referenceJson=& dotnet msbuild $project "-p:Variant=$v" -p:BuildFlavor=release -getItem:Reference
    Assert-YjcmNativeExit "Reference evaluation failed: $v"
    $references=($referenceJson -join [Environment]::NewLine|ConvertFrom-Json).Items.Reference
    foreach($name in @('sts2','GodotSharp','0Harmony','STS2-RitsuLib','STS2-RitsuLib.Runtime','STS2-RitsuLib.Shared','STS2-RitsuLib.Settings','STS2-RitsuLib.Ui')) {
        $reference=@($references|Where-Object Identity -eq $name)
        if($reference.Count -ne 1 -or [IO.Path]::GetFullPath($reference[0].HintPath) -ne (Join-Path $snapshotRoot "$name.dll")) { throw "Unpinned reference: $v/$name" }
    }
    & node (Join-Path $repo 'tools/generate_cards.mjs') --variant $v --check
    Assert-YjcmNativeExit "Generator drift: $v"
    & dotnet build (Join-Path $repo 'mod/Yjcm.csproj') -c Release "-p:Variant=$v" -p:BuildFlavor=release -v quiet *> (Join-Path $logs "$v.log")
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $logs "$v.log") -Tail 50; throw "Build failed: $v" }
    Copy-Item -LiteralPath (Join-Path $repo "artifacts/bin/Yjcm/$v/Release/Yjcm.dll") -Destination (Join-Path $stage "Yjcm.$version.dll") -Force
    & pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao.ps1') -Variant $v -Flavor release
    Assert-YjcmNativeExit "Release Cao Cao registration failed: $v"
    if (-not $SkipTests) {
        foreach ($test in @('check_assembly','check_guang_yong','check_liecang_ui','check_interop','check_card_effects','check_shangfanggu','check_character_attacks','check_collaboration_cards','check_weidongzhuo_skin')) {
            $arguments = @('-Variant',$v)
            if ($test -eq 'check_assembly') { $arguments += @('-Mode','test') }
            & pwsh -NoProfile -File (Join-Path $repo "tests/$test.ps1") @arguments
            Assert-YjcmNativeExit "$test failed: $v"
        }
        & pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao_starters.ps1') -Variant $v -Flavor release
        Assert-YjcmNativeExit "Cao Cao mechanics regression failed: $v"
    }
    $stamp=@{variant=$v;sourceFingerprint=Get-YjcmSourceFingerprint;dllSha256=(Get-FileHash -LiteralPath (Join-Path $stage "Yjcm.$version.dll")).Hash;testsPassed=(-not $SkipTests)}
    [IO.File]::WriteAllText((Join-Path $stage "$v.build.json"),($stamp|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    Write-Output "PINNED_BUILD_OK $v"
}
Write-Output "BUILD_OK variants=$($variants -join ',') logs=$logs"
