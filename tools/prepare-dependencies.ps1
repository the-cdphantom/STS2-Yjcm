param([Parameter(Mandatory)][ValidateSet('stable','beta')][string]$Variant,[Parameter(Mandatory)][string]$SourceDirectory)
. (Join-Path $PSScriptRoot 'config.ps1')
$version=if($Variant -eq 'stable'){'0.111.0'}else{'0.114.0'}
$target=Join-Path $script:RepositoryRoot "dependencies/$Variant-$version"
$manifest=Get-Content -LiteralPath (Join-Path $target 'manifest.json') -Raw | ConvertFrom-Json
foreach ($file in $manifest.files) {
    $source=Join-Path $SourceDirectory $file.name
    if ((Get-FileHash -LiteralPath $source).Hash -ne $file.sha256) { throw "Wrong dependency version/content: $($file.name)" }
}
foreach ($file in $manifest.files) { Copy-Item -LiteralPath (Join-Path $SourceDirectory $file.name) -Destination (Join-Path $target $file.name) -Force }
Write-Output "DEPENDENCIES_OK $Variant $version"
