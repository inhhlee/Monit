# S005 초기화 증거

이 증거는 COM 기능 verification 통과 근거가 아니다. 코드 상태는 기존 소스를 포함한 최초 커밋 전 변경 상태이며 실제 기준선 커밋 해시는 claude-progress.md S005에 기록한다. 로컬 환경: Windows 10.0.26200 / .NET SDK 10.0.401 / runtime 10.0.12 / PowerShell 7.6.5 / Python 3.12.14 / Git Bash 5.3.15. 패키지: MQTTnet 5.2.0.1603(기존 GUI), xUnit 2.9.3, VSTest SDK 17.14.1. Broker 및 Core/AMR 대역은 실행하지 않았다.

| 실행 | 명령/절차 | 기대 / 실제 / 결과 | 저장소 상대 경로 |
| --- | --- | --- | --- |
| 최초 빌드 | 프로젝트 루트에서 dotnet build AmrControlSystem.slnx --no-restore | 기대: 빌드 가능. 실제: 중복 타입 및 MQTTnet 네임스페이스 오류 10건, exit 1. failed | evidence/initialization/S005/baseline-before.log |
| 2026-09-18T18:12:47+09:00 | ./init.ps1 -Start | 기대: 기준선+콘솔 시작. 실제: Store Python 별칭 exit 9009, 헬퍼 exit 1. failed | evidence/bootstrap/20260918T181247434+0900-d0b54f/ |
| 2026-09-18T18:13:55+09:00 | ./init.ps1 -Start | 기대: 모든 단계 및 콘솔 시작. 실제: 빌드 오류 0, .NET 4/4, Python 8/8, Hello World 출력/정상 종료, exit 0. passed | evidence/bootstrap/20260918T181355749+0900-f29b72/ |
| 2026-09-18T18:15:09+09:00 | repos에서 Git Bash로 프로젝트의 init.sh -Start 절대 경로 실행 | 기대: 다른 디렉터리에서 재시작. 실제: 신규 .git 소유권 불일치 git exit 128, 헬퍼 exit 1. failed | evidence/bootstrap/20260918T181509341+0900-c27470/ 및 evidence/initialization/S005/bash-restart.log |
| 2026-09-18T18:16:01+09:00 | .git 소유자 복구 후 동일 Bash 호출 | 기대: 전체 기준선+콘솔 재시작. 실제: .NET 4/4, Python 8/8, 콘솔 정상 종료, exit 0. passed | evidence/bootstrap/20260918T181601056+0900-3eadf1/ 및 evidence/initialization/S005/bash-restart-retry.log |
| 2026-09-18T18:19:00+09:00 | git clone --no-hardlinks . .tools/S005-clean-checkout 후 해당 init.ps1 -Start | 기대: 커밋된 소스만으로 재현. 실제: 58a53008c02cde8765e37a23375cf54c35f7dc85에서 bin/obj 없이 전체 빌드·Python 8/8·.NET 4/4·콘솔 종료 통과, exit 0. passed | evidence/initialization/S005/clean-checkout/ 및 evidence/initialization/S005/clean-checkout-run.log |

각 bootstrap 디렉터리의 summary.log에는 시작·종료 시각과 단계별 실행 명령/결과가 있다. 개별 로그 및 TRX로 실제 테스트 수를 확인한다. 최초 NuGet.Config 접근 제한 및 승인 재시도는 S005 세션 기록에 있으며 그 두 호출의 독립 로그 파일은 저장하지 않았다.

clean-checkout/는 검증용 복제본에서 생성한 실행 로그를 그대로 복사한 디렉터리다. 로그 내부 절대 경로는 원래 실행 위치를 가리킨다. 같은 머신의 SDK 및 NuGet 패키지 캐시를 사용했으므로 새 머신 설치 검증과 구분한다.

검사기 테스트에는 잘못된 JSON, 상태/의존성/동시 작업, 누락·경로 이탈 증거, 이전 revision, 최신 실패, 시간대 누락 및 TRX 0건/실패/미실행 거부 사례가 포함된다. 합성 입력은 TemporaryDirectory에서만 생성하고 제품 evidence로 사용하지 않는다. 검사기는 증거 내용의 진실성 및 제품 동작까지 검증하지 않는다.
