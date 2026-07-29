$ErrorActionPreference = 'Stop'

$overlayProject = Join-Path $PSScriptRoot '..\src\Guard.Overlay\Guard.Overlay.csproj'
$outputDirectory = Join-Path $PSScriptRoot '..\publish\Guard.Overlay'

dotnet publish $overlayProject -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o $outputDirectory
