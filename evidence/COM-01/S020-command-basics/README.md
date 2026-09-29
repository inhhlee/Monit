# S020 기본 명령 네 항목 결과

사용자가 승인한 첫 작업 1~4(/command, 형식 검사, 고유 ID 보존, 거짓 SUCCESS 제거/실패 이유 전달)를 구현했다. 이동·상태 시뮬레이션은 구현하지 않았다.

| 항목 | 결과 | 실제 확인 |
| --- | --- | --- |
| /command 통일 | 통과 | MOVE/STOP/RESET 모두 선택한 AMR의 /command로 원문 그대로 도착; 다른 대상/구식 /cmd로 미전달 |
| cmdId/type/ts/param 검사 | 통과 | 깨진 JSON/필수항목·자료형·유형·좌표·중복키·UTF8 오류 검사. FLY/깨진 JSON은 발행 전 거부, MQTT 추가 전달 0 |
| 고유 ID·보존 | 통과 | 실제 콘솔 세 번의 요청 ID가 모두 다르며 AMR과 관제 ACK에서 동일 값 확인 |
| 결과·실패 이유 | 통과 | 정상 요청도 현재 이동 미구현이므로 FAILURE/SIMULATION_NOT_IMPLEMENTED. 유효한 ID의 잘못된 유형/좌표에는 검사 이유; 깨진/없는/중복 ID에는 ACK 없음 |
| 수신 회귀 | 통과 | 두 AMR × 네 유형 전달, ACK reason 보존, 비정상 네 입력 거부 후 정상 처리. 합성 SUCCESS도 그대로 수신 |
| 기존 연결 감시 | 통과 | COM-03 실제 Broker 5/5; 중단0.063초, 응답차단3.097초, idle6.006초 및 새 프로세스 연결 |
| AMR별 독립 처리 | 실패 유지 | A 처리 정지 시 B도 1초간 대기. COM-01 verification5는 후속이며 전체 passing 아님 |

## 검증 증거

- 표준 verify: `../../bootstrap/20260929T131821179+0900-3bd09a/`, 제품375 + 도구37 = **412건 통과**, skip0, 오류0. 기존 WPF NU1701 경고6건.
- `probe/result.json`, `probe/typed-messages.json`, `probe.log`: 진단8개 중7개 통과, 다른 AMR 독립 처리1개 실패. 종료0은 수집 완료이지 전체 통과가 아니다. 기대값을 완화하지 않았으며 명령 fixture만 새 계약에 맞췄다.
- `broker-20260929T132025/result.json`: 현재 Console/TestClient 실제 실행 **9/9 통과**. observer.log, amr.log, MOVE/STOP/RESET.log에 cmdId·결과·이유·Heartbeat 포함. 이 스크립트는 실패 시 nonzero로 종료한다.
- COM-03 `../../COM-03/20260929T132120-f38e5bd6b7b14956baad777cbf1bafa9/result.json`: 5항목 모두 passed.
- 초기 기준선 `../../bootstrap/20260929T124006136+0900-b065b4/`, baseline-retry.log/trx 및 code-integrity.json: Windows Smart App Control 0x800711C7 차단. 사용자 직접 실행에서도 재현. 수정 후 표준 실행과 실제 Broker 검사에서는 재현되지 않았다. 에이전트가 Windows 보안 설정을 변경하지 않았으며 외부 설정 변화/재발 여부는 단정하지 않는다.
- source-sha256.json: 실행 코드·테스트·진단 도구 해시. 시작 HEAD2f0ef64 및 사용자 로컬 변경이 있는 환경이며 사용자 변경은 커밋 제외.

## 실행

Broker를 켜고 init.cmd verify로 빌드한 다음 별도 터미널에서 실행한다.

```powershell
dotnet run --no-build --project src/Amr.TestClient -- AMR_001
dotnet run --no-build --project src/Amr.Communication.Console -- --command MOVE --amr-id AMR_001 --x 30 --y 15 --duration-seconds 5
```

기대 결과: 같은 cmdId의 FAILURE, reason=SIMULATION_NOT_IMPLEMENTED. 이것은 통신 오류가 아니라 가상 이동 미구현 응답이다. STOP/RESET은 좌표 인수를 빼고 요청한다. init.cmd start -Heartbeat는 수신만 하며 자동 MOVE를 보내지 않는다.

재검증: `pwsh -NoProfile -File tools/verify-command-basics.ps1 -Docker <docker.exe 경로>`. 전체 COM-01 진단은 `dotnet run --project tools/Amr.Communication.Audit -- 127.0.0.1 1883 <새 증거 경로>`. 기존 S018 진단 스크립트는 당시 자동 MOVE 동작의 역사적 시험이므로 현 명령 검증에는 새 스크립트를 사용한다.

## 범위와 다음 작업

COM-04 revision4 passing, COM-03 passing, COM-01 revision3 in_progress. COM-02/05 not_started 유지. 실제 Core 대역을 사용하며 ACK 성공의 합성 입력은 실제 이동 성공 증거가 아니다. 요청한 네 항목 이후에는 COM-01의 AMR별 독립 큐를 보완한다. Timeout·재접속·전체 매핑·실제 시뮬레이션·GUI/DB는 미완료다. 생산자 테스트 프로세스는 자체 정리하므로 Ctrl+C 정상 종료 검증으로 사용하지 않는다. 사용자 서비스/프로세스는 중단하지 않았다.
