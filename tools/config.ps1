$ErrorActionPreference = 'Stop'
$script:RepositoryRoot = Split-Path $PSScriptRoot -Parent
# Loose metadata must not match the game's recursive *.json mod-manifest scan.
$script:YjcmRuntimeMetadata = @('yjcm_build_profile.json','Spine/CardEffects/index.json',
    'Spine/WeiDongZhuoTaiShan/skin.json','Spine/WeiCaoCao/character.json','Spine/WeiCaoCao/CardEffects/index.json')
function Get-YjcmRuntimeRelativePath([string]$Relative) {
    $normalized=$Relative.Replace('\','/')
    if($normalized -in $script:YjcmRuntimeMetadata){return $normalized+'.txt'}
    return $Relative
}
function Convert-YjcmRuntimeMetadata([string]$Root) {
    foreach($relative in $script:YjcmRuntimeMetadata) {
        $path=Join-Path $Root $relative
        if(Test-Path -LiteralPath $path -PathType Leaf){Move-Item -LiteralPath $path -Destination ($path+'.txt') -Force}
    }
}
function Get-YjcmRetiredMetadataPaths([string]$Root) {
    foreach($relative in $script:YjcmRuntimeMetadata){Join-Path $Root $relative}
}
function Get-YjcmRetiredRuntimePaths([string]$Root) {
    # Exact retired prototype files only. Never mirror-delete unknown mod/user files.
    $rootPath=[IO.Path]::GetFullPath($Root)
    $rootPrefix=$rootPath.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    foreach($relative in @('Spine/WeiDongZhuo/Combat/character_attack.skel','Spine/MouZhugeliang/Combat/character_attack.skel')) {
        $path=[IO.Path]::GetFullPath((Join-Path $rootPath $relative))
        if(!$path.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Retired resource outside runtime root'}
        $path
    }
}
function Get-YjcmConfiguration {
    $configFile = Join-Path $script:RepositoryRoot 'build.local.json'
    $config = if (Test-Path -LiteralPath $configFile) { Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    return $config
}
function Assert-YjcmNativeExit([string]$Message) {
    if ($LASTEXITCODE -ne 0) { throw "$Message (exit=$LASTEXITCODE)" }
}
function Get-YjcmSourceFingerprint([ValidateSet('release','caocao')][string]$Flavor='release') {
    $repo=$script:RepositoryRoot
    $files=@(Get-ChildItem -LiteralPath (Join-Path $repo 'mod/src') -Recurse -File -Filter '*.cs')
    $files+=@(Get-ChildItem -LiteralPath (Join-Path $repo 'loader') -File | Where-Object Extension -in @('.cs','.csproj'))
    foreach($relative in @('mod/Yjcm.csproj','Directory.Build.props','dependencies/GameReferences.props','dependencies/stable-0.111.0/manifest.json','dependencies/beta-0.114.0/manifest.json')) { $files+=Get-Item -LiteralPath (Join-Path $repo $relative) }
    $lines=@($files|Sort-Object FullName|ForEach-Object { [IO.Path]::GetRelativePath($repo,$_.FullName)+':'+(Get-FileHash -LiteralPath $_.FullName).Hash })
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($Flavor+"`n"+($lines -join "`n")))))
}
