param([string]$GamePath,[switch]$CaoCao)
. (Join-Path $PSScriptRoot 'config.ps1')
if (-not $GamePath) { $GamePath = (Get-YjcmConfiguration).GamePath }
if (-not $GamePath -or -not (Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))) { throw 'Pass the real game directory using -GamePath or build.local.json.' }
if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) { throw 'Close the game before deployment; nothing was changed.' }
$repo = $script:RepositoryRoot
if($CaoCao){ & pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao_delivery.ps1') }
else{ & pwsh -NoProfile -File (Join-Path $repo 'tests/check_delivery.ps1') }
Assert-YjcmNativeExit 'Package not verified'
$target = [IO.Path]::GetFullPath((Join-Path $GamePath 'mods/yjcm'))
if ((Split-Path $target -Leaf) -ne 'yjcm') { throw 'Deployment must stay in mods/yjcm' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
$sourceRoot=Join-Path $repo $(if($CaoCao){'artifacts/caocao/dist/yjcm'}else{'artifacts/dist/yjcm'})
$changed=@(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
    $destination=Join-Path $target ([IO.Path]::GetRelativePath($sourceRoot,$_.FullName))
    (Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash
})
$retired=@(Get-YjcmRetiredRuntimePaths $target | Where-Object {Test-Path -LiteralPath $_ -PathType Leaf})
$retired+=@(Get-YjcmRetiredMetadataPaths $target | Where-Object {Test-Path -LiteralPath $_ -PathType Leaf})
foreach($retiredDllName in @('Yjcm.0.107.1.dll','Yjcm.0.113.0.dll')){
    $retiredDll=Join-Path $target $retiredDllName
    if(Test-Path -LiteralPath $retiredDll -PathType Leaf){$retired += $retiredDll}
}
$installedProfile=Join-Path $target 'yjcm_build_profile.json.txt'
if(!(Test-Path -LiteralPath $installedProfile)){$installedProfile=Join-Path $target 'yjcm_build_profile.json'}
if(Test-Path -LiteralPath $installedProfile){
    $installed=Get-Content -LiteralPath $installedProfile -Raw | ConvertFrom-Json
    if($installed.flavor -eq 'caocao'){
        foreach($relative in $installed.developmentOnlyFiles){
            $full=[IO.Path]::GetFullPath((Join-Path $target $relative))
            $allowed=@('Spine/WeiCaoCao','Voice/WeiCaoCao')|ForEach-Object {[IO.Path]::GetFullPath((Join-Path $target $_))+[IO.Path]::DirectorySeparatorChar}
            if(!($allowed|Where-Object {$full.StartsWith($_,[StringComparison]::OrdinalIgnoreCase)})){throw 'Experimental cleanup outside exact CaoCao resource directories'}
            $relative=[IO.Path]::GetRelativePath($target,$full)
            if(!(Test-Path -LiteralPath (Join-Path $sourceRoot $relative) -PathType Leaf) -and (Test-Path -LiteralPath $full -PathType Leaf)){$retired+= $full}
        }
    }
}
$backupFiles=@($changed | ForEach-Object {
    Get-Item -LiteralPath (Join-Path $target ([IO.Path]::GetRelativePath($sourceRoot,$_.FullName)))
}) + @($retired | ForEach-Object {Get-Item -LiteralPath $_})
if($backupFiles.Count) {
    $backupRoot=Join-Path $repo 'artifacts/deploy-backups'
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $archive=Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd_HHmmss_fff')+'.zip')
    $zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
    try { foreach($file in $backupFiles) {
        $relative=[IO.Path]::GetRelativePath($target,$file.FullName)
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative.Replace('\','/'))
    }} finally {$zip.Dispose()}
    # Keep only the newest three exact ZIP files under this dedicated backup directory.
    $backups=@(Get-ChildItem -LiteralPath $backupRoot -File -Filter '*.zip'|Sort-Object Name -Descending|Select-Object -Skip 3)
    foreach($file in $backups) {
        if([IO.Path]::GetFullPath($file.DirectoryName) -ne [IO.Path]::GetFullPath($backupRoot)) {throw 'Backup outside expected directory'}
        Remove-Item -LiteralPath $file.FullName
    }
}
Get-ChildItem -LiteralPath $sourceRoot -Force | Copy-Item -Destination $target -Recurse -Force
foreach($file in $retired) {
    Remove-Item -LiteralPath $file
    Write-Output "REMOVED_RETIRED_RESOURCE $file (saved in deployment backup)"
}
if($CaoCao){ & pwsh -NoProfile -File (Join-Path $repo 'tests/check_caocao_delivery.ps1') -Target $target }
else{ & pwsh -NoProfile -File (Join-Path $repo 'tests/check_delivery.ps1') -CheckSteam -GamePath $GamePath }
Assert-YjcmNativeExit 'Deployed files failed verification'
Write-Output "DEPLOY_OK destination=$target"
