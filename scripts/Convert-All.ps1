[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $RepositoryRoot,

    [switch] $Force
)

$ErrorActionPreference = 'Stop'

function Get-Sha256 {
    param([Parameter(Mandatory)][string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-MarpDocument {
    param([Parameter(Mandatory)][string] $Path)
    $content = [IO.File]::ReadAllText($Path)
    return $content -match '(?im)^\s*marp\s*:\s*true\s*$|<!--\s*marp\b|^\s*<!--\s*_(?:class|paginate|header|footer)\s*:'
}

function Test-NoPdfDocument {
    param([Parameter(Mandatory)][string] $Path)
    $reader = [IO.File]::OpenText($Path)
    try {
        $firstLine = $reader.ReadLine()
    } finally {
        $reader.Dispose()
    }
    return $null -ne $firstLine -and $firstLine.Trim() -ceq '<!--no-pdf-->'
}

function Remove-ConversionState {
    param([Parameter(Mandatory)][string] $Path)
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Remove-Item -LiteralPath $Path -Force
    }
}

$repositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$pipelineRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$cliProject = Join-Path $pipelineRoot 'src\RensMarkdownTemplates.Cli\RensMarkdownTemplates.Cli.csproj'
$cliDll = Join-Path $pipelineRoot 'src\RensMarkdownTemplates.Cli\bin\Release\net9.0\RensMarkdownTemplates.Cli.dll'
$pdfIgnorePath = Join-Path $repositoryRootPath '.pdfignore'
$cacheRoot = Join-Path $repositoryRootPath '.cache'
$stateDirectory = Join-Path $cacheRoot 'md-to-pdf\state'
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null

& dotnet build $cliProject --configuration Release
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $cliDll -PathType Leaf)) {
    throw 'Unable to build the shared Markdown-to-PDF CLI.'
}

$pipelineFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $pipelineRoot 'src') -File -Recurse
    Get-ChildItem -LiteralPath (Join-Path $pipelineRoot 'templates') -File -Recurse
    Get-Item -LiteralPath (Join-Path $pipelineRoot '.mmdc.json')
    Get-Item -LiteralPath (Join-Path $pipelineRoot 'package-lock.json')
    Get-Item -LiteralPath (Join-Path $pipelineRoot 'puppeteer-config.json')
    Get-Item -LiteralPath $PSCommandPath
)
$pipelineSignature = ($pipelineFiles | Sort-Object FullName | ForEach-Object {
    Get-Sha256 -Path $_.FullName
}) -join "`n"
$pipelineHashBytes = [Text.Encoding]::UTF8.GetBytes($pipelineSignature)
$pipelineHash = ([BitConverter]::ToString(
    [Security.Cryptography.SHA256]::Create().ComputeHash($pipelineHashBytes)
)).Replace('-', '').ToLowerInvariant()

$relativeMarkdownPaths = @(
    git -C $repositoryRootPath ls-files --cached --others --exclude-standard |
        Where-Object { $_ -match '(?i)\.md$' } |
        Sort-Object -Unique
)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to enumerate Markdown sources with Git.'
}

$pdfIgnoredPaths = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase
)
if (Test-Path -LiteralPath $pdfIgnorePath -PathType Leaf) {
    $ignoredPaths = @(
        git -C $repositoryRootPath ls-files --cached --others --ignored --exclude-from=$pdfIgnorePath
    )
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to apply .pdfignore.'
    }
    foreach ($ignoredPath in $ignoredPaths) {
        [void] $pdfIgnoredPaths.Add($ignoredPath.Replace('\', '/'))
    }
}

$generated = 0
$fresh = 0
$pdfIgnored = 0
$noPdf = 0
$marp = 0
$failed = 0
$failures = [Collections.Generic.List[string]]::new()

foreach ($relativePath in $relativeMarkdownPaths) {
    $sourcePath = Join-Path $repositoryRootPath $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        continue
    }

    $normalizedRelativePath = $relativePath.Replace('\', '/')
    $relativeBytes = [Text.Encoding]::UTF8.GetBytes($normalizedRelativePath)
    $cacheKey = ([BitConverter]::ToString(
        [Security.Cryptography.SHA256]::Create().ComputeHash($relativeBytes)
    )).Replace('-', '').ToLowerInvariant()
    $statePath = Join-Path $stateDirectory "$cacheKey.json"

    if ($pdfIgnoredPaths.Contains($normalizedRelativePath)) {
        Remove-ConversionState -Path $statePath
        Write-Output "IGNORE $relativePath (.pdfignore)"
        $pdfIgnored++
        continue
    }
    if (Test-NoPdfDocument -Path $sourcePath) {
        Remove-ConversionState -Path $statePath
        Write-Output "NO-PDF $relativePath"
        $noPdf++
        continue
    }
    if (Test-MarpDocument -Path $sourcePath) {
        Remove-ConversionState -Path $statePath
        Write-Output "MARP  $relativePath"
        $marp++
        continue
    }

    $sourceHash = Get-Sha256 -Path $sourcePath
    $outputPath = [IO.Path]::ChangeExtension($sourcePath, '.pdf')
    $isFresh = $false
    if (-not $Force -and
        (Test-Path -LiteralPath $statePath -PathType Leaf) -and
        (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        try {
            $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
            $isFresh = $state.sourceHash -eq $sourceHash -and
                $state.pipelineHash -eq $pipelineHash -and
                $state.output -eq $normalizedRelativePath.Substring(0, $normalizedRelativePath.Length - 3) + '.pdf'
        } catch {
            $isFresh = $false
        }
    }

    if ($isFresh) {
        Write-Output "FRESH $relativePath"
        $fresh++
        continue
    }

    try {
        $arguments = @(
            $cliDll,
            'render',
            '--input', $sourcePath,
            '--output', $outputPath,
            '--content-root', $repositoryRootPath,
            '--cache-root', $cacheRoot,
            '--pipeline-root', $pipelineRoot
        )
        if ($Force) {
            $arguments += '--force'
        }
        & dotnet @arguments | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "The shared renderer exited with code $LASTEXITCODE."
        }

        $outputRelative = $normalizedRelativePath.Substring(0, $normalizedRelativePath.Length - 3) + '.pdf'
        $state = [ordered]@{
            source = $normalizedRelativePath
            sourceHash = $sourceHash
            pipelineHash = $pipelineHash
            output = $outputRelative
            generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        $temporaryState = "$statePath.tmp"
        $state | ConvertTo-Json | Set-Content -LiteralPath $temporaryState -Encoding UTF8
        Move-Item -LiteralPath $temporaryState -Destination $statePath -Force
        Write-Output "BUILT $relativePath"
        $generated++
    } catch {
        $message = "$relativePath`: $($_.Exception.Message)"
        Write-Output "FAIL  $message"
        $failures.Add($message)
        $failed++
    }
}

Write-Output ''
Write-Output "Markdown PDF summary: built=$generated fresh=$fresh ignored=$pdfIgnored no-pdf=$noPdf marp=$marp failed=$failed"
if ($failed -gt 0) {
    throw "$failed Markdown document(s) failed PDF conversion.`n$($failures -join [Environment]::NewLine)"
}
