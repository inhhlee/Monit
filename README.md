# AmrControlSystem (practice)

가상 AMR 제어·통신·관제 시스템의 개발 저장소다. 현재 개발 범위는 MQTT 통신 모듈 `Amr.Communication`의 COM-01~05다. **COM-03: Broker 연결 및 연결 상태 감시**를 구현했으며 다음 작업은 **COM-04: 수신 메시지 유효성 검증**이다. 기존 GUI는 개발 실험용이며 Core·SQLite·GUI 제품 통합은 후속 범위다.

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

`-Start`는 검증 후 통신 콘솔을 실행한다. 기본 localhost:1883 Broker에 연결하고 Ctrl+C까지 유지하며 JSON 형태로 연결 상태·원인을 출력한다. 먼저 Broker를 실행해야 한다. 기본 KeepAlive는 5초, 연결/통신 timeout은 3초다. 연결 실패는 처리된 종료 코드 2, 연결 후 단절은 3, 설정 오류는 1, 정상 종료는 0이다. 자동 재접속과 업무 메시지 송수신은 아직 구현하지 않았다.

이미 빌드했다면 제한된 시간 동안 직접 실행할 수도 있다:

```powershell
dotnet run --project src/Amr.Communication.Console --no-build --no-restore -- --host 127.0.0.1 --port 1883 --keepalive-seconds 5 --timeout-seconds 3 --duration-seconds 15
```

환경변수 `AMR_MQTT_HOST`, `AMR_MQTT_PORT`, `AMR_MQTT_KEEPALIVE_SECONDS`, `AMR_MQTT_TIMEOUT_SECONDS`로도 설정한다. 선택적 인증은 `AMR_MQTT_USERNAME`, `AMR_MQTT_PASSWORD`로 주입하며 인증값은 로그에 출력하지 않는다. 현재 검증 범위는 로컬 TCP·익명 접속이며 운영 TLS/인증 환경은 별도 검증이 필요하다.

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

## 개발 서비스와 실제 COM-03 검증

기존 `docker/docker-compose.yml`에는 Mosquitto `2.1.2-alpine`과 Seq `2026.1`이 정의되어 있다. 개발 Broker는 localhost:1883, 익명 접속, Seq는 localhost:5341을 사용한다. 운영 설정으로 확정한 값이 아니다. Docker Desktop 엔진을 먼저 시작하고 Docker 실행 파일을 PATH에 추가한다. 이 워크스테이션의 실행 파일은 `C:/Users/Admin/AppData/Local/Programs/DockerDesktop/resources/bin/docker.exe`다.

```powershell
docker compose -f docker/docker-compose.yml up -d mosquitto
docker compose -f docker/docker-compose.yml logs --tail 100 mosquitto
docker compose -f docker/docker-compose.yml stop mosquitto
```

데이터는 기존 bind mount에 보존된다. 초기화 헬퍼는 컨테이너를 실행하거나 데이터를 지우지 않는다. COM-03의 자동 실증은 위 지속형 서비스와 분리된 임의 localhost 포트의 테스트 컨테이너를 사용한다. 최초 환경에서 `docker pull eclipse-mosquitto:2.1.2-alpine`로 이미지를 준비하고 다음을 실행한다:

```powershell
./init.ps1
python scripts/verify_com03.py
# Docker가 PATH에 없을 때:
python scripts/verify_com03.py --docker 'C:/Users/Admin/AppData/Local/Programs/DockerDesktop/resources/bin/docker.exe'
```

검증기는 현재 소스를 빌드한 뒤 실제 연결, 업무 메시지 없이 6초 유지(KeepAlive 2초), PINGREQ/PINGRESP, Broker 정상 종료, 응답 차단, 최초 접속 실패, 실행 프로그램 재시작, 자동 테스트를 검사한다. 응답 차단은 TCP 테스트 프록시가 Broker 응답만 버리는 방식이며 PC 방화벽을 변경하지 않는다. 실행마다 `evidence/COM-03/<실행ID>/result.json`, Broker/클라이언트 로그, TRX를 남기고 생성한 테스트 컨테이너만 정리한다. 기본 기준선만 통과했다고 실제 Broker 검증 통과로 판단하지 않는다.

`MqttConnectionManager`가 연결 상태와 수명주기를 관리하고 `MqttClientService`가 MQTTnet을 감싼다. 공개 API는 `ConnectAsync`, `DisconnectAsync`, `Snapshot`, `StateChanged`, `DisposeAsync`다. 연결 실패/취소는 false와 상태로 전달하며, 이미 종료된 관리자의 재사용은 예외다. 상태 구독자는 짧게 처리하고 콜백 안에서 수명주기 메서드를 동기 대기하지 않는다. 상태 시각은 Int64 Unix milliseconds이며 AMR Heartbeat 수신 시각과 별개다. 이전 싱글톤 골격은 연결별 소유 구조로 교체했다.

적용 결정은 feature_list.json의 DN-03/04에 기록했다. 구현 참고: [MQTTnet 공식 연결 예제](https://github.com/dotnet/MQTTnet/blob/master/Samples/Client/Client_Connection_Samples.cs), [Mosquitto 공식 문서](https://mosquitto.org/documentation/). 실제 검증에는 MQTTnet 5.2.0.1603과 Mosquitto 2.1.2를 사용했다.

## 구조와 미완성 범위

| 경로 | 역할 |
| --- | --- |
| `src/Amr.Communication/` | Broker 연결·감시 구현, 메시지 송수신·검증 등은 후속 |
| `src/Amr.Communication.Console/` | 설정 기반 Broker 연결·상태 로그·정상 종료 호스트 |
| `src/Amr.TestClient/` | 가상 AMR 테스트 클라이언트 골격 |
| `src/AmrControlSystem/`, `src/AmrControlTest/` | 기존 WPF 개발 실험 앱, 자동 테스트 프로젝트가 아님 |
| `tests/Amr.Communication.Tests/` | 연결 수명주기·취소·오래된 이벤트·자원 해제 및 기존 스모크 테스트 |
| `tests/harness/` | 추적기 및 테스트 실행 결과 검사기의 정상/거부 사례 |
| `scripts/` | 검증 도구 |
| `evidence/` | 실패와 성공을 포함한 실행 증거 |

COM-03은 기록된 개발 계약으로 실제 Mosquitto 검증을 수행했다. COM-04/01/02/05는 아직 not_started다. 실제 Core·GUI·AMR 이동 통합 검증과 구분한다. 기존 `MqttTopics`의 `cmd`, `cmd/ack`, `state`와 추적기 계약 사이 차이는 COM-01/04 작업에서 해결해야 한다. 현재 GUI가 MQTT/SQLite에 직접 접근하는 코드는 실험 코드이며 목표 아키텍처를 충족하지 않는다.

WPF 의존성의 NU1701 경고와 비어 있는 비동기 통신 메서드의 CS1998 경고는 기록한 미해결 사항이다. PDF 원문과 이전 S003/S004의 검사기·증거 파일은 전달되지 않았으며, 이전 문서에 적힌 검증은 이번 세션에서 재현된 사실과 구분한다.
