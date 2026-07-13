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
/// Verifies the fixture boundary for the documentation audit.
/// </summary>
public sealed class DocumentedType { }
"@

$undocumented = @"
public sealed class UndocumentedType { }
"@

if ((Invoke-AuditFixture -Source $documented) -ne 0)
{
    throw "Documented production type was rejected."
}

$chineseText = [string]([char]0x804C) + [char]0x8D23
$utf8WithoutBom = "/// <summary>`n/// $chineseText`n/// </summary>`npublic sealed class Utf8DocumentedType { }`n"
$utf8Fixture = Join-Path $fixtureRoot "Utf8Fixture.cs"
[System.IO.File]::WriteAllText($utf8Fixture, $utf8WithoutBom, [System.Text.UTF8Encoding]::new($false))
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
if ($LASTEXITCODE -ne 0)
{
    throw "UTF-8 documentation without BOM was rejected."
}
Remove-Item -LiteralPath $utf8Fixture -Force

$utf8Undocumented = "// $chineseText`npublic sealed class Utf8UndocumentedType { }`n"
[System.IO.File]::WriteAllText($utf8Fixture, $utf8Undocumented, [System.Text.UTF8Encoding]::new($false))
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -Quiet
if ($LASTEXITCODE -eq 0)
{
    throw "UTF-8 undocumented type without BOM was not detected."
}
Remove-Item -LiteralPath $utf8Fixture -Force

if ((Invoke-AuditFixture -Source $undocumented) -eq 0)
{
    throw "Undocumented production type was accepted."
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
    throw "Audit did not report every undocumented production type."
}

$exclusionFile = Join-Path $fixtureRoot "Exclusions.txt"
Set-Content -LiteralPath $fixtureFile -Value $undocumented -Encoding UTF8
Set-Content -LiteralPath $exclusionFile -Value "Temp/DocumentationAuditSpecs/Fixture.cs" -Encoding ASCII
& powershell -NoProfile -ExecutionPolicy Bypass -File $auditScript -SourceRoot $fixtureRoot -ExclusionFile $exclusionFile -Quiet
if ($LASTEXITCODE -ne 0)
{
    throw "Explicitly excluded legacy file was rejected."
}

Write-Output "PASS: documentation audit specifications"
