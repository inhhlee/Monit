# S018 현재 기능 점검 — 2026-09-29

실제 실행 결과: **기본 송수신과 수신 검증은 동작하지만 COM-01 전체는 미완료**다. 제품 코드는 이번 점검에서 수정하지 않았다. 잘못된 `passing` 표시만 `in_progress`로 정정했다.

## 확인한 범위

| 기능 | 결과 | 확인 내용 |
| --- | --- | --- |
| 빌드·기존 자동 테스트 | 통과 | 표준 verify-broker: 제품306 + 도구37 = 343건, skip 0 |
| Broker 연결·KeepAlive·끊김·최초 연결 실패·재시작 | 통과 | 실제 Mosquitto 5항목; idle 6.009초, 종료 감지 0.071초, 응답 차단 감지 3.071초 |
| 두 AMR × 네 유형 수신 | 통과 | Heartbeat/Telemetry/Alarm/ACK 8건 Core 대역 도착, 필드와 실패 ACK 원값 보존 |
| 비정상 수신 거부·이후 정상 처리 | 통과 | 깨진 JSON/status 누락/ID 불일치/잘못된 UTF-8 4건 거부, 다음 정상 입력 전달 |
| 가상 AMR Heartbeat·Command/ACK | 기본 왕복 통과 | 실제 실행 파일로 명령3건 → 가상 AMR → ACK3건 → 관제 출력 |
| 명령 Topic 계약 | 실패 | 추적기는 /command인데 실제 발행·AMR 구독은 /cmd |
| 발행 전 명령 검사 | 실패 | FLY와 깨진 JSON이 예외 없이 Broker로 전달됨 |
| 같은 AMR의 순서 | 이번 시나리오 통과 | A 첫 처리를 지연하면 A 두 번째는 대기 |
| 다른 AMR의 독립 처리 | 실패 | A 처리 정지 중 B도 1초 동안 미처리; A 해제 후 B 도착 |
| 명령 ID 구분 | 실패 | 명령3건 모두 test-cmd-; 응답도 같은 ID라 요청별 구분 불가 |
| 잘못된 명령에 대한 AMR 처리 | 실패 | { 입력에도 AMR이 임의 cmdId를 새로 만들어 SUCCESS ACK 발행 |
| Heartbeat Timeout | 미구현 | HeartbeatMonitor가 비어 있음. AMR2 중단 뒤 알림도 관찰되지 않음; 확정 Timeout 정책 없으므로 시간 경계 합격 시험은 아님 |
| 자동 재접속 | 미구현 | Broker 끊김은 감지하나 콘솔 종료. 재접속 루프 없음; COM-03 통과에 포함되지 않음 |

ACK SUCCESS는 현재 실습 프로그램의 자동 응답이다. 로봇 이동 성공이나 실제 Core 업무 완료를 뜻하지 않는다. GUI/DB/실제 장비 운행은 검사하지 않았다.

## 증거

- 시작 실패: `../../bootstrap/20260929T120743034+0900-4c6de7/` (샌드박스 NuGet 접근), `../../bootstrap/20260929T120944246+0900-8d851d/` (빌드는 성공, COM-01 증거 경로 누락으로 tracker 실패).
- 최종 표준 검증: `../../bootstrap/20260929T121108361+0900-374798/`.
- COM-03 실제 Broker: `../../COM-03/20260929T121120-56119d760f6a4df7b383c9b2baf174ed/result.json`.
- `probe/result.json`, `probe/typed-messages.json`, `probe.log`: 실제 Broker + 제어 가능한 Core/로그 대역. 진단 8개 중 3개 요구 실패를 그대로 보존. 추적기 번호로는 COM-01 verification 5/6/7 실패.
- `console-20260929T121529/result.json`, console/amr1/amr2/invalid-command-amr 로그: 현재 관제·가상 AMR 실행 파일 검증.
- `tracker-before.json`: S017 완료 표시와 파일 경로 없는 원래 기록 전체 보존. 원래 evidence 한 건은 내용 변경 없이 추적기의 `unverified_historical_evidence`로 이동했다. 새 증거로 과거 실행을 꾸미지 않는다.
- `source-sha256.json`: 실행 코드·도구·실행 helper 해시. 현재 HEAD 9160f14와 사용자의 로컬 init.ps1 수정 기준. 사용자 init.ps1 수정은 점검 커밋에서 제외한다. 이전 S017의 전체 통과 주장은 이 레코드로 정정한다.

## 다시 실행

저장소 루트에서 실행한다. Docker 경로는 해당 PC의 실제 경로를 사용한다. 기본 Broker 주소는 127.0.0.1:1883이다. 아래 진단들은 현재 결함도 측정하므로 `result.json`의 항목별 failed를 반드시 읽는다. **진단 도구 종료0은 수집 완료이며 모든 요구사항 통과가 아니다.** 일반 테스트와 분리했고 solution에 추가하지 않았다.

```powershell
.\init.cmd verify-broker -Docker <docker.exe 경로>
dotnet run --project tools/Amr.Communication.Audit -- 127.0.0.1 1883 evidence/COM-01/S018-rerun
pwsh -NoProfile -File tools/verify-console-audit.ps1 -Docker <docker.exe 경로>
```

콘솔 진단은 기본 가상 AMR1/2 및 고유 AUDIT_BAD ID를 사용하며 로컬 개발 Broker에 실습 메시지를 보낸다. 기존 서비스를 중단하지 않는다. 이번 진단에서 실행한 생산자 프로세스는 스크립트가 중단하며, 그 결과를 Ctrl+C 정상 종료 검증으로 해석하지 않는다. 콘솔은 정해진 실행 시간 후 종료0을 확인했다.

## 다음 작업

COM-01에서 먼저 잘못된 명령 거부, /command 계약과 실행 코드 정합, AMR별 독립 처리를 보완한다. 고정 cmdId와 잘못된 SUCCESS 응답도 함께 정리하되, COM-05의 전체 요청/결과 매핑과 COM-02 Timeout을 통과 처리하지 않는다. 자동 재접속은 과제 문장에는 포함되지만 DN-04에서 미뤄졌으므로 향후 범위를 명시적으로 추적해야 한다.

최종 기록 검사: 추적기 5기능·35항목의 구조/증거 경로 검사와 git diff --check 통과. 제품 동작의 추가 합격 판정은 아니다.

점검 후 12:24:50에 기존 작업에서 MqttSubscriber.cs 설명 주석 한 줄이 추가된 것을 발견했다. 해당 변경도 보존하고 점검 커밋에서 제외한다. 실행 동작 변경은 없으며 source-sha256.json은 검사 당시 파일 해시를 유지한다.
