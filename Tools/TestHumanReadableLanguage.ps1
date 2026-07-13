param(
    [string[]]$SourceRoot,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

if (-not $SourceRoot -or $SourceRoot.Count -eq 0)
{
    $SourceRoot = @(
        (Join-Path $projectRoot "README.md"),
        (Join-Path $projectRoot "Docs"),
        (Join-Path $projectRoot "Assets\_Project\Code"),
        (Join-Path $projectRoot "Assets\_Project\Scripts"),
        (Join-Path $projectRoot "Assets\_Project\MyScripts"),
        (Join-Path $projectRoot "Assets\_Project\Tests"),
        (Join-Path $projectRoot "Tests"),
        (Join-Path $projectRoot "Tools")
    )
}

# 只审计明确面向人阅读的上下文；代码标识符、动画名、配置 ID 和文件路径保持英文。
$csharpPatterns = @(
    '\[(?:Header|Tooltip|MenuItem)\(\$?"(?<text>[^"]+)"',
    'CreateAssetMenu\([^\r\n]*menuName\s*=\s*\$?"(?<text>[^"]+)"',
    'Debug\.Log(?:Error|Warning)?\(\$?"(?<text>[^"]+)"',
    'throw\s+new\s+[A-Za-z0-9_.<>]+Exception\(\$?"(?<text>[^"]+)"',
    'Console\.(?:WriteLine|Error\.WriteLine)\(\$?"(?<text>[^"]+)"',
    'Spec\.(?:True|Equal|Throws)[^\r\n]*,\s*"(?<text>[^"]+)"\);',
    'Spec\.Run\("(?<text>[^"]+)"',
    'errors\.Add\(\$?"(?<text>[^"]+)"\)'
)
$powershellPatterns = @(
    'Write-(?:Output|Error|Warning|Host)\s+\$?"(?<text>[^"]+)"',
    'throw\s+\$?"(?<text>[^"]+)"',
    '\.Add\(\$?"(?<text>[^"]+)"\)'
)
$violations = [System.Collections.Generic.List[string]]::new()

function Test-ContainsChinese
{
    param([string]$Text)
    return $Text -match '[\u3400-\u9fff]'
}

function Add-CodeViolations
{
    param(
        [System.IO.FileInfo]$File,
        [string[]]$Patterns
    )

    $lines = @([System.IO.File]::ReadAllLines($File.FullName, [System.Text.UTF8Encoding]::new($false, $true)))
    for ($index = 0; $index -lt $lines.Count; $index++)
    {
        foreach ($pattern in $Patterns)
        {
            $match = [regex]::Match($lines[$index], $pattern)
            if ($match.Success -and -not (Test-ContainsChinese $match.Groups['text'].Value))
            {
                $relative = $File.FullName.Substring($projectRoot.Length).TrimStart('\', '/')
                $violations.Add("${relative}:$($index + 1) 面向人阅读的文本需要使用中文：$($match.Groups['text'].Value)")
            }
        }
    }
}

function Add-MarkdownViolations
{
    param([System.IO.FileInfo]$File)

    $insideCodeBlock = $false
    $lines = @([System.IO.File]::ReadAllLines($File.FullName, [System.Text.UTF8Encoding]::new($false, $true)))
    for ($index = 0; $index -lt $lines.Count; $index++)
    {
        $line = $lines[$index].Trim()
        if ($line.StartsWith('```'))
        {
            $insideCodeBlock = -not $insideCodeBlock
            continue
        }

        if ($insideCodeBlock -or $line.Length -eq 0 -or $line -eq '# Odyssey')
        {
            continue
        }

        if ($line -match '[A-Za-z]{4}' -and -not (Test-ContainsChinese $line))
        {
            $relative = $File.FullName.Substring($projectRoot.Length).TrimStart('\', '/')
            $violations.Add("${relative}:$($index + 1) Markdown 说明需要使用中文：$line")
        }
    }
}

$files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($root in $SourceRoot)
{
    if (Test-Path -LiteralPath $root -PathType Leaf)
    {
        $files.Add((Get-Item -LiteralPath $root))
    }
    elseif (Test-Path -LiteralPath $root -PathType Container)
    {
        Get-ChildItem -LiteralPath $root -Recurse -File |
            Where-Object { $_.Extension -in '.cs', '.ps1', '.md' } |
            ForEach-Object { $files.Add($_) }
    }
}

foreach ($file in $files)
{
    switch ($file.Extension)
    {
        '.cs' { Add-CodeViolations -File $file -Patterns $csharpPatterns }
        '.ps1' { Add-CodeViolations -File $file -Patterns $powershellPatterns }
        '.md' { Add-MarkdownViolations -File $file }
    }
}

if ($violations.Count -gt 0)
{
    if (-not $Quiet)
    {
        $violations | ForEach-Object { Write-Output "错误：$_" }
    }
    exit 1
}

if (-not $Quiet)
{
    Write-Output "通过：人类可读文本中文审计"
}

exit 0
