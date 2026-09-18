#Requires -Version 7.0
[CmdletBinding()]
param(
    [switch]$Start,
    [string]$Python = $env:AMR_PYTHON
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$runId = (Get-Date -Format 'yyyyMMddTHHmmssfffK').Replace(':', '') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$logDir = Join-Path $root "evidence/bootstrap/$runId"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$summary = Join-Path $logDir 'summary.log'
$stage = 'prepare'

function Record([string]$Message) {
    $Message | Tee-Object -FilePath $summary -Append | Write-Host
}
function Run-Step([string]$Name, [string]$Executable, [string[]]$Arguments) {
    $script:stage = $Name
    Record "BEGIN $Name : $Executable $($Arguments -join ' ')"
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $logDir "$Name.log") | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
    Record "PASS $Name"
}

Push-Location $root
try {
    Record "Started: $(Get-Date -Format o)"
    Record "Root: $root; PowerShell: $($PSVersionTable.PSVersion)"
    Record 'Scope: scaffold build/tests only; no MQTT feature is certified.'
    if (-not $Python) {
        $pythonCommand = Get-Command python.exe -All -ErrorAction SilentlyContinue |
            Where-Object { $_.Source -notmatch '[/\\]WindowsApps[/\\]' } |
            Select-Object -First 1
        if ($pythonCommand) { $Python = $pythonCommand.Source }
        else {
            # Optional bundled runtime on this workstation; portable override: -Python.
            $Python = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
        }
    }
    Run-Step 'python-version' $Python @('--version')
    Run-Step 'dotnet-info' 'dotnet' @('--info')
    if (Test-Path '.git') {
        Run-Step 'git-state' 'git' @('status', '--short')
        # No HEAD is valid before the first initialization commit.
        $head = & git rev-parse --verify HEAD 2>$null
        if ($LASTEXITCODE -eq 0) { Record "Code version: $head" }
        else { Record 'Code version: initial uncommitted scaffold' }
    }
    Run-Step 'harness' $Python @('scripts/verify_harness.py')
    Run-Step 'harness-tests' $Python @('-m', 'unittest', 'discover', '-s', 'tests/harness', '-v')
    Run-Step 'restore' 'dotnet' @('restore', 'AmrControlSystem.slnx')
    Run-Step 'build' 'dotnet' @('build', 'AmrControlSystem.slnx', '--no-restore')
    $testProjects = @(Get-ChildItem (Join-Path $root 'tests') -Recurse -Filter '*.csproj')
    if ($testProjects.Count -eq 0) { throw 'No automated .NET test project found.' }
    foreach ($project in $testProjects) {
        $name = $project.BaseName
        Run-Step "test-$name" 'dotnet' @('test', $project.FullName, '--no-build', '--no-restore', '--logger', "trx;LogFileName=$name.trx", '--results-directory', $logDir)
        Run-Step "test-count-$name" $Python @('scripts/verify_test_results.py', (Join-Path $logDir "$name.trx"))
    }
    Record 'BASELINE PASSED. Product COM-01..05 verification remains separate.'
    if ($Start) {
        Run-Step 'start' 'dotnet' @('run', '--project', 'src/Amr.Communication.Console/Amr.Communication.Console.csproj', '--no-build', '--no-restore')
    }
    Record "Finished: $(Get-Date -Format o)"
    Record "Evidence: $logDir"
}
catch {
    Record "FAILED stage=$stage : $($_.Exception.Message)"
    exit 1
}
finally { Pop-Location }
