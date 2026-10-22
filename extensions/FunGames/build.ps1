param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'New-Icon.ps1') -OutputPath (Join-Path $PSScriptRoot 'source\icon.png')
dotnet build (Join-Path $PSScriptRoot 'source\Osiris.FunGames.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Fun Games build failed: $LASTEXITCODE" }
