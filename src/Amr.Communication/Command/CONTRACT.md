# Command 계약 v1 (S020 / DN-08, 개발용 임시 계약)

출처: 사용자 승인한 첫 작업 네 항목, PDF v0.3 p5(Command/ACK), p6(Topic/Payload), p11(실패 이유). 이번 PDF SHA256은 99CC217588343A3CD2526EB23779DE818DDF9AF75A8516D93073474D5BA0EB72. 과제2의 실제 이동·State/Alarm 처리는 아직 구현하지 않았다.

## 명령 형식

Topic: `amr/{amrId}/command`. 기존 `/cmd`는 사용하지 않는다. ID에 공백·제어문자·`/`·`+`·`#`를 허용하지 않는다.

```json
{"cmdId":"CMD-unique-id","type":"MOVE","ts":2208988800123,"param":{"x":30.0,"y":15.0}}
{"cmdId":"CMD-another-id","type":"STOP","ts":2208988800124,"param":{}}
{"cmdId":"CMD-third-id","type":"RESET","ts":2208988800125,"param":{}}
```

- cmdId/type/ts/param은 모두 필수다. cmdId는 비어 있지 않은 문자열, type은 대소문자를 구분하는 MOVE/STOP/RESET, ts는 기존 signed Int64 Unix 밀리초다.
- MOVE param은 유한 숫자 x/y만 포함한다. STOP/RESET param은 빈 object다. 빈 param 형식은 PDF의 확정값이 아니라 이번 개발 선택이다.
- 최대 65536 bytes, 깊이 16, strict UTF-8 JSON object. 알 수 없는 필드, 중복 키, 잘못된 자료형, 주석·후행 쉼표를 거부한다.
- 관제 발행 전과 가상 AMR 수신에서 같은 CommandValidator를 사용한다. 발행 전 거부는 ArgumentException의 코드·필드·이유로 호출자에게 전달되며 MQTT 발행하지 않는다.
- 요청 측 콘솔(Core 대역)이 `CMD-` + Guid로 한 번 생성한다. Communication과 AMR은 ID를 바꾸거나 재생성하지 않는다. 실제 Core 생성기·전체 요청/ACK 매핑은 COM-05/CORE-04 후속이다.

## ACK와 오류

수신 ACK 계약은 `Validation/CONTRACT.md`의 v3를 따른다. reason은 선택 nonempty 문자열이며, 새 TestClient의 실패 응답에는 항상 이유가 들어간다. 기존 reason 없는 ACK 수신은 호환 유지한다.

- JSON을 읽을 수 없거나 cmdId가 없거나 잘못됐거나 중복되면 로그를 남기고 폐기한다. 임의의 ID로 ACK를 만들지 않는다.
- 유효하고 유일한 cmdId를 읽을 수 있는 거부 요청은 같은 ID로 FAILURE 및 검사 실패 이유를 응답한다.
- 현재 TestClient는 이동·정지·복구 시뮬레이터가 아니다. 형식이 정상인 MOVE/STOP/RESET도 `FAILURE`, `reason: SIMULATION_NOT_IMPLEMENTED`를 반환한다. 수신 성공과 동작 완료를 혼동하지 않는다.
- 정상 SUCCESS ACK 수신 자체는 지원한다. 실제 이동 완료 SUCCESS는 이후 시뮬레이션 구현 시 동작 완료에 맞춰 발생시킨다.

## 실행

먼저 `init.cmd verify`와 Broker 실행을 마친다. 각각 별도 터미널에서 실행한다.

```powershell
dotnet run --no-build --project src/Amr.TestClient -- AMR_001
dotnet run --no-build --project src/Amr.Communication.Console -- --command MOVE --amr-id AMR_001 --x 30 --y 15 --duration-seconds 5
```

STOP/RESET은 `--command STOP` 또는 `--command RESET`을 사용하고 x/y를 생략한다. 한 실행에서 한 명령만 보낸다. --heartbeat 또는 init.cmd start -Heartbeat는 수신만 하며 자동 MOVE를 보내지 않는다. ACK 출력에서 cmdId, result, reason, ts를 확인한다.

실제 Broker 회귀: `pwsh -NoProfile -File tools/verify-command-basics.ps1 -Docker <docker.exe 경로>`. 사용자 기존 프로세스는 중단하지 않는다. 테스트는 자기 프로세스만 정리하며, 생산자 강제 종료를 Ctrl+C 정상 종료 증거로 쓰지 않는다.
