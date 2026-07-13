param()

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$auditScript = Join-Path $projectRoot "Tools\TestHumanReadableLanguage.ps1"
$fixtureRoot = Join-Path $projectRoot "Temp\HumanReadableLanguageSpecs"

if (Test-Path -LiteralPath $fixtureRoot)
{
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

$englishSource = @"
[MenuItem("Tools/Build")]
public static void Build() { }
"@
Set-Content -LiteralPath (Join-Path $fixtureRoot "English.cs") -Value $englishSource -Encoding UTF8
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
if ($LASTEXITCODE -eq 0)
{
    throw "面向人阅读的英文文本被错误接受。"
}

Remove-Item -LiteralPath (Join-Path $fixtureRoot "English.cs") -Force
$chineseText = [string]([char]0x5DE5) + [char]0x5177
$chineseSource = "[MenuItem(`"$chineseText`/Build`" )]`npublic static void Build() { }`n"
[System.IO.File]::WriteAllText(
    (Join-Path $fixtureRoot "Chinese.cs"),
    $chineseSource,
    [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText(
    (Join-Path $fixtureRoot "README.md"),
    "## $chineseText`n",
    [System.Text.UTF8Encoding]::new($false))

& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
if ($LASTEXITCODE -ne 0)
{
    throw "面向人阅读的中文文本被错误拒绝。"
}

Write-Output "通过：人类可读文本中文审计规格"
