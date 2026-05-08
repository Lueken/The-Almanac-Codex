param([switch]$NoBuild)

# Dev-loop deploy: rebuild + copy DLL/pdb/modinfo/lang into VS Mods folder.
# Usage:
#   ./scripts/deploy-dev.ps1            # builds first, then deploys
#   ./scripts/deploy-dev.ps1 -NoBuild   # skips the dotnet build step

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dst = Join-Path $env:APPDATA 'VintagestoryData\Mods\almanaccodex'

if (-not $NoBuild) {
    Write-Host 'Building...' -ForegroundColor Cyan
    Push-Location $root
    try { dotnet build } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }
}

if (-not (Test-Path $dst)) {
    throw "Deploy target not found: $dst. Install the Codex mod once via VS, then re-run."
}

Copy-Item "$root\bin\Debug\Mods\AlmanacCodex.dll" "$dst\AlmanacCodex.dll" -Force
Copy-Item "$root\bin\Debug\Mods\AlmanacCodex.pdb" "$dst\AlmanacCodex.pdb" -Force
Copy-Item "$root\modinfo.json" "$dst\modinfo.json" -Force

# Mirror the assets tree so new lang keys + future asset additions land cleanly.
$assetsDst = Join-Path $dst 'assets'
if (Test-Path $assetsDst) { Remove-Item $assetsDst -Recurse -Force }
Copy-Item "$root\assets" $assetsDst -Recurse -Force

Write-Host "Deployed to $dst" -ForegroundColor Green
Write-Host 'Restart Vintage Story to pick up the new DLL.' -ForegroundColor Yellow
