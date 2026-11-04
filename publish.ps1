# Buduje Kasetownik 9000 w trybie Native AOT do folderu publish\win-<arch>
#   powershell -ExecutionPolicy Bypass -File publish.ps1            (x64)
#   powershell -ExecutionPolicy Bypass -File publish.ps1 -Arch arm64
param([ValidateSet('x64', 'arm64')][string]$Arch = 'x64')
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\Kasetownik9000\Kasetownik9000.csproj'
$out = Join-Path $PSScriptRoot "publish\win-$Arch"

dotnet publish $project -c Release -r "win-$Arch" -o $out
if ($LASTEXITCODE -ne 0) { throw "Publikacja nie powiodla sie (kod $LASTEXITCODE)." }

$size = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Gotowe: {0}\Kasetownik9000.exe  ({1:N0} MB w folderze)" -f $out, $size) -ForegroundColor Green
