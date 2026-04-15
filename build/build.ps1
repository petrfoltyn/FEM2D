<#
.SYNOPSIS
    Sestaví FEALiTE2D solution.
.PARAMETER Configuration
    Konfigurace buildu: Debug nebo Release (výchozí: Release).
#>
param(
    [string]$Configuration = "Release"
)

$sln = Join-Path $PSScriptRoot "..\FEALiTE2D.sln"
dotnet build $sln -c $Configuration
