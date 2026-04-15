<#
.SYNOPSIS
    Spustí testy FEALiTE2D solution.
.PARAMETER Configuration
    Konfigurace buildu: Debug nebo Release (výchozí: Debug).
.PARAMETER Filter
    Volitelný filtr testů předaný do --filter.
    Příklady:
      .\test.ps1 -Filter "FullyQualifiedName~StructureTest"
      .\test.ps1 -Filter "TestCategory=Beam"
#>
param(
    [string]$Configuration = "Debug",
    [string]$Filter = ""
)

$sln = Join-Path $PSScriptRoot "..\FEALiTE2D.sln"

if ($Filter) {
    dotnet test $sln -c $Configuration --filter $Filter
} else {
    dotnet test $sln -c $Configuration
}
