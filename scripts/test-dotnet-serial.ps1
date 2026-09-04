param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
Set-Location $Root

$dotnet = Get-Command dotnet -ErrorAction Stop
$projects = @(Get-ChildItem -Path (Join-Path $Root "tests") -Filter "*.Tests.csproj" -Recurse | Sort-Object FullName)
if ($projects.Count -eq 0) {
    throw "No .NET test projects were found under $Root\tests."
}

# Every project runs, even after one fails.
#
# This used to throw on the first failure, which is the wrong shape for a
# gate whose job is to describe the state of the repository. When CI was
# switched back on, five failures in Deluno.Integrations.Tests -- the
# alphabetically first project -- stopped the run there, and nothing could say
# whether the other six projects were healthy or hiding the same defect. One
# fix, one push, one wait, to find out. Serially is still one project at a
# time, because SQLite and file locks do not want two.
Write-Host "Running $($projects.Count) .NET test projects serially."
$failed = [System.Collections.Generic.List[string]]::new()
foreach ($project in $projects) {
    Write-Host ""
    Write-Host "=== $($project.Name) ==="
    & $dotnet.Source test $project.FullName --configuration Release --no-build --no-restore --logger "trx;LogFileName=backend-tests.trx"
    if ($LASTEXITCODE -ne 0) {
        $failed.Add($project.Name)
    }
}

Write-Host ""
if ($failed.Count -gt 0) {
    throw "Tests failed in $($failed.Count) of $($projects.Count) projects: $($failed -join ', ')."
}

Write-Host "All .NET test projects passed serially."
