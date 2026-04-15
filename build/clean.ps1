<#
.SYNOPSIS
    Vyčistí všechny build výstupy FEALiTE2D solution.
    Spustí dotnet clean a odstraní adresář /out.
#>

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$sln  = Join-Path $root "FEALiTE2D.sln"

Write-Host "Running dotnet clean..."
dotnet clean $sln

# /out není standardní bin/obj, dotnet clean ho nevymaže
$outDir = Join-Path $root "out"
if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
    Write-Host "Removed: $outDir"
}

Write-Host "Clean completed."
