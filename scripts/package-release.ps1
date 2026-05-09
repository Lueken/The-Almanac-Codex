param([string]$OutDir = "$env:USERPROFILE\Desktop")
$root = Split-Path -Parent $PSScriptRoot
$modinfo = Get-Content "$root\modinfo.json" -Raw | ConvertFrom-Json
$version = $modinfo.version
$stage = "$env:TEMP\almanaccodex_pkg"
$out = Join-Path $OutDir "almanaccodex_$version.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item "$root\modinfo.json" $stage
Copy-Item "$root\bin\Release\Mods\AlmanacCodex.dll" $stage
Copy-Item "$root\assets" $stage -Recurse
if (Test-Path $out) { Remove-Item $out -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $out -Force
$f = Get-Item $out
Write-Output ("Built: " + $f.FullName + " (" + [math]::Round($f.Length/1KB,1) + " KB)")
