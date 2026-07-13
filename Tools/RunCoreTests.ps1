param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$unityData = "D:\Unity\Hub\Editor\2023.2.20f1c1\Editor\Data"
$compiler = Join-Path $unityData "MonoBleedingEdge\lib\mono\4.5\csc.exe"
$mono = Join-Path $unityData "MonoBleedingEdge\bin\mono.exe"
$outputDirectory = Join-Path $projectRoot "Temp\CoreTests"
$outputAssembly = Join-Path $outputDirectory "Odyssey.Core.Specs.exe"

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$sources = @(
    Get-ChildItem (Join-Path $projectRoot "Assets\_Project\Code\Core") -Recurse -Filter *.cs -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $projectRoot "Assets\_Project\Code\Gameplay") -Recurse -Filter *.cs -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $projectRoot "Tests\Core") -Recurse -Filter *.cs
) | ForEach-Object { $_.FullName }

if ($sources.Count -eq 0)
{
    throw "未找到核心测试源文件。"
}

& $mono $compiler -nologo -langversion:latest -target:exe -out:$outputAssembly $sources
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

& $mono $outputAssembly
exit $LASTEXITCODE
