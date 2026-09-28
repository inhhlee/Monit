# Regression of the existing S015 path; does not implement all of COM-01.
# Requires the developer's local Mosquitto on localhost:1883, no restart/cleanup of that service.
param([string]$BrokerContainer = 'amr-mosquitto')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$run = Join-Path $root ('evidence/COM-04/S016-pdf-contract/broker-' + (Get-Date -Format 'yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
$id = 'S016_' + [Guid]::NewGuid().ToString('N')
$topic = "amr/$id/heartbeat"
$consumer = $null
$producer = $null
$result = [ordered]@{ result='failed'; executed_at=(Get-Date -Format o); amr_id=$id;
    broker='localhost:1883'; test_doubles='Console Core/log sink; Amr.TestClient virtual publisher';
    limitations='Heartbeat only. Publisher stopped by test process; no actual Core/GUI or AMR movement.' }

function Wait-Log([string]$path, [string]$pattern, [int]$seconds = 10) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        if (Test-Path $path) {
            $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            $reader = [IO.StreamReader]::new($stream)
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($content -match $pattern) { return }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Expected output not observed: $pattern"
}
function Start-Consumer([string]$name, [int]$seconds) {
    $dll = Join-Path $root 'src/Amr.Communication.Console/bin/Debug/net10.0/Amr.Communication.Console.dll'
    Start-Process -FilePath (Get-Command dotnet).Source -WindowStyle Hidden -PassThru `
        -ArgumentList @(('"' + $dll + '"'), '--heartbeat', '--duration-seconds', $seconds) `
        -RedirectStandardOutput (Join-Path $run "$name.log") -RedirectStandardError (Join-Path $run "$name.err")
}
function Publish([string]$payload) {
    $payload | docker exec -i $BrokerContainer mosquitto_pub -h 127.0.0.1 -p 1883 -V mqttv5 -q 0 -t $topic -s
    if ($LASTEXITCODE -ne 0) { throw 'Test publish failed' }
    Add-Content -LiteralPath (Join-Path $run 'published.jsonl') -Value $payload -Encoding utf8
}
try {
    $version = docker version --format '{{.Server.Version}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker unavailable' }
    $result.docker = $version
    $result.image = docker inspect $BrokerContainer --format '{{.Config.Image}}'
    if ($LASTEXITCODE -ne 0) { throw 'Local broker container unavailable' }
    $consumer = Start-Consumer 'consumer' 22
    $log = Join-Path $run 'consumer.log'
    Wait-Log $log 'Subscribed: amr/\+/heartbeat'
    $dll = Join-Path $root 'src/Amr.TestClient/bin/Debug/net10.0/Amr.TestClient.dll'
    $producer = Start-Process -FilePath (Get-Command dotnet).Source -WindowStyle Hidden -PassThru `
        -ArgumentList @(('"' + $dll + '"'), $id) `
        -RedirectStandardOutput (Join-Path $run 'producer.log') -RedirectStandardError (Join-Path $run 'producer.err')
    Wait-Log $log "Received Heartbeat: $id ts="
    Wait-Log (Join-Path $run 'producer.log') '"status":"OK"'
    Stop-Process -Id $producer.Id
    $producer.WaitForExit()

    Publish '{'
    Wait-Log $log ('"Topic":"' + $topic + '","Code":"InvalidJson"')
    Publish (@{id=$id;ts=2208988800111} | ConvertTo-Json -Compress)
    Wait-Log $log ('"Topic":"' + $topic + '","Code":"MissingField","Field":"\$\.status"')
    Publish (@{id=$id;ts=2208988800222;status='BAD'} | ConvertTo-Json -Compress)
    Wait-Log $log ('"Topic":"' + $topic + '","Code":"OutOfRange","Field":"\$\.status"')
    Publish (@{id='WRONG';ts=2208988800333;status='OK'} | ConvertTo-Json -Compress)
    Wait-Log $log ('"Topic":"' + $topic + '","Code":"IdMismatch"')
    Publish (@{id=$id;ts=2208988800999;status='OK'} | ConvertTo-Json -Compress)
    Wait-Log $log "Received Heartbeat: $id ts=2208988800999"
    if (-not $consumer.WaitForExit(30000)) { throw 'Consumer did not terminate' }
    if ($consumer.ExitCode -ne 0) { throw "Consumer exit code: $($consumer.ExitCode)" }
    $lines = Get-Content $log
    $errors = @($lines | Where-Object { $_.StartsWith('{') } | ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object { $_.Event -eq 'Rejected' -and $_.Topic -eq $topic })
    if ($errors.Count -ne 4) { throw "Expected four rejections, got $($errors.Count)" }
    if (($lines -join "`n") -match "Received Heartbeat: $id ts=2208988800(111|222|333)") {
        throw 'Invalid payload reached the Core sink'
    }
    $result.rejection_codes = @($errors | ForEach-Object Code)
    $result.producer_with_status_received = $true
    $result.valid_after_rejection_received = $true

    $consumer.Dispose()
    $consumer = Start-Consumer 'restart' 3
    $log = Join-Path $run 'restart.log'
    Wait-Log $log 'Subscribed: amr/\+/heartbeat'
    Publish (@{id=$id;ts=2208988800888;status='OK'} | ConvertTo-Json -Compress)
    Wait-Log $log "Received Heartbeat: $id ts=2208988800888"
    if (-not $consumer.WaitForExit(8000) -or $consumer.ExitCode -ne 0) { throw 'Restart failed' }
    $result.restart_received = $true
    $result.result = 'passed'
} catch {
    $result.error = $_.Exception.Message
    throw
} finally {
    foreach ($process in @($producer, $consumer)) {
        if ($null -ne $process) {
            if (-not $process.HasExited) { Stop-Process -Id $process.Id }
            $process.Dispose()
        }
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $run 'result.json')
    Write-Output "Evidence: $run"
}
