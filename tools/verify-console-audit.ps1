# Current executable audit. Runs only local virtual clients and preserves existing processes.
# A failed requirement remains failed in result.json; exit 0 means collection completed.
param([Parameter(Mandatory)][string]$Docker)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root ('evidence/COM-01/S018-audit/console-' + (Get-Date -Format yyyyMMddTHHmmss))
New-Item -ItemType Directory -Path $run | Out-Null
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$result = [ordered]@{ executed_at=(Get-Date -Format o); collection='failed'; checks=@(); environment='Current built Console and TestClient; real local Mosquitto; console Core/log doubles' }
function Read-Log([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $reader=[IO.StreamReader]::new($stream)
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
function Wait-Log([string]$path,[string]$pattern) {
    $limit=(Get-Date).AddSeconds(8)
    do {
        if ((Read-Log $path) -match $pattern) { return }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $limit)
    throw "No expected output: $pattern in $path"
}
function Start-AuditProcess([string]$name,[string]$dll,[string[]]$arguments) {
    $absolute = Join-Path $root $dll
    $process=Start-Process dotnet -WindowStyle Hidden -PassThru -ArgumentList (@(('"'+$absolute+'"'))+$arguments) -WorkingDirectory $root -RedirectStandardOutput (Join-Path $run "$name.log") -RedirectStandardError (Join-Path $run "$name.err")
    $processes.Add($process)
    return $process
}
function Record([string]$name,[bool]$success,[string]$expected,$actual) {
    $script:result.checks += [ordered]@{name=$name;result=$(if($success){'passed'}else{'failed'});expected=$expected;actual=$actual}
}
try {
    $result.docker = & $Docker version --format '{{.Server.Version}}'
    $result.broker_image = & $Docker inspect amr-mosquitto --format '{{.Config.Image}}'
    if ($LASTEXITCODE -ne 0) { throw 'Expected local development Broker missing' }
    $console=Start-AuditProcess 'console' 'src/Amr.Communication.Console/bin/Debug/net10.0/Amr.Communication.Console.dll' @('--heartbeat','--duration-seconds','18')
    Wait-Log (Join-Path $run 'console.log') 'Subscribed:'
    $amr1=Start-AuditProcess 'amr1' 'src/Amr.TestClient/bin/Debug/net10.0/Amr.TestClient.dll' @('AMR_001')
    $amr2=Start-AuditProcess 'amr2' 'src/Amr.TestClient/bin/Debug/net10.0/Amr.TestClient.dll' @('AMR_002')
    $badId='AUDIT_BAD_'+[guid]::NewGuid().ToString('N').Substring(0,8)
    $badAmr=Start-AuditProcess 'invalid-command-amr' 'src/Amr.TestClient/bin/Debug/net10.0/Amr.TestClient.dll' @($badId)
    Wait-Log (Join-Path $run 'console.log') 'Received Heartbeat: AMR_002'
    Wait-Log (Join-Path $run 'invalid-command-amr.log') 'Subscribed to commands:'
    '{' | & $Docker exec -i amr-mosquitto mosquitto_pub -h 127.0.0.1 -t "amr/$badId/cmd" -s
    if ($LASTEXITCODE -ne 0) { throw 'Malformed fixture publish failed' }
    Wait-Log (Join-Path $run 'invalid-command-amr.log') 'Received Command:'
    Wait-Log (Join-Path $run 'console.log') 'Received Ack: AMR_001'
    # Stop only our AMR_002 to inspect the lack of per-AMR timeout handling.
    Stop-Process -Id $amr2.Id
    $amr2.WaitForExit()
    $result.amr2_stopped_at = Get-Date -Format o
    if (-not $console.WaitForExit(22000)) { throw 'Console timed out' }
    $console.Refresh()
    $consoleLog=Read-Log (Join-Path $run 'console.log')
    $amrLog=Read-Log (Join-Path $run 'amr1.log')
    $badLog=Read-Log (Join-Path $run 'invalid-command-amr.log')
    $commands=@($consoleLog -split '\r?\n' | Where-Object { $_ -like 'Published Command*' } | ForEach-Object { ($_ -replace '^[^{]+','') | ConvertFrom-Json })
    $acks=@($amrLog -split '\r?\n' | Where-Object { $_ -like 'Published ACK:*' } | ForEach-Object { ($_ -replace '^[^{]+','') | ConvertFrom-Json })
    $badAcks=@($badLog -split '\r?\n' | Where-Object { $_ -like 'Published ACK:*' } | ForEach-Object { ($_ -replace '^[^{]+','') | ConvertFrom-Json })
    Record 'real_console_virtual_amr_roundtrip' ($commands.Count -ge 2 -and $acks.Count -ge 2 -and $consoleLog.Contains('Received Ack: AMR_001') -and $console.ExitCode -eq 0) 'Command -> virtual AMR -> ACK -> Console; normal console exit' @{command_count=$commands.Count;ack_count=$acks.Count;console_exit=$console.ExitCode;command_ids=@($commands.cmdId);ack_ids=@($acks.cmdId)}
    Record 'periodic_heartbeat_two_amrs' (($consoleLog -match 'Received Heartbeat: AMR_001') -and ($consoleLog -match 'Received Heartbeat: AMR_002') -and @($amrLog -split '\r?\n' | Where-Object { $_ -like 'Published: amr/AMR_001/heartbeat*' }).Count -ge 3) 'Two virtual AMRs produce heartbeats' 'See console.log, amr1.log and amr2.log'
    Record 'unique_command_ids' ($commands.Count -ge 2 -and @($commands.cmdId | Select-Object -Unique).Count -eq $commands.Count) 'Each request should have a different cmdId' @($commands.cmdId)
    Record 'malformed_command_does_not_succeed' ($badAcks.Count -eq 0) 'Malformed JSON must not produce fabricated SUCCESS ACK' @($badAcks)
    Record 'timeout_notification_observation' ($consoleLog -match 'HeartbeatTimeout|Heartbeat Timeout') 'Per-AMR heartbeat interruption notification (COM-02 unimplemented; no policy adopted)' @{notification_found=($consoleLog -match 'HeartbeatTimeout|Heartbeat Timeout'); note='Observation only; source HeartbeatMonitor is empty; no timeout threshold is implemented'}
    $result.collection='completed'
} catch { $result.error=$_.Exception.ToString(); throw }
finally {
    foreach($p in $processes) { if(-not $p.HasExited){ Stop-Process -Id $p.Id }; $p.Dispose() }
    $result | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Evidence: $run"
}
