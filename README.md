# AmrControlSystem (practice)

가상 AMR 통신·관제 시스템의 개발 저장소다. 현재 구현된 제품 기능은 **COM-03: MQTT Broker 연결 및 연결 상태 감시**, **COM-04: PDF 반영 계약 v2에 따른 수신 메시지 유효성 검증**이다. 다음 기능은 **COM-01: 실제 MQTT 메시지 송수신 및 AMR별 처리 큐**다. 기존 WPF 화면은 실험용이고, Core·SQLite·GUI·실제 AMR 이동 통합은 후속 범위다.

이 기준선은 **다른 Windows 노트북과 다른 체크아웃 경로에서 같은 명령으로 시작하는 것**을 목표로 한다. WPF 때문에 macOS/Linux 전체 솔루션 실행은 지원하지 않는다. 새 노트북 실물 검증 여부와 최근 증거는 [진행 기록](claude-progress.md)을 따른다.

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

`verify`에서 **BASELINE PASSED**, `start`에서 **Connected**와 **Subscribed: amr/+/heartbeat**가 나오면 기본 개발 준비와 Broker 연결·Heartbeat 구독에 성공한 것이다. Heartbeat가 없으면 대기하고, 수신하면 검사 결과를 출력한다. 종료는 Ctrl+C다. 관제 GUI나 로봇 이동을 실행하는 명령은 아니다.

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

다른 터미널에서 ` .\init.cmd start `로 관제 콘솔을 실행한다. `Subscribed: amr/+/heartbeat` 다음에 `Received Heartbeat: AMR_001 ts=...`가 약 1초마다 출력되면 **가상 AMR → Mosquitto → 관제 검사기 → 콘솔 출력**이 연결된 것이다. 관제와 가상 AMR은 각각 Ctrl+C로 종료한다. 실제 Core 대신 콘솔 출력 대역을 사용한다.

MQTT Explorer를 같은 `127.0.0.1:1883`에 연결해 `amr → AMR_001 → heartbeat`를 확인해도 된다. 해당 Topic에 `{}` 또는 깨진 JSON을 발행하면 관제에는 `Rejected`와 실패 이유가 나오고 정상 수신 출력에는 포함되지 않는다. 이후 정상 Heartbeat는 계속 처리된다. 프로그램의 Published 출력 자체는 발행 호출 완료이며 실제 수신은 관제나 Explorer에서 확인한다.

코드는 [Amr.TestClient/Program.cs](src/Amr.TestClient/Program.cs)의 **연결 → JSON 생성 → 발행 → 1초 대기** 순서로 읽으면 된다. AMR ID는 `AMR_001`, 주소는 `127.0.0.1:1883`으로 고정한 첫 실습이다. QoS 0, Retain false를 사용하며 자동 재접속·Heartbeat 중단 감지는 후속 단계다. Broker 연결이 끊기면 오류를 출력하고 종료하며, Broker 복구 후 프로그램을 다시 실행한다. COM-01 전체 완료를 의미하지 않는다.

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

추가 설정: `AMR_MQTT_KEEPALIVE_SECONDS`, `AMR_MQTT_TIMEOUT_SECONDS`, `AMR_MQTT_USERNAME`, `AMR_MQTT_PASSWORD`. 직접 Heartbeat 수신 콘솔을 실행하려면 `dotnet run --project src/Amr.Communication.Console -- --heartbeat`를 쓴다. `--heartbeat` 없이 직접 실행하면 기존 연결 전용 모드이며 COM-03의 무업무 메시지 KeepAlive 검증에 사용한다. `init.cmd start`는 수신 옵션을 자동으로 넣는다.

## 검증과 증거

`verify`는 restore → 전체 solution build → 기능 추적기 검사 → 모든 .NET 테스트 프로젝트 실행 → TRX 검사 순으로 동작한다. 실패, 0건, 미실행, 결과 불일치는 성공으로 처리하지 않는다. 로그와 TRX는 `evidence/bootstrap/<실행ID>/`에 쌓이며 기존 기록은 지우지 않는다.

S010부터 복원·빌드는 작업자 하나와 재사용 비활성화로 실행한다. 현재 환경의 기본 병렬 복원 실패를 복구한 설정이며 테스트를 생략하지 않는다. COM-04의 [임시 수신 계약](src/Amr.Communication/Validation/CONTRACT.md)은 Telemetry/Alarm/Heartbeat/ACK의 Topic·JSON·필드·범위를 검증한다. 성공한 불변 모델만 Core 수신 인터페이스에 전달하며 실제 MQTT에서는 Heartbeat만 연결되어 있으며 다른 유형은 COM-01에서 수행한다. 기존 WPF와 TelemetryPayload는 이 검증 경로를 사용하지 않는다.

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
| 다음 행동은 무엇인가? | 진행 로그의 Next best action; 현재 COM-01 |
| 돌아갈 안전 기준선은? | 진행 로그의 기준선 커밋과 Git 이력 |

`AGENTS.md`는 작업 규칙, `feature_list.json`은 기능·설계·상태·증거, `claude-progress.md`는 현재 상태와 인수인계, `init.cmd/init.ps1/init.sh`는 시작·검증 경로를 담당한다. 기록된 이전 작업을 초기화하거나 진행 기능을 임의로 완료 처리하지 않는다.

## 구조와 남은 범위

| 경로 | 역할 |
| --- | --- |
| `src/Amr.Communication/` | Broker 연결·감시 및 내부 수신 메시지 검증; 실제 송수신·AMR별 큐는 후속 |
| `src/Amr.Communication.Console/` | Broker 연결과 상태 로그 |
| `src/Amr.TestClient/` | 가상 AMR Heartbeat 발행 실습 |
| `src/AmrControlSystem/`, `src/AmrControlTest/` | WPF 실험 앱 |
| `tests/Amr.Communication.Tests/` | 통신 제품 자동 테스트 |
| `tests/Amr.Verification.Tests/` | 추적기·TRX·테스트 프록시 검증 |
| `tools/Amr.Verification/` | .NET 개발 검증 도구 |
| `docker/` | 로컬 Mosquitto·Seq 설정 |
| `evidence/` | 성공·실패 실행 증거 |

S016에서 COM-04를 PDF 반영 계약 v2(revision 3)로 보완했다. 현재 검증 결과는 feature_list.json과 claude-progress.md를 따른다. COM-01은 Heartbeat만 구현되어 있으며 전체 기능은 미완료이다. 기존 WPF 의존성의 NU1701 경고, GUI의 직접 MQTT/SQLite 접근, 기존 MqttTopics의 cmd/cmd/ack/state와 추적기 계약 차이는 미해결이다. PDF v0.3 원문은 전달되어 계약 비교에 사용했다. S003/S004 원본 실행 증거는 여전히 없다. 실제 GUI·Core·AMR 이동, 운영 TLS/인증, 다른 Windows 노트북 실물 실행은 확인한 범위 이상으로 통과를 주장하지 않는다.

## COM-04 수신 검증 계약

[현재 계약 v2](src/Amr.Communication/Validation/CONTRACT.md)에 맞는 Telemetry·Alarm·Heartbeat·ACK만 Core 경계로 전달한다. 잘못된 입력은 실패 이유를 로그에 남기고 폐기하며 다음 정상 입력을 계속 처리한다.

- Telemetry: `pos{x,y}`와 `sp`, `state`를 분리한다. State는 `IDLE/BUSY/STOPPED/FAULT`.
- Alarm: `level`은 `INFO/WARN/ERR/FATAL`, `alarmState`는 개발용 `ACTIVE/CLEARED`. `message`는 선택 문자열.
- Heartbeat: `id`, `ts`, `status`가 필수이며 현재 `status`는 `OK`를 사용한다.
- `ts`는 기존 Unix 밀리초 Int64 계약을 유지한다. PDF 예시 숫자를 자릿수로 변환하지 않는다.

이전 `Pos/sp/stat` 형식과 `status` 없는 Heartbeat는 거부된다. 현재 실제 구독은 Heartbeat만이며 나머지 유형의 MQTT 연결·큐·발행은 COM-01에서 구현한다. 검증 테스트는 `tests/Amr.Communication.Tests/MessageValidationTests.cs`의 `PdfPageSixExamplesPreserveData`부터 읽는다.
