# AmrControlSystem (practice)

가상 AMR 제어·통신·관제 시스템의 개발 저장소다. 현재 개발 범위는 MQTT 통신 모듈 `Amr.Communication`의 COM-01~05이며 다음 작업은 **COM-03: Broker 연결 및 연결 상태 감시**다. 기존 GUI는 개발 실험용이며 Core·SQLite·GUI 제품 통합은 후속 범위다.

새 세션은 `claude-progress.md` → `AGENTS.md` → `feature_list.json` 순서로 읽는다. 프로젝트 기준 문서는 이 디렉터리에 있다. 상위 `repos`의 동명 문서는 이전 복사용 자료다.

## 준비 및 검증

- Windows, .NET SDK **10.0.401** (같은 feature band의 최신 패치 허용), PowerShell **7 이상**, Python **3.10 이상**, Git.
- 최초 NuGet 복원에는 패키지 소스 접근이 필요하다. GUI를 포함한 솔루션이므로 현재 표준 경로는 Windows 전용이다.
- Python이 PATH에 없으면 `-Python 'C:/path/to/python.exe'` 또는 `AMR_PYTHON`을 지정한다. 이 워크스테이션에서는 Codex 제공 Python을 자동 탐색한다. Windows Store 실행 별칭은 제외한다.

프로젝트 루트의 PowerShell 7에서:

```powershell
./init.ps1
./init.ps1 -Start
```

첫 명령은 환경 확인 → 추적기 검사 → 검사기 테스트 → `dotnet restore` → 전체 솔루션 `dotnet build --no-restore` → `tests/`의 .NET 테스트 → TRX 실행 건수 검사를 수행한다. 필수 단계 실패, 테스트 0건, 미실행 또는 실패 테스트가 있으면 0이 아닌 종료 코드를 반환한다. 매번 별도 `evidence/bootstrap/<실행ID>/`에 로그와 TRX를 기록한다. 실행한 검증 증거는 검토 후 관련 파일을 커밋한다.

`-Start`는 검증 후 통신 콘솔을 실행한다. **현재 콘솔은 `Hello, World!` 출력 후 종료되는 골격**이며 Broker 연결이나 운영 서비스 시작을 의미하지 않는다. COM-03에서 실행 프로그램을 구현하고 이 설명을 갱신한다.

Git for Windows Bash 5.3.15에서도 같은 파이프라인을 사용한다:

```bash
bash ./init.sh
bash ./init.sh -Start
```

`pwsh.exe`가 PATH에 없으면 `POWERSHELL_EXE`에 PowerShell 7 실행 파일 경로를 지정한다. WSL Bash는 검증 범위가 아니다. 두 헬퍼 모두 자신의 위치를 기준으로 실행하므로 다른 디렉터리에서도 절대 경로로 호출할 수 있다.

검사기만 실행하려면:

```powershell
python scripts/verify_harness.py
python -m unittest discover -s tests/harness -v
```

## 개발 서비스 (COM-03에서 실제 연동 검증 필요)

기존 `docker/docker-compose.yml`에는 Mosquitto `2.1.2-alpine`과 Seq `2026.1`이 정의되어 있다. 개발 Broker는 localhost:1883, 익명 접속, Seq는 localhost:5341을 사용한다. 운영 설정으로 확정한 값이 아니다. Docker CLI/daemon은 초기화 세션에서 사용할 수 없어 아래 서비스 명령은 아직 검증하지 않았다.

```powershell
docker compose -f docker/docker-compose.yml up -d mosquitto
docker compose -f docker/docker-compose.yml logs --tail 100 mosquitto
docker compose -f docker/docker-compose.yml stop mosquitto
```

데이터는 기존 bind mount에 보존된다. 초기화 헬퍼는 컨테이너를 실행하거나 데이터를 지우지 않는다. COM-03 시작 시 Docker Desktop/Compose 또는 실제 Mosquitto 실행 환경부터 확보하고 Broker 로그·KeepAlive·단절 감지를 검증한다.

## 구조와 미완성 범위

| 경로 | 역할 |
| --- | --- |
| `src/Amr.Communication/` | 통신 라이브러리 골격, 대부분 미구현 |
| `src/Amr.Communication.Console/` | 통신 실행 프로그램 골격 |
| `src/Amr.TestClient/` | 가상 AMR 테스트 클라이언트 골격 |
| `src/AmrControlSystem/`, `src/AmrControlTest/` | 기존 WPF 개발 실험 앱, 자동 테스트 프로젝트가 아님 |
| `tests/Amr.Communication.Tests/` | 기존 DTO의 Int64 JSON 보존·빈 AMR ID 거부 스모크 테스트 |
| `tests/harness/` | 추적기 및 테스트 실행 결과 검사기의 정상/거부 사례 |
| `scripts/` | 검증 도구 |
| `evidence/` | 실패와 성공을 포함한 실행 증거 |

모든 제품 기능은 `not_started`이며 제품 통과 증거는 없다. 스모크 테스트를 실제 MQTT 통신 검증으로 해석하지 않는다. 기존 `MqttTopics`의 `cmd`, `cmd/ack`, `state`와 추적기 계약 사이 차이는 COM-01/04 작업에서 해결해야 한다. 현재 GUI가 MQTT/SQLite에 직접 접근하는 코드는 실험 코드이며 목표 아키텍처를 충족하지 않는다.

WPF 의존성의 NU1701 경고와 비어 있는 비동기 통신 메서드의 CS1998 경고는 기록한 미해결 사항이다. PDF 원문과 이전 S003/S004의 검사기·증거 파일은 전달되지 않았으며, 이전 문서에 적힌 검증은 이번 세션에서 재현된 사실과 구분한다.
