# S021 AMR 독립 처리 검증

결과: **독립 처리 실패, COM-01 revision 3 / in_progress 유지**. 제품 코드를 변경하지 않고 기존 도구에 5초 지연과 처리 시간 기록을 추가해 실제 Mosquitto에서 실행했다.

## 실험 방법과 결과

시험용 A/B는 각각 AMR_001/AMR_002 역할이다. 사용자 실행과 겹치지 않게 이번 실행에서는 `AUDIT_a919bf79d9_A`, `AUDIT_a919bf79d9_B`라는 고유 ID를 사용했다. 하나의 MQTT 연결과 지연 가능한 Core 대역으로 현재 제품 수신 경로를 검증했다.

1. A의 첫 Heartbeat(ts=401)가 Core 대역에서 처리되기 시작하면 완료를 보류한다.
2. A의 두 번째·세 번째(ts=402,404), B의 Heartbeat(ts=403)를 차례로 실제 발행한다.
3. 5초 동안 B가 처리되는지 확인한 뒤 A를 해제한다.
4. 처리 시작·완료 순서와 연결 종료를 기록한다.

`ts` 값은 시험 메시지 식별값이다. 지연 측정에는 Payload ts가 아니라 같은 Stopwatch 시계를 사용했다.

| A 첫 처리 시작 기준 경과 | 관찰 |
| --- | --- |
| 0초 | A 첫 메시지 처리 시작, 완료 보류 |
| 약 5.010초까지 | B 처리 없음. A 두 번째·세 번째 처리도 시작하지 않음 |
| 약 5.010초 | A 지연 해제, 첫 처리 완료 |
| 약 5.011초 | A 두 번째·세 번째 처리 후 B 처리 시작 |

| 검증 | 결과 |
| --- | --- |
| 다른 AMR 독립 처리 | **실패**: B는 A를 해제한 뒤에야 처리됨 |
| 같은 AMR 순서 | 통과: 401 → 402 → 404, 지연 중 추월 없음 |
| 비정상 입력 후 정상 처리 | 통과: JSON/필수 필드/ID/UTF8 오류 4건 거부, 다음 정상 수신 |
| 지연 해제·메시지 처리 완료 후 종료 | 통과: Disposed 도달, 약 5.98ms |

추가 네 유형 수신·필드 보존·명령 전달·Topic·발행 전 검사도 통과했다. 진단은 총 9개 중 8개 통과/1개 실패이다. 기존 도구의 종료 코드 0은 증거 수집 완료를 뜻하며 전체 기능 통과가 아니다.

종료 검사는 지연을 해제하고 메시지를 처리한 다음 실행했다. 아직 없는 AMR별 작업자의 취소·큐 잔여 메시지 정리까지 검증했다는 의미가 아니다. 큐 용량 초과 정책·실제 Core/GUI/이동도 이번 검증 대상이 아니다.

## 증거

- `run-1/result.json`: 항목별 결과와 실행 시각(2026-09-29T14:14:30.6763289+09:00에 수집 완료).
- `run-1/processing-timeline.json`: 처리 시작·완료·지연 해제의 동일 시계 기록. A 시작840.0806ms, 해제5850.403ms, B 시작5851.1038ms.
- `run-1/typed-messages.json`, `run-1.log`: 실제 수신 모델 및 콘솔 출력.
- `build.log`: 진단 도구 빌드, 경고0/오류0.
- `source-sha256.json`: 시작 HEAD dac677c42a9c866aa6d6dee653981a05909a9166와 이번 진단 코드·기존 사용자 변경을 포함한 실행 소스 해시. 진단 도구 외에는 S020 해시와 일치한다.
- `../../bootstrap/20260929T141301525+0900-376671/`: 표준 verify 제품375 + 도구37 = 412건 통과, skip0, 기존 NU1701 경고6건. 처음 샌드박스 NuGet 접근 실패는 `20260929T141253309+0900-d3dbb0/`에 보존하고 승인된 같은 명령으로 재검증했다.

환경: Windows 10.0.26200, .NET SDK10.0.401, PowerShell7.6.5, MQTTnet5.2.0.1603, Docker Mosquitto2.1.2-alpine, 127.0.0.1:1883, MQTT5/QoS0/Retain=false. 테스트 발행자와 Core·로그 대역을 사용했다. 기존 Broker·Seq 및 사용자 프로세스는 중단하지 않았다.

## 재현

개발 Broker를 켠 뒤 프로젝트 루트에서 실행한다. 이미 사용한 증거 폴더를 덮어쓰지 않도록 매번 새 경로를 만든다.

```powershell
dotnet build tools/Amr.Communication.Audit --no-restore
$runPath = 'evidence/COM-01/independence-' + (Get-Date -Format yyyyMMddTHHmmss)
dotnet tools/Amr.Communication.Audit/bin/Debug/net10.0/Amr.Communication.Audit.dll 127.0.0.1 1883 $runPath 5000
```

마지막 숫자가 지연 관찰 시간(ms)이다. 생략하면 기존 1000ms를 사용한다. 현재 기대 관찰은 `FAIL other_amr_independent`다. 다음 구현은 AMR별 대기열과 순차 처리 작업을 분리하고 같은 시험에서 B가 A 해제 전에 처리되게 하는 것이다.
