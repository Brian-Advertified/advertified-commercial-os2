param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$before = (Get-PSDrive C).Free
Write-Host ("Host storage before generated-artifact reclaim: {0:N2} GiB free." -f ($before / 1GB))

$targets = @(
    (Join-Path $repoRoot 'web/dist'),
    (Join-Path $repoRoot 'web/test-results')
)
$targets += Get-ChildItem -Path (Join-Path $repoRoot 'api') -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Select-Object -ExpandProperty FullName

$uniqueTargets = @($targets | Sort-Object -Unique)
foreach ($target in $uniqueTargets) {
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $resolved = (Resolve-Path -LiteralPath $target).Path
    if (-not $resolved.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove generated path outside repository: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

$after = (Get-PSDrive C).Free
Write-Host ("Host storage after generated-artifact reclaim: {0:N2} GiB free; reclaimed {1:N2} GiB." -f
    ($after / 1GB), (($after - $before) / 1GB))
