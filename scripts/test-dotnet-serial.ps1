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
    # DIAGNOSTIC, temporary. --blame-hang is back on with a short fuse, and the
    # Sequence file it writes is printed below.
    #
    # Blame writes a Sequence file only when tests were still in flight when the
    # timer fired, and says "All tests finished running, Sequence file will not
    # be generated" otherwise. Deluno.Persistence.Tests got a Sequence file — so
    # something WAS running, and the file names it. That name is the whole
    # question, and it is a few hundred bytes, so it goes in the log rather than
    # an artifact nobody downloads.
    $sequenceRoot = Join-Path (Split-Path $project.FullName -Parent) "TestResults"
    if (Test-Path $sequenceRoot) { Remove-Item $sequenceRoot -Recurse -Force -ErrorAction SilentlyContinue }

    & $dotnet.Source test $project.FullName --configuration Release --no-build --no-restore `
        --logger "trx;LogFileName=backend-tests.trx" `
        --blame-hang --blame-hang-timeout 3m --blame-hang-dump-type mini

    if (Test-Path $sequenceRoot) {
        foreach ($sequence in Get-ChildItem -Path $sequenceRoot -Filter "Sequence_*.xml" -Recurse -ErrorAction SilentlyContinue) {
            Write-Host "--- blame sequence: $($sequence.Name) ---"
            Get-Content $sequence.FullName | ForEach-Object { Write-Host "    $_" }
        }
    }
    if ($LASTEXITCODE -ne 0) {
        $failed.Add($project.Name)
    }
}

Write-Host ""
if ($failed.Count -gt 0) {
    throw "Tests failed in $($failed.Count) of $($projects.Count) projects: $($failed -join ', ')."
}

Write-Host "All .NET test projects passed serially."
