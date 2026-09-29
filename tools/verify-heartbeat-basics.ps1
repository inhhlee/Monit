# Runs the actual Console and two TestClients against an existing localhost Mosquitto.
# Stops only child processes created here. A process stop represents loss of one AMR.
param([string]$EvidenceRoot = ('evidence/COM-02/console-' + (Get-Date -Format yyyyMMddTHHmmssfff)))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root $EvidenceRoot
if (Test-Path -LiteralPath $run) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $run | Out-Null
$prefix = 'HB_CONSOLE_' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$amrA = $prefix + '_A'
$amrB = $prefix + '_B'
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$result = [ordered]@{
    executed_at = (Get-Date -Format o); result = 'failed'; checks = @()
    environment = 'Actual Console/TestClient processes and localhost:1883 Mosquitto; Console Core/log doubles; default 5s timeout/0.5s check, 1s publish. A is stopped by this script, not keyboard Ctrl+C.'
    amr_a = $amrA; amr_b = $amrB
}
function Read-Log([string]$name) {
    $path = Join-Path $run "$name.log"
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $reader = [IO.StreamReader]::new($stream)
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
function Events {
    @((Read-Log observer) -split '\r?\n' | Where-Object { $_ -like '{*' } | ForEach-Object { $_ | ConvertFrom-Json })
}
function Wait-Until([scriptblock]$Condition, [int]$TimeoutMilliseconds = 10000) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not (& $Condition)) {
        if ($watch.ElapsedMilliseconds -ge $TimeoutMilliseconds) { throw 'Expected console observation did not arrive.' }
        Start-Sleep -Milliseconds 80
    }
}
function Start-Client([string]$name, [string]$relative, [string[]]$arguments) {
    $dll = Join-Path $root $relative
    $process = Start-Process dotnet -WindowStyle Hidden -PassThru -WorkingDirectory $root `
        -ArgumentList (@(('"' + $dll + '"')) + $arguments) `
        -RedirectStandardOutput (Join-Path $run "$name.log") -RedirectStandardError (Join-Path $run "$name.err")
    $processes.Add($process)
    return $process
}
function Check([string]$name, [bool]$passed, [string]$expected, $actual) {
    $result.checks += [ordered]@{ name = $name; executed_at = (Get-Date -Format o); result = $(if ($passed) { 'passed' } else { 'failed' }); expected = $expected; actual = $actual }
    Write-Output "$(if ($passed) { 'PASS' } else { 'FAIL' }) $name"
}
try {
    $consoleDll = 'src/Amr.Communication.Console/bin/Debug/net10.0/Amr.Communication.Console.dll'
    $clientDll = 'src/Amr.TestClient/bin/Debug/net10.0/Amr.TestClient.dll'
    $observer = Start-Client observer $consoleDll @('--heartbeat', '--duration-seconds', '22')
    Wait-Until { (Read-Log observer).Contains('Subscribed:') }
    $a = Start-Client amr-a $clientDll @($amrA)
    $b = Start-Client amr-b $clientDll @($amrB)
    Wait-Until { (Read-Log observer).Contains("Received Heartbeat: $amrA") -and (Read-Log observer).Contains("Received Heartbeat: $amrB") }
    Start-Sleep -Seconds 2
    $started = @((Events) | Where-Object { $_.Event -eq 'HeartbeatMonitoringStarted' -and $_.AmrId -in @($amrA, $amrB) })
    $configuration = @((Events) | Where-Object Event -eq HeartbeatConfiguration)
    Check 'default_configuration_and_two_amrs' ($started.Count -eq 2 -and $configuration.Count -eq 1 -and $configuration[0].TimeoutSeconds -eq 5 -and $configuration[0].CheckIntervalSeconds -eq 0.5) 'Two AMRs start monitoring with default5s/0.5s' @{ started = $started; configuration = $configuration }
    Stop-Process -Id $a.Id
    $a.WaitForExit()
    $bBefore = @((Read-Log observer) -split '\r?\n' | Where-Object { $_ -like "Received Heartbeat: $amrB *" }).Count
    Wait-Until { @((Events) | Where-Object { $_.Event -eq 'HeartbeatTimeout' -and $_.AmrId -eq $amrA }).Count -eq 1 }
    Start-Sleep -Milliseconds 900
    $timeouts = @((Events) | Where-Object { $_.Event -eq 'HeartbeatTimeout' -and $_.AmrId -in @($amrA, $amrB) })
    $bAfter = @((Read-Log observer) -split '\r?\n' | Where-Object { $_ -like "Received Heartbeat: $amrB *" }).Count
    Check 'only_stopped_a_times_out_once' ($timeouts.Count -eq 1 -and $timeouts[0].AmrId -eq $amrA -and $bAfter -gt $bBefore) 'One timeout for A; B continues receiving' @{ timeouts = $timeouts; bBefore = $bBefore; bAfter = $bAfter }
    $timeout = $timeouts | Where-Object AmrId -eq $amrA | Select-Object -First 1
    Check 'default_timeout_timing' ($null -ne $timeout -and $timeout.ElapsedMilliseconds -ge 5000 -and $timeout.ElapsedMilliseconds -le 6000) '5s timeout +0.5s poll +0.5s OS scheduling tolerance, measured from last receipt' $timeout
    $null = Start-Client amr-a-restarted $clientDll @($amrA)
    Wait-Until { @((Events) | Where-Object { $_.Event -eq 'HeartbeatRecovered' -and $_.AmrId -eq $amrA }).Count -eq 1 }
    Start-Sleep -Seconds 2
    $recovered = @((Events) | Where-Object { $_.Event -eq 'HeartbeatRecovered' -and $_.AmrId -eq $amrA })
    Check 'same_id_restart_recovers_once' ($recovered.Count -eq 1) 'Restarting A with same ID yields exactly one recovery' $recovered
    if (-not $observer.WaitForExit(25000)) { throw 'Console did not finish its timed run.' }
    $observer.Refresh()
    $allTimeouts = @((Events) | Where-Object { $_.Event -eq 'HeartbeatTimeout' -and $_.AmrId -in @($amrA, $amrB) })
    Check 'normal_exit_without_additional_timeout' ($observer.ExitCode -eq 0 -and $allTimeouts.Count -eq 1) 'Receiver exits normally after resumed healthy traffic, no additional timeout' @{ exit = $observer.ExitCode; timeouts = $allTimeouts }
    if (@($result.checks | Where-Object result -ne passed).Count -gt 0) { throw 'Console heartbeat checks failed.' }
    $result.result = 'passed'
} catch {
    $result.error = $_.Exception.Message
    throw
} finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id }
        $process.Dispose()
    }
    $result.finished_at = Get-Date -Format o
    $result | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Evidence: $run"
}
