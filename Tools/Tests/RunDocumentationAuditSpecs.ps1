param()

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$auditScript = Join-Path $projectRoot "Tools\TestDocumentation.ps1"
$fixtureRoot = Join-Path $projectRoot "Temp\DocumentationAuditSpecs"

if (Test-Path -LiteralPath $fixtureRoot)
{
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

function Invoke-AuditFixture
{
    param([string]$Source)

    $file = Join-Path $fixtureRoot "Fixture.cs"
    Set-Content -LiteralPath $file -Value $Source -Encoding UTF8
    & powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
    return $LASTEXITCODE
}

$documented = @"
/// <summary>
/// 验证架构注释审计的测试边界。
/// </summary>
public sealed class DocumentedType { }
"@

$undocumented = @"
public sealed class UndocumentedType { }
"@

if ((Invoke-AuditFixture -Source $documented) -ne 0)
{
    throw "已编写架构说明的生产类型被错误拒绝。"
}

$chineseText = [string]([char]0x804C) + [char]0x8D23
$utf8WithoutBom = "/// <summary>`n/// $chineseText`n/// </summary>`npublic sealed class Utf8DocumentedType { }`n"
$utf8Fixture = Join-Path $fixtureRoot "Utf8Fixture.cs"
[System.IO.File]::WriteAllText($utf8Fixture, $utf8WithoutBom, [System.Text.UTF8Encoding]::new($false))
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
if ($LASTEXITCODE -ne 0)
{
    throw "无 BOM 的 UTF-8 架构说明被错误拒绝。"
}
Remove-Item -LiteralPath $utf8Fixture -Force

$utf8Undocumented = "// $chineseText`npublic sealed class Utf8UndocumentedType { }`n"
[System.IO.File]::WriteAllText($utf8Fixture, $utf8Undocumented, [System.Text.UTF8Encoding]::new($false))
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
if ($LASTEXITCODE -eq 0)
{
    throw "未检测到无 BOM UTF-8 文件中缺少架构说明的类型。"
}
Remove-Item -LiteralPath $utf8Fixture -Force

if ((Invoke-AuditFixture -Source $undocumented) -eq 0)
{
    throw "缺少架构说明的生产类型被错误接受。"
}

$multipleUndocumented = @"
public sealed class FirstUndocumentedType { }
public interface SecondUndocumentedType { }
"@
$fixtureFile = Join-Path $fixtureRoot "Fixture.cs"
Set-Content -LiteralPath $fixtureFile -Value $multipleUndocumented -Encoding UTF8
$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$auditOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot 2>&1
$ErrorActionPreference = $previousErrorAction
if ($LASTEXITCODE -eq 0 -or
    -not ($auditOutput -match 'FirstUndocumentedType') -or
    -not ($auditOutput -match 'SecondUndocumentedType'))
{
    throw "审计未报告全部缺少架构说明的生产类型。"
}

$exclusionFile = Join-Path $fixtureRoot "Exclusions.txt"
Set-Content -LiteralPath $fixtureFile -Value $undocumented -Encoding UTF8
Set-Content -LiteralPath $exclusionFile -Value "Temp/DocumentationAuditSpecs/Fixture.cs" -Encoding ASCII
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -ExclusionFile $exclusionFile -Quiet
if ($LASTEXITCODE -ne 0)
{
    throw "明确列入白名单的遗留文件被错误拒绝。"
}

Write-Output "通过：架构注释审计规格"
