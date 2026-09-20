# AmrControlSystem (practice)

가상 AMR 통신·관제 시스템의 개발 저장소다. 현재 구현된 제품 기능은 **COM-03: MQTT Broker 연결 및 연결 상태 감시**이며, 다음 기능은 **COM-04: 수신 메시지 유효성 검증**이다. 기존 WPF 화면은 실험용이고, Core·SQLite·GUI·실제 AMR 이동 통합은 후속 범위다.

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

`verify`에서 **BASELINE PASSED**, `start`에서 **Connected**가 나오면 기본 개발 준비와 Broker 연결에 성공한 것이다. 이후 출력 없이 대기하는 것은 정상이다. 종료는 Ctrl+C다. 관제 GUI나 로봇 이동을 실행하는 명령은 아니다.

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

추가 설정: `AMR_MQTT_KEEPALIVE_SECONDS`, `AMR_MQTT_TIMEOUT_SECONDS`, `AMR_MQTT_USERNAME`, `AMR_MQTT_PASSWORD`. 직접 콘솔을 실행하려면 `dotnet run --project src/Amr.Communication.Console`을 쓴다.

## 검증과 증거

`verify`는 restore → 전체 solution build → 기능 추적기 검사 → 모든 .NET 테스트 프로젝트 실행 → TRX 검사 순으로 동작한다. 실패, 0건, 미실행, 결과 불일치는 성공으로 처리하지 않는다. 로그와 TRX는 `evidence/bootstrap/<실행ID>/`에 쌓이며 기존 기록은 지우지 않는다.

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
| 다음 행동은 무엇인가? | 진행 로그의 Next best action; 현재 COM-04 |
| 돌아갈 안전 기준선은? | 진행 로그의 기준선 커밋과 Git 이력 |

`AGENTS.md`는 작업 규칙, `feature_list.json`은 기능·설계·상태·증거, `claude-progress.md`는 현재 상태와 인수인계, `init.cmd/init.ps1/init.sh`는 시작·검증 경로를 담당한다. 기록된 이전 작업을 초기화하거나 진행 기능을 임의로 완료 처리하지 않는다.

## 구조와 남은 범위

| 경로 | 역할 |
| --- | --- |
| `src/Amr.Communication/` | 연결·감시 구현; 메시지 검증·송수신은 후속 |
| `src/Amr.Communication.Console/` | Broker 연결과 상태 로그 |
| `src/Amr.TestClient/` | 가상 AMR 테스트 클라이언트 골격 |
| `src/AmrControlSystem/`, `src/AmrControlTest/` | WPF 실험 앱 |
| `tests/Amr.Communication.Tests/` | 통신 제품 자동 테스트 |
| `tests/Amr.Verification.Tests/` | 추적기·TRX·테스트 프록시 검증 |
| `tools/Amr.Verification/` | .NET 개발 검증 도구 |
| `docker/` | 로컬 Mosquitto·Seq 설정 |
| `evidence/` | 성공·실패 실행 증거 |

현재 COM-03만 passing이고 COM-04/01/02/05는 not_started다. 기존 WPF 의존성의 NU1701 경고, GUI의 직접 MQTT/SQLite 접근, 기존 Topic과 추적기 계약 차이는 미해결이다. PDF v0.3과 S003/S004 원본 증거는 전달되지 않았다. 실제 GUI·Core·AMR 이동, 운영 TLS/인증, 다른 Windows 노트북 실물 실행은 확인한 범위 이상으로 통과를 주장하지 않는다.
