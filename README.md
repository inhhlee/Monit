# AmrControlSystem (practice)

가상 AMR 통신·관제 시스템의 개발 저장소다. **COM-03: MQTT Broker 연결 및 연결 상태 감시**, **COM-04: 수신 메시지 유효성 검증**, **COM-01: 실제 MQTT 송수신 및 AMR별 비동기 큐**, **COM-02: AMR별 Heartbeat Timeout·복구 감지**를 구현·검증했다. 현재 기준 COM-01~04는 passing이며, 다음은 COM-05 Command/ACK 식별자 연계 검증이다. 기존 WPF 화면은 실험용이고, Core·SQLite·GUI·실제 AMR 이동 통합은 후속 범위다.

이 기준선은 **다른 Windows 노트북과 다른 체크아웃 경로에서 같은 명령으로 시작하는 것**을 목표로 한다. WPF 때문에 macOS/Linux 전체 솔루션 실행은 지원하지 않는다. 새 노트북 실물 검증 여부와 최근 증거는 [진행 기록](claude-progress.md)을 따른다.

## 기획과 설계 문서

사용자 제공 KubeOpsAgent의 문서 구성을 참고해 `instruction/`과 `project_guide/`를 추가했다. 요구사항·기능명세 버전0.2는 PDF v0.3 원문을 대조하여 **다른 개발자가 구현할 전체 제품의 목표**로 개정했다. 현재 구현 상태는 PDF 비교서와 진행 로그에 분리했다. 기존 C# 소스·테스트·도구 구조와 제품 실행 계약은 유지하며, 실제 기능 상태·revision·증거는 계속 `feature_list.json`이 기준이다.

| 문서 | 내용 |
| --- | --- |
| [프로젝트 소개](project_guide/introduction.md) | 목표·현재 상태·폴더 구성·읽는 순서 |
| [요구사항 정의서](instruction/prd.md) | 범위·요구사항·인수 목표·결정 대기 사항 |
| [기능명세서](instruction/feature_spec.md) | 입력·처리·출력·예외·메시지 계약·검증 |
| [공통 데이터와 인터페이스](instruction/interface_contract.md) | wire·Core/GUI·저장·상태·설정·시험 프로파일 |
| [PDF 비교와 결정대장](instruction/pdf_comparison.md) | 원문 페이지별 대응·누락·충돌·권고·현재 구현 차이 |
| [사용자 시나리오](project_guide/user_scenario.md) | 생존·명령·이동·장애·복구·통합 흐름 |
| [상세 설계](project_guide/detail_architecture.md) | 현재/목표 구조·책임·인터페이스 |
| [개발 작업 목록](instruction/task.md) | 단계별 작업과 완료 기준 |
| [Iteration 01](instruction/iteration_01.md) | 다음 COM-05의 범위와7개 검증 계획 |

신규 개발자는 요구사항 → 기능명세 → 공통 계약 → PDF 비교/결정대장 순으로 읽는다. 개발 작업 목록과 Iteration01은 기존 저장소의 재개 계획이다. Telemetry·Alarm의 관제 수신은 구현됐지만 가상 AMR의 실제 생성·발행은 후속이다. 문서 개정으로 제품 기능 상태나 실행 계약 revision을 변경하지 않았다.

## 다른 Windows PC에서 처음 시작하기

1. Git for Windows, **.NET SDK 10.0.401**(global.json이 허용하는 10.0.4xx 패치), **PowerShell 7.4 이상**을 설치하고 터미널을 새로 연다. Python과 Codex 전용 런타임은 필요하지 않다.
2. Broker를 이 PC에서 실행하려면 Docker Desktop을 설치하고 **Linux 컨테이너 엔진**을 켠다. 빌드·자동 테스트만 할 때는 Docker가 필요 없다.
3. 저장소를 복제하고 해당 폴더로 이동한다.

   ```powershell
   git clone https://github.com/indsf/practice.git
   cd practice
   ```

4. 다음 명령을 차례로 실행한다. Windows PowerShell 5.1 창에서도 `init.cmd`가 PowerShell 7을 호출한다.

   ```powershell
   .\init.cmd check
   .\init.cmd verify
   .\init.cmd broker
   .\init.cmd start
   ```

`verify`에서 **BASELINE PASSED**, `start`에서 **Connected**가 나오면 기본 개발 준비와 Broker 연결에 성공한 것이다. 수신 실습은 `init.cmd start -Heartbeat`로 실행하고 **Subscribed:**를 확인한다. Heartbeat가 없으면 대기하고, 수신하면 검사 결과를 출력한다. 종료는 Ctrl+C다. 관제 GUI나 로봇 이동을 실행하는 명령은 아니다.

최초 NuGet 패키지 복원과 Docker 이미지 다운로드에는 네트워크가 필요하다. 시스템 설정 변경이나 프로그램 설치를 헬퍼가 자동으로 수행하지 않는다.

## 평소 사용하는 명령

| 목적 | 명령 | 필요한 조건 |
| --- | --- | --- |
| SDK·Git·실행 환경 확인 | ` .\init.cmd check ` | Windows, PowerShell, .NET SDK, Git |
| 전체 빌드·자동 테스트·기능 추적기 검사 | ` .\init.cmd verify ` 또는 ` .\init.cmd ` | 최초 NuGet 접근 |
| 개발 Broker 실행 | ` .\init.cmd broker ` | Docker Desktop 실행 |
| 통신 콘솔 빌드·실행 | ` .\init.cmd start ` | 접근 가능한 MQTT Broker |
| 실제 COM-03 연결·단절 검증 | ` .\init.cmd verify-broker ` | Docker 실행 및 이미지 다운로드 가능 |
| 개발 Broker 중지 | `docker compose -f docker/docker-compose.yml stop mosquitto` | Docker 실행 |

`check`는 준비 도구만 검사한다. `start`는 테스트 없이 통신 콘솔을 빌드·실행한다. `verify`는 Broker 없이 실행된다. 기본 검증 통과를 실제 Broker 검증 통과로 간주하지 않는다.

PowerShell 7에서는 `./init.ps1 verify`, Git for Windows Bash에서는 `bash ./init.sh verify`로 같은 헬퍼를 호출할 수 있다. 이전 `./init.ps1 -Start`는 검증 후 실행하는 호환 명령으로 유지한다.

다른 폴더에서 호출해도 헬퍼는 자기 위치를 프로젝트 루트로 사용한다.

```powershell
& 'D:\작업 폴더\practice\init.cmd' verify
```

## 가상 AMR Heartbeat 보내기 (기초 실습)

Docker Desktop과 개발 Broker를 켠 뒤 프로젝트 루트에서 실행한다.

```powershell
.\init.cmd broker
dotnet run --project src/Amr.TestClient
```

`Connected` 다음에 `Published: amr/AMR_001/heartbeat {"id":"AMR_001","ts":...,"status":"OK"}`가 약 1초마다 출력된다. `ts`는 실행 시점의 Unix 밀리초 시각이며 매번 새로 만든다. 종료는 Ctrl+C다. Broker가 이미 실행 중이면 두 번째 명령만 실행한다.

다른 터미널에서 ` .\init.cmd start -Heartbeat `로 관제 콘솔을 실행한다. `Subscribed: amr/+/heartbeat` 다음에 `Received Heartbeat: AMR_001 ts=...`가 약 1초마다 출력되면 **가상 AMR → Mosquitto → 관제 검사기 → 콘솔 출력**이 연결된 것이다. 관제와 가상 AMR은 각각 Ctrl+C로 종료한다. 실제 Core 대신 콘솔 출력 대역을 사용한다.

MQTT Explorer를 같은 `127.0.0.1:1883`에 연결해 `amr → AMR_001 → heartbeat`를 확인해도 된다. 해당 Topic에 `{}` 또는 깨진 JSON을 발행하면 관제에는 `Rejected`와 실패 이유가 나오고 정상 수신 출력에는 포함되지 않는다. 이후 정상 Heartbeat는 계속 처리된다. 프로그램의 Published 출력 자체는 발행 호출 완료이며 실제 수신은 관제나 Explorer에서 확인한다.

코드는 [Amr.TestClient/Program.cs](src/Amr.TestClient/Program.cs)의 **연결 → JSON 생성 → 발행 → 1초 대기** 순서로 읽으면 된다. AMR ID 기본값은 `AMR_001`이며 `dotnet run --project src/Amr.TestClient -- AMR_002`로 다른 ID를 줄 수 있다. Broker 주소는 `127.0.0.1:1883`으로 고정되어 있다. Heartbeat 수신 모드는 자동 명령을 보내지 않는다. 명령은 아래처럼 별도로 요청한다. 가상 AMR은 실제 이동이 미구현이므로 정상 MOVE/STOP/RESET에도 FAILURE와 SIMULATION_NOT_IMPLEMENTED를 응답한다. QoS 0, Retain false를 사용하며 자동 재접속은 후속이다. Heartbeat 중단 감지는 S024에서 실제 Mosquitto 및 콘솔/가상 AMR 두 대로 검증했다. Broker 연결이 끊기면 오류를 출력하고 종료하며, Broker 복구 후 프로그램을 다시 실행한다.

## 기본 명령 한 건 보내기

`init.cmd verify`로 빌드하고 Broker와 가상 AMR을 실행한 뒤, **다른 터미널**에서 아래 명령을 실행한다.

```powershell
dotnet run --no-build --project src/Amr.Communication.Console -- --command MOVE --amr-id AMR_001 --x 30 --y 15 --duration-seconds 5
```

요청마다 새로운 `cmdId`를 만들며 `Published Command`와 `Received Ack`의 ID가 같아야 한다. 현재 기대 응답은 `FAILURE`, 이유는 `SIMULATION_NOT_IMPLEMENTED`다. 통신은 완료됐지만 가상 이동은 아직 구현하지 않았다는 뜻이다. `STOP`과 `RESET`은 `--x 30 --y 15`를 빼고 요청한다. 실행 중인 프로그램을 다시 빌드할 때 파일 잠금 오류가 나면 기존 프로그램을 Ctrl+C로 종료한 뒤 빌드한다. 이미 빌드한 코드만 실행할 때는 `--no-build`를 사용한다.

명령 형식은 [Command 계약](src/Amr.Communication/Command/CONTRACT.md), 실제 검증 결과는 [S020 결과](evidence/COM-01/S020-command-basics/README.md)에 있다.

## AMR별 Heartbeat 중단 감지 (COM-02)

수신 모드에서는 처음 정상 Heartbeat를 받은 AMR부터 감시한다. 기본값은 **5초 동안 정상 수신 없음**, 확인 주기는 **0.5초**다. 가상 AMR의 Payload 시각이나 Core 처리 대기시간이 아니라 관제에서 실제로 받은 시각을 사용한다. 구현의 현재 검증 상태는 `claude-progress.md`를 확인한다.

직접 관찰하려면 Broker를 켜고 한 번 빌드한 뒤, 관제와 가상 AMR2대를 서로 다른 터미널에서 실행한다. 실행 중인 파일의 재빌드를 피하도록 아래는 `--no-build`를 사용한다.

```powershell
# 창1: 관제
dotnet run --no-build --project src/Amr.Communication.Console -- --heartbeat

# 창2: 가상 AMR 001
dotnet run --no-build --project src/Amr.TestClient -- AMR_001

# 창3: 가상 AMR 002
dotnet run --no-build --project src/Amr.TestClient -- AMR_002
```

1. 관제에 두 AMR의 `HeartbeatMonitoringStarted`와 주기적 Heartbeat가 표시된다.
2. **AMR_001 창에서만 Ctrl+C**를 누른다. Broker와 AMR_002는 유지한다.
3. 마지막 정상 수신 약5~5.5초 뒤(스케줄링 지연 별도) `HeartbeatTimeout`과 `AmrId: AMR_001`이 한 번 표시되어야 한다. AMR_002에는 Timeout이 없어야 한다.
4. 같은 AMR_001 명령을 다시 실행하면 `HeartbeatRecovered`가 한 번 표시되어야 한다.

설정 변경은 관제 실행 인수에 `--heartbeat-timeout-seconds 10 --heartbeat-check-seconds 1`처럼 추가한다. 최초 수신 전 장비를 추정하거나 최종 Offline 상태로 바꾸지 않는다. 잘못된 Heartbeat는 시각을 갱신하지 않는다. Broker 연결이 끊겼다고 감지되면 AMR 감시도 중단하며, 재접속은 여전히 명시적으로 요청해야 한다.

실제 Broker 자동 검증 도구(약15초, Core/AMR은 대역)는 다음과 같이 실행한다. 기존 결과를 덮어쓰지 않도록 새 폴더를 사용한다. `result.json`에 완료 여부와 각 검증 결과가 남고 미완료·실패는 종료 코드1이다.

```powershell
$runPath = 'evidence/COM-02/heartbeat-check-' + (Get-Date -Format yyyyMMddTHHmmssfff)
dotnet run --project tools/Amr.Heartbeat.Audit -- 127.0.0.1 1883 $runPath
```

세부 정책은 [Heartbeat 감시 계약](src/Amr.Communication/Monitoring/CONTRACT.md)을 따른다.

관제 콘솔과 실제 TestClient 프로세스 두 개로 기본5초 감지·복구를 자동 재현하려면 `pwsh -NoProfile -File tools/verify-heartbeat-basics.ps1`을 실행한다. 기존 Broker를 사용하며 이 도구가 시작한 AMR 한 대만 중단·재실행한다. 키보드 Ctrl+C 입력 자체를 검증하는 절차는 아니다. 결과는 새 `evidence/COM-02/console-<시각>/`에 저장된다.

`Amr.Heartbeat.Audit`는 솔루션의 `tools`에 등록되어 표준 빌드에 포함된다. 표준 `verify`에서는 빌드만 하며 실제 Broker 검사는 위 명령으로 따로 실행한다. S024 검증 결과는 [Heartbeat 결과 보고서](evidence/COM-02/S024-heartbeat/README.md)에 기록했다.

## AMR별 비동기 수신 처리

정상 메시지를 AMR별 대기열에 넣고 각 AMR의 작업이 하나씩 처리한다. 같은 AMR은 순서를 지키고 다른 AMR은 기다리지 않는다. S022 실제 검증에서는 A를 5초 멈춘 동안 B가 A 처리 시작 후 약0.041초에 처리됐다. 이는 해당 실험의 관측값이며 처리 시간 보장값은 아니다.

현재 개발용 제한은 AMR당 대기256개, 연결당64개 AMR이다. 가득 찬 큐는 새 메시지를 거부하고 `ProcessingFailure` 로그를 남긴다. 종료·연결 끊김 시 진행 작업에 취소를 요청하고 남은 큐를 정리한다. 실제 Core를 연결할 때는 AMR 간 동시 호출과 취소 토큰을 지원해야 한다. [상세 계약](src/Amr.Communication/Mqtt/RECEIVE_QUEUE.md), [검증 결과](evidence/COM-01/S022-async-queues/README.md)를 참조한다.

## 실행 환경과 문제 해결

- **PowerShell을 찾지 못함:** `pwsh --version`을 확인하고 설치 후 터미널을 다시 연다. 표준 PATH를 사용할 수 없으면 `POWERSHELL_EXE`에 이 PC의 `pwsh.exe` 전체 경로를 지정한다. `init.cmd`와 `init.sh`가 사용한다.
- **SDK 불일치:** 프로젝트 폴더에서 `dotnet --version`을 확인하고 [global.json](global.json)에 지정된 SDK를 설치한다. 런타임만 설치하면 빌드할 수 없다.
- **Docker를 찾지 못함:** Docker Desktop을 켜고 터미널을 다시 연다. 필요하면 `AMR_DOCKER`에 이 PC의 `docker.exe` 경로를 지정한다.
- **Broker 접속 실패:** `docker compose -f docker/docker-compose.yml logs --tail 100 mosquitto`로 확인한다. 기본 Broker 주소는 `127.0.0.1:1883`이다. 포트 충돌이나 다른 PC의 Broker 사용 시 아래 환경변수를 지정한다.
- **한글 깨짐:** 헬퍼는 콘솔·외부 명령·저장 로그를 UTF-8로 맞춘다. 이전에 깨진 출력은 복원되지 않으므로 새 실행 로그를 확인한다.
- **Git 소유권 오류:** 공유한 작업 폴더의 소유자와 실행 계정을 확인한다. 개인 계정으로 새 경로에 clone하는 방법을 우선한다. 헬퍼는 전역 safe.directory를 자동 변경하지 않는다.
- **소스 ZIP:** 일반 빌드·실행은 가능하지만 커밋과 실제 Broker 검증의 코드 버전 기록에는 Git 체크아웃이 필요하다.

개발 기본값은 KeepAlive 5초, 연결·통신 timeout 3초다. 아래 환경변수로 설정한다. 인증값을 진행 문서나 증거에 넣지 않는다.

```powershell
$env:AMR_MQTT_HOST = '127.0.0.1'
$env:AMR_MQTT_PORT = '1883'
.\init.cmd start
# 15초만 실행하는 재시작 확인
.\init.cmd start -DurationSeconds 15
```

추가 설정: `AMR_MQTT_KEEPALIVE_SECONDS`, `AMR_MQTT_TIMEOUT_SECONDS`, `AMR_MQTT_USERNAME`, `AMR_MQTT_PASSWORD`. 직접 Heartbeat 수신 콘솔을 실행하려면 `dotnet run --project src/Amr.Communication.Console -- --heartbeat`를 쓴다. `--heartbeat` 없이 직접 실행하면 기존 연결 전용 모드이며 COM-03의 무업무 메시지 KeepAlive 검증에 사용한다. 현재 실행 helper에서는 `init.cmd start -Heartbeat`로 수신 옵션을 넣는다. `init.cmd start`만 실행하면 연결 전용이다.

## 검증과 증거

`verify`는 restore → 전체 solution build → 기능 추적기 검사 → 모든 .NET 테스트 프로젝트 실행 → TRX 검사 순으로 동작한다. 실패, 0건, 미실행, 결과 불일치는 성공으로 처리하지 않는다. 로그와 TRX는 `evidence/bootstrap/<실행ID>/`에 쌓이며 기존 기록은 지우지 않는다.

S010부터 복원·빌드는 작업자 하나와 재사용 비활성화로 실행한다. 현재 환경의 기본 병렬 복원 실패를 복구한 설정이며 테스트를 생략하지 않는다. COM-04의 [임시 수신 계약](src/Amr.Communication/Validation/CONTRACT.md)은 Telemetry/Alarm/Heartbeat/ACK의 Topic·JSON·필드·범위를 검증한다. 성공한 불변 모델만 Core 수신 인터페이스에 전달하며 실제 MQTT에서 Telemetry/Alarm/Heartbeat/ACK 네 유형을 구독한다. S018에서 두 AMR의 네 유형 수신을 확인했지만 COM-01 전체는 미완료이다. 기존 WPF와 TelemetryPayload는 이 검증 경로를 사용하지 않는다.

검증 구현은 [tools/Amr.Verification](tools/Amr.Verification)에 있다. 이전 Python `scripts/`와 `tests/harness/`를 대체한다. 이미 빌드한 상태에서 추적기만 검사하려면:

```powershell
dotnet tools/Amr.Verification/bin/Debug/net10.0/Amr.Verification.dll tracker feature_list.json
```

`verify-broker`는 기본 검증 후 Mosquitto 2.1.2의 임의 localhost 포트와 고유한 임시 컨테이너를 사용한다. 실제 연결, 6초 idle/KeepAlive 2초 및 PINGREQ/PINGRESP, 정상 Broker 종료, 응답 차단, 최초 연결 실패, 콘솔 재시작을 검사한다. 응답 차단은 테스트 TCP 프록시로 구현한다. 생성한 테스트 컨테이너만 정리하며 기존 개발 Broker·Seq·데이터는 보존한다. 결과는 `evidence/COM-03/<실행ID>/result.json`과 관련 로그에 남긴다.

기능 통과 상태는 결과만 보고 자동 변경하지 않는다. 현재 spec_revision과 항목별 증거를 검토하여 [feature_list.json](feature_list.json)에 기록한다. 과거 Python 명령과 PC 경로는 당시 증거·세션 기록에만 보존한다.

## 새 세션에서 이어가기

[claude-progress.md](claude-progress.md) → [AGENTS.md](AGENTS.md) → [feature_list.json](feature_list.json) 순서로 읽는다.

| 초기화 플레이북의 질문 | 기준 파일·절차 |
| --- | --- |
| 무엇을 만드는가? | 이 문서 첫 단락과 feature_list.json의 scope |
| 어떻게 시작하는가? | `init.cmd broker` → `init.cmd start` |
| 어떻게 검증하는가? | `init.cmd verify`; 실제 Broker 검증은 `init.cmd verify-broker` |
| 무엇이 미완성인가? | feature_list.json의 status·blocker와 진행 로그 |
| 다음 행동은 무엇인가? | 진행 로그의 Next best action; 현재 COM-02 실행 허용 확인 및 COM-01 회귀 |
| 돌아갈 안전 기준선은? | 진행 로그의 기준선 커밋과 Git 이력 |

`AGENTS.md`는 작업 규칙, `feature_list.json`은 기능·설계·상태·증거, `claude-progress.md`는 현재 상태와 인수인계, `init.cmd/init.ps1/init.sh`는 시작·검증 경로를 담당한다. 기록된 이전 작업을 초기화하거나 진행 기능을 임의로 완료 처리하지 않는다.

## 구조와 남은 범위

| 경로 | 역할 |
| --- | --- |
| `src/Amr.Communication/` | Broker 연결·감시, 수신 검증, 명령 발행 전 검사, AMR별 독립 처리 큐 |
| `src/Amr.Communication.Console/` | 연결·수신 로그 및 명시적 명령 요청 |
| `src/Amr.TestClient/` | Heartbeat 발행 및 검사 후 사실에 맞는 실패 ACK |
| `src/AmrControlSystem/`, `src/AmrControlTest/` | WPF 실험 앱 |
| `tests/Amr.Communication.Tests/` | 통신 제품 자동 테스트 |
| `tests/Amr.Verification.Tests/` | 추적기·TRX·테스트 프록시 검증 |
| `tools/Amr.Verification/` | .NET 개발 검증 도구 |
| `docker/` | 로컬 Mosquitto·Seq 설정 |
| `evidence/` | 성공·실패 실행 증거 |

S024 최종 표준 검증440건(제품403/도구37, Heartbeat19 포함), 실제 Heartbeat8시나리오, 콘솔5항목, 송수신/큐12항목, 명령9항목 및 COM-03 연결5항목을 통과했다. COM-01 revision4/COM-02 revision2/COM-03 revision2/COM-04 revision4는 passing이고 COM-05는 not_started다. 초기 Windows DLL 차단과 재시도 실패는 증거에 보존했고 최종 실행에서는 재현되지 않았다. 현재 상태와 증거는 feature_list.json, claude-progress.md 및 [S024 결과](evidence/COM-02/S024-heartbeat/README.md)를 따른다. 기존 WPF NU1701 경고와 GUI 직접 MQTT/SQLite 접근은 별개 실험 경로다. 실제 이동·자동 재접속·GUI/Core 통합·다른 PC 검증은 후속이다.

## COM-04 수신 검증 계약

[현재 계약 v3](src/Amr.Communication/Validation/CONTRACT.md)에 맞는 Telemetry·Alarm·Heartbeat·ACK만 Core 경계로 전달한다. 잘못된 입력은 실패 이유를 로그에 남기고 폐기하며 다음 정상 입력을 계속 처리한다.

- Telemetry: `pos{x,y}`와 `sp`, `state`를 분리한다. State는 `IDLE/BUSY/STOPPED/FAULT`.
- Alarm: `level`은 `INFO/WARN/ERR/FATAL`, `alarmState`는 개발용 `ACTIVE/CLEARED`. `message`는 선택 문자열.
- Heartbeat: `id`, `ts`, `status`가 필수이며 현재 `status`는 `OK`를 사용한다.
- ACK는 선택 `reason`을 보존한다. 기존 reason 없는 ACK도 받을 수 있다.
- `ts`는 기존 Unix 밀리초 Int64 계약을 유지한다. PDF 예시 숫자를 자릿수로 변환하지 않는다.

이전 `Pos/sp/stat` 형식과 `status` 없는 Heartbeat는 거부된다. 현재 실제 구독은 Telemetry·Alarm·Heartbeat·ACK 네 유형이다. 정상 모델은 AMR별 큐로 보내며 Command 발행 전 검증도 적용했다. 검증 테스트는 `tests/Amr.Communication.Tests/MessageValidationTests.cs`의 `PdfPageSixExamplesPreserveData`부터 읽는다.
