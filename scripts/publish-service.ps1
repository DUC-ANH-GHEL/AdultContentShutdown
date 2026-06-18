$ErrorActionPreference = 'Stop'

$serviceProject = Join-Path $PSScriptRoot '..\src\Guard.Service\Guard.Service.csproj'
$outputDirectory = Join-Path $PSScriptRoot '..\publish\Guard.Service'

dotnet publish $serviceProject -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o $outputDirectory
