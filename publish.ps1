# Buduje Kasetownik 9000 (Native AOT):
#   publish\win-<arch>\          - folder z aplikacja (exe + biblioteki Windows App SDK)
#   dist\Kasetownik9000.exe      - JEDEN plik do rozdania (aplikacja spakowana w srodku)
#
#   powershell -ExecutionPolicy Bypass -File publish.ps1
#   powershell -ExecutionPolicy Bypass -File publish.ps1 -Arch arm64
#   powershell -ExecutionPolicy Bypass -File publish.ps1 -NoSingleFile
param(
    [ValidateSet('x64', 'arm64')][string]$Arch = 'x64',
    [switch]$NoSingleFile
)
$ErrorActionPreference = 'Stop'

$app = Join-Path $PSScriptRoot 'src\Kasetownik9000\Kasetownik9000.csproj'
$launcher = Join-Path $PSScriptRoot 'src\Kasetownik9000.Launcher\Kasetownik9000.Launcher.csproj'
$out = Join-Path $PSScriptRoot "publish\win-$Arch"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet publish $app -c Release -r "win-$Arch" -o $out
if ($LASTEXITCODE -ne 0) { throw "Publikacja aplikacji nie powiodla sie (kod $LASTEXITCODE)." }

$size = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Folder:      {0}  ({1:N0} MB)" -f $out, $size) -ForegroundColor Green

if (-not $NoSingleFile) {
    $dist = Join-Path $PSScriptRoot 'dist'
    $name = if ($Arch -eq 'x64') { 'Kasetownik9000.exe' } else { "Kasetownik9000-$Arch.exe" }
    $tmp = Join-Path $PSScriptRoot "artifacts\launcher-$Arch"
    if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }

    # AppPublishDir bez koncowego "\" (inaczej \" psuje cudzyslow w linii polecen)
    dotnet publish $launcher -c Release -r "win-$Arch" -o $tmp "-p:AppPublishDir=$out"
    if ($LASTEXITCODE -ne 0) { throw "Budowanie pliku jednoplikowego nie powiodlo sie (kod $LASTEXITCODE)." }

    New-Item -ItemType Directory -Force $dist | Out-Null
    Copy-Item (Join-Path $tmp 'Kasetownik9000.exe') (Join-Path $dist $name) -Force
    $one = Get-Item (Join-Path $dist $name)
    Write-Host ("Jeden plik:  {0}  ({1:N0} MB)" -f $one.FullName, ($one.Length / 1MB)) -ForegroundColor Green
}
