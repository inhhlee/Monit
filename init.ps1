#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('check', 'verify', 'start', 'broker', 'verify-broker')]
    [string]$Mode = 'verify',
    [switch]$Start,
    [string]$Docker = $env:AMR_DOCKER,
    [ValidateRange(1, 86400)][int]$DurationSeconds
    [ValidateRange(1, 86400)][int]$DurationSeconds,
    [switch]$Heartbeat
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8
$root = $PSScriptRoot
$runId = (Get-Date -Format 'yyyyMMddTHHmmssfffK').Replace(':', '') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$logDir = Join-Path $root "evidence/bootstrap/$runId"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$summary = Join-Path $logDir 'summary.log'
$stage = 'prepare'
$verifier = Join-Path $root 'tools/Amr.Verification/bin/Debug/net10.0/Amr.Verification.dll'

function Record([string]$Message) {
    $Message | Tee-Object -FilePath $summary -Append -Encoding utf8 | Write-Host
}
function Run-Step([string]$Name, [string]$Executable, [string[]]$Arguments) {
    $script:stage = $Name
    Record "BEGIN $Name : $Executable $($Arguments -join ' ')"
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $logDir "$Name.log") -Encoding utf8 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
    Record "PASS $Name"
}
function Require-Command([string]$Name, [string]$HelpText) {
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $command) { throw "$Name is not available. $HelpText" }
    return $command.Source
}
function Resolve-Docker {
    if (-not $script:Docker) { $script:Docker = 'docker' }
    $script:Docker = Require-Command $script:Docker 'Install Docker Desktop, restart this terminal, or set AMR_DOCKER to docker.exe.'
    Run-Step 'docker-engine' $script:Docker @('version', '--format', '{{.Server.Version}}')
}
function Start-Console {
function Start-Console {
    $arguments = @('run', '--project', 'src/Amr.Communication.Console/Amr.Communication.Console.csproj', '--')
    if ($Heartbeat) { $arguments += '--heartbeat' }
    if ($DurationSeconds) { $arguments += @('--duration-seconds', "$DurationSeconds") }
    Run-Step 'start' 'dotnet' $arguments
}
Push-Location $root
try {
    Record "Started: $(Get-Date -Format o)"
    Record "Root: $root; PowerShell: $($PSVersionTable.PSVersion); Mode: $Mode"
    if (-not $IsWindows) { throw 'This baseline includes WPF and requires Windows. See README.md.' }
    if ($Start -and $Mode -ne 'verify') { throw 'Legacy -Start is only supported with verify. Use start for direct execution.' }
    $null = Require-Command 'dotnet' 'Install the .NET SDK version specified in global.json.'
    $null = Require-Command 'git' 'Install Git for Windows and restart this terminal.'
    Run-Step 'dotnet-sdk' 'dotnet' @('--version')
    Run-Step 'git-version' 'git' @('--version')
    # Handles both normal checkouts and worktrees. Source ZIPs are also usable.
    if (Test-Path '.git') {
        Run-Step 'git-state' 'git' @('status', '--short')
        $head = & git rev-parse --verify HEAD 2>$null
        if ($LASTEXITCODE -eq 0) { Record "Code version: $head" }
        else { Record 'Code version: initial uncommitted scaffold' }
    } else { Record 'Code version: source archive (no Git metadata)' }

    switch ($Mode) {
        'check' { Record 'PREREQUISITES PASSED. Build/tests and Broker have not been checked.' }
        'broker' {
            Resolve-Docker
            Run-Step 'docker-compose' $Docker @('compose', 'version')
            Run-Step 'broker-start' $Docker @('compose', '-f', 'docker/docker-compose.yml', 'up', '-d', 'mosquitto')
            Record 'Broker container started. Use init.cmd start to check the MQTT connection.'
        }
        'start' { Start-Console }
        default {
            # Bound worker creation and avoid leaving reusable MSBuild nodes after verification.
            Run-Step 'restore' 'dotnet' @('restore', 'AmrControlSystem.slnx', '--disable-parallel', '-m:1', '-nr:false')
            Run-Step 'build' 'dotnet' @('build', 'AmrControlSystem.slnx', '--no-restore', '-m:1', '-nr:false')
            Run-Step 'harness' 'dotnet' @($verifier, 'tracker', (Join-Path $root 'feature_list.json'))
            $testProjects = @(Get-ChildItem (Join-Path $root 'tests') -Recurse -Filter '*.csproj' | Where-Object { $_.FullName -notmatch '[/\\](bin|obj)[/\\]' } | Sort-Object FullName)
            if ($testProjects.Count -eq 0) { throw 'No automated .NET test project found.' }
            foreach ($project in $testProjects) {
                $name = $project.BaseName
                Run-Step "test-$name" 'dotnet' @('test', $project.FullName, '--no-build', '--no-restore', '--logger', "trx;LogFileName=$name.trx", '--results-directory', $logDir)
                Run-Step "test-count-$name" 'dotnet' @($verifier, 'trx', (Join-Path $logDir "$name.trx"))
            }
            Record 'BASELINE PASSED. Actual Broker verification remains separate.'
            if ($Mode -eq 'verify-broker') {
                Resolve-Docker
                Run-Step 'broker-image' $Docker @('pull', 'eclipse-mosquitto:2.1.2-alpine')
                Run-Step 'broker-verification' 'dotnet' @($verifier, 'broker', $root, $Docker)
            }
            if ($Start) { Start-Console }
        }
    }
    Record "Finished: $(Get-Date -Format o)"
    Record "Evidence: $logDir"
}
catch {
    Record "FAILED stage=$stage : $($_.Exception.Message)"
    exit 1
}
finally { Pop-Location }
