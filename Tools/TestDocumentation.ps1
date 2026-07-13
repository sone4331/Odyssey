param(
    [string[]]$SourceRoot,
    [string]$ExclusionFile,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SourceRoot -or $SourceRoot.Count -eq 0)
{
    $SourceRoot = @(
        (Join-Path $projectRoot "Assets\_Project\Code"),
        (Join-Path $projectRoot "Assets\_Project\Scripts"),
        (Join-Path $projectRoot "Assets\_Project\MyScripts")
    )
}

if (-not $ExclusionFile)
{
    $ExclusionFile = Join-Path $projectRoot "Tools\DocumentationAuditExclusions.txt"
}

# 该审计器只承担“核心生产类型是否存在架构说明”的最低门禁。
# 注释是否真正解释职责、模式与设计原因仍需代码审查判断，避免用正则替代工程判断。
$typePattern = '^\s*(public|internal)\s+(?:(?:sealed|static|abstract|readonly|partial)\s+)*(class|interface|struct|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)'
$violations = [System.Collections.Generic.List[string]]::new()
$exclusions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
if (Test-Path -LiteralPath $ExclusionFile)
{
    foreach ($entry in Get-Content -LiteralPath $ExclusionFile)
    {
        $normalized = $entry.Trim().Replace('\', '/')
        if ($normalized.Length -gt 0 -and -not $normalized.StartsWith('#'))
        {
            [void]$exclusions.Add($normalized)
        }
    }
}

foreach ($root in $SourceRoot)
{
    if (-not (Test-Path -LiteralPath $root))
    {
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Filter *.cs -File)
    {
        $relativePath = $file.FullName.Substring($projectRoot.Length).TrimStart('\', '/').Replace('\', '/')
        if ($exclusions.Contains($relativePath))
        {
            continue
        }

        # C# 文件统一为无 BOM UTF-8；显式指定编码，避免 Windows PowerShell 5.1 按本地代码页破坏中文换行。
        $lines = @([System.IO.File]::ReadAllLines($file.FullName, [System.Text.UTF8Encoding]::new($false, $true)))
        for ($index = 0; $index -lt $lines.Count; $index++)
        {
            $match = [regex]::Match($lines[$index], $typePattern)
            if (-not $match.Success)
            {
                continue
            }

            $cursor = $index - 1
            while ($cursor -ge 0 -and ($lines[$cursor].Trim().Length -eq 0 -or $lines[$cursor].TrimStart().StartsWith('[')))
            {
                $cursor--
            }

            if ($cursor -lt 0 -or $lines[$cursor].Trim() -ne '/// </summary>')
            {
                $violations.Add("${relativePath}:$($index + 1) $($match.Groups[2].Value) '$($match.Groups[3].Value)' is missing XML architecture documentation.")
            }
        }
    }
}

if ($violations.Count -gt 0)
{
    if (-not $Quiet)
    {
        $violations | ForEach-Object { Write-Output "ERROR: $_" }
    }

    exit 1
}

if (-not $Quiet)
{
    Write-Output "PASS: production type documentation audit"
}

exit 0
