# Verifies the four S020 command basics against an existing local development Mosquitto.
# Stops only processes created by this script. Any failed requirement returns nonzero.
param([Parameter(Mandatory)][string]$Docker, [string]$EvidenceRoot='evidence/COM-01/S020-command-basics')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$run=Join-Path (Join-Path $root $EvidenceRoot) ('broker-'+(Get-Date -Format yyyyMMddTHHmmss))
New-Item -ItemType Directory -Path $run | Out-Null
$amrId='S020_'+[guid]::NewGuid().ToString('N').Substring(0,12)
$processes=[Collections.Generic.List[Diagnostics.Process]]::new()
$result=[ordered]@{executed_at=(Get-Date -Format o);result='failed';amr_id=$amrId;checks=@();environment='Current built Console/TestClient, real localhost:1883 Mosquitto, Console Core/log doubles. Movement deliberately unimplemented.'}
function Read-Log([string]$name) {
 $path=Join-Path $run "$name.log"
 if(-not(Test-Path -LiteralPath $path)){return ''}
 $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
 $reader=[IO.StreamReader]::new($stream)
 try{$reader.ReadToEnd()}finally{$reader.Dispose()}
}
function Wait-Log([string]$name,[string]$pattern) {
 $deadline=(Get-Date).AddSeconds(8)
 do {if((Read-Log $name) -match $pattern){return}; Start-Sleep -Milliseconds 80}while((Get-Date) -lt $deadline)
 throw "Missing output $pattern in $name.log"
}
function Start-Client([string]$name,[string]$relative,[string[]]$arguments) {
 $dll=Join-Path $root $relative
 $p=Start-Process dotnet -WindowStyle Hidden -PassThru -ArgumentList (@(('"'+$dll+'"'))+$arguments) -WorkingDirectory $root -RedirectStandardOutput (Join-Path $run "$name.log") -RedirectStandardError (Join-Path $run "$name.err")
 $processes.Add($p)
 return $p
}
function Check([string]$name,[bool]$passed,[string]$expected,$actual) {
 $result.checks += [ordered]@{name=$name;executed_at=(Get-Date -Format o);result=$(if($passed){'passed'}else{'failed'});expected=$expected;actual=$actual}
}
function Send-Fixture([string]$payload,[string]$kind='command') {
 $payload | & $Docker exec -i amr-mosquitto mosquitto_pub -h 127.0.0.1 -t "amr/$amrId/$kind" -s
 if($LASTEXITCODE -ne 0){throw 'Fixture publish failed'}
}
try {
 $result.broker_image=& $Docker inspect amr-mosquitto --format '{{.Config.Image}}'
 if($LASTEXITCODE -ne 0){throw 'Local development Broker not running'}
 $result.docker=& $Docker version --format '{{.Server.Version}}'
 $result.code_commit=git -C $root rev-parse HEAD
 $consoleDll='src/Amr.Communication.Console/bin/Debug/net10.0/Amr.Communication.Console.dll'
 $observer=Start-Client 'observer' $consoleDll @('--heartbeat','--duration-seconds','30')
 Wait-Log observer 'Subscribed:'
 $amr=Start-Client 'amr' 'src/Amr.TestClient/bin/Debug/net10.0/Amr.TestClient.dll' @($amrId)
 Wait-Log amr "Subscribed to commands: amr/$amrId/command"
 Wait-Log observer "Received Heartbeat: $amrId"
 $commands=@()
 foreach($type in @('MOVE','STOP','RESET')) {
  $arguments=@('--command',$type,'--amr-id',$amrId,'--duration-seconds','2')
  if($type -eq 'MOVE'){$arguments+=@('--x','30','--y','15')}
  $p=Start-Client $type $consoleDll $arguments
  if(-not $p.WaitForExit(8000)){throw "Console $type timed out"}
  $p.Refresh()
  $log=Read-Log $type
  $request=@($log -split '\r?\n'|Where-Object{$_ -like 'Published Command*'}|ForEach-Object{($_ -replace '^[^{]+','')|ConvertFrom-Json})
  $ack=@($log -split '\r?\n'|Where-Object{$_ -like "Received Ack: $amrId *"}|ForEach-Object{($_ -replace '^[^{]+','')|ConvertFrom-Json})
  $commands+=$request
  $matches=@($ack|Where-Object{$request.Count -eq 1 -and $_.cmdId -eq $request[0].cmdId -and $_.result -eq 'FAILURE' -and $_.reason -eq 'SIMULATION_NOT_IMPLEMENTED'})
  Check "roundtrip_$type" ($p.ExitCode -eq 0 -and $request.Count -eq 1 -and $matches.Count -eq 1) 'Exactly one request and same-ID FAILURE with truthful reason; normal console exit' @{exit=$p.ExitCode;request=$request;ack=$ack}
 }
 Check 'unique_request_ids' ($commands.Count -eq 3 -and @($commands.cmdId|Select-Object -Unique).Count -eq 3) 'Each console request has a different cmdId' @($commands.cmdId)
 Send-Fixture '{'
 Send-Fixture '{"type":"MOVE","ts":1,"param":{"x":1,"y":2}}'
 Send-Fixture '{"cmdId":"A","cmdId":"B","type":"MOVE","ts":1,"param":{"x":1,"y":2}}'
 Send-Fixture '{"cmdId":"BAD-TYPE","type":"FLY","ts":1,"param":{}}'
 Send-Fixture '{"cmdId":"BAD-PARAM","type":"MOVE","ts":1,"param":{"x":1}}'
 Wait-Log observer 'BAD-PARAM'
 $acks=@((Read-Log amr) -split '\r?\n'|Where-Object{$_ -like 'Published ACK:*'}|ForEach-Object{($_ -replace '^[^{]+','')|ConvertFrom-Json})
 Check 'invalid_commands_no_fabricated_success' ($acks.Count -eq 5 -and @($acks|Where-Object result -ne 'FAILURE').Count -eq 0 -and @($acks|Where-Object cmdId -eq 'BAD-TYPE').Count -eq 1 -and @($acks|Where-Object cmdId -eq 'BAD-PARAM').Count -eq 1) 'Malformed/missing/duplicate IDs produce no ACK; known IDs rejected with FAILURE and reason' $acks
 Check 'heartbeat_mode_does_not_send_commands' (-not (Read-Log observer).Contains('Published Command')) 'Heartbeat receiver does not issue automatic MOVE requests (observed beyond 5 seconds)' 'See observer.log'
 Check 'pdf_topic_and_payload' ((Read-Log amr).Contains("Received Command: amr/$amrId/command") -and @($commands|Where-Object{ -not $_.ts -or $null -eq $_.param }).Count -eq 0) 'Commands arrive on /command with ts/param present' $commands
 Send-Fixture '{"cmdId":"ACK-SUCCESS-FIXTURE","ts":1,"result":"SUCCESS"}' 'ack'
 Wait-Log observer 'ACK-SUCCESS-FIXTURE'
 Check 'success_ack_preserved' ((Read-Log observer) -match 'ACK-SUCCESS-FIXTURE.*SUCCESS') 'A synthetic SUCCESS ACK remains SUCCESS (not a claim of simulated movement)' 'See observer.log'
 Send-Fixture ('{"id":"'+$amrId+'","ts":1}') 'heartbeat'
 Wait-Log observer 'MissingField'
 Wait-Log observer "Received Heartbeat: $amrId"
 if(-not $observer.WaitForExit(32000)){throw 'Observer did not exit normally'}
 $observer.Refresh()
 Check 'normal_observer_exit' ($observer.ExitCode -eq 0) 'Timed receiver exits normally' $observer.ExitCode
 if(@($result.checks|Where-Object result -ne 'passed').Count -gt 0){throw 'Command baseline checks failed; see result.json'}
 $result.result='passed'
} catch {$result.error=$_.Exception.Message; throw}
finally {
 foreach($p in $processes){if(-not $p.HasExited){Stop-Process -Id $p.Id};$p.Dispose()}
 $result.finished_at=Get-Date -Format o
 $result|ConvertTo-Json -Depth 15|Set-Content (Join-Path $run 'result.json') -Encoding utf8
 Write-Output "Evidence: $run"
}
