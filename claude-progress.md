# AmrControlSystem Progress Log

새 세션은 이 파일을 먼저 읽고 `AGENTS.md`, `feature_list.json`과 관련 소스·테스트를 확인한다. 이 파일이 있는 AmrControlSystem이 실제 프로젝트 루트다. 상위 repos의 동명 파일은 과거 복사용 문서이며 현재 상태의 기준이 아니다.

## 현재 검증된 상태 (Current Verified State)

| Field | Verified value |
| --- | --- |
| Last updated | 2026-09-18, Asia/Seoul |
| Repository root directory | C:\Users\Admin\Desktop\repos\AmrControlSystem; S005에서 신규 Git 저장소 생성. |
| Standard startup path | PowerShell 7에서 `./init.ps1 -Start`; Git for Windows Bash에서 `bash ./init.sh -Start`. 현재 콘솔은 Hello World 출력 후 종료하는 골격. |
| Standard verification path | `./init.ps1`: 추적기 검사, Python 검사기 테스트, 전체 솔루션 restore/build, .NET tests, TRX 0건·실패·미실행 거부 검사. 전체 로그는 evidence/bootstrap/실행ID/. |
| Current scope | 과제 1: COM-01~05. 현재 기능별 검증 항목 35개. |
| Highest priority unfinished feature | COM-03: MQTT Broker 연결 및 연결 상태 감시 |
| Active feature | 없음. 모든 기능은 not_started. |
| Verified product behavior | 전체 솔루션 빌드, 기존 DTO/빈 ID 거부 스모크 4건, 검사기 테스트 8건 통과. COM 기능 검증 및 실제 MQTT 동작은 미실행. |
| Current blocker | 초기화 기준선에는 없음. Docker CLI/daemon 및 Mosquitto 실행 환경 미확보로 실제 Broker 검증은 미실행. COM-03의 첫 작업에서 확보·확인할 것. |
| Requirements baseline | PDF v0.3을 출발점으로 사용한다. 현재 적용 범위와 검증은 feature_list.json, 설계 쟁점은 design_notes에서 관리한다. |
| Harness verification | S005에서 누락된 검사기를 새로 작성하고 검사기 테스트 8건 통과. S003/S004가 언급한 증거 파일은 현재 전달받지 못했다. 과거 기록은 보존하되 현재 증거로 사용하지 않는다. |
| Working rules | 루트 AGENTS.md 적용. 초기화 세션에서 COM 기능은 시작하지 않았으며 5개 모두 not_started, revision 1, evidence 빈 배열 유지. |
| Environment | Windows 10.0.26200, .NET SDK 10.0.401/runtime 10.0.12, PowerShell 7.6.5, Git Bash 5.3.15, Python 3.12.14. |
| Python executable | C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe. 새 환경은 PATH의 실제 Python 또는 init.ps1 -Python 경로 사용. |
| Initial baseline commit | 58a53008c02cde8765e37a23375cf54c35f7dc85. 이 커밋만 복제한 새 체크아웃에서도 빌드·테스트·콘솔 시작 통과. |
| Git remote / branch | origin = https://github.com/indsf/practice.git, master → origin/master. S006에서 사용자 지정 저장소로 업로드. |

## 세션 레코드 (Session Record)

### S001 - 2026-09-18 - 최초 검토 (이전 대화에서 소급 기록)

- Goal: 사용자가 작성한 초기 feature_list와 PDF의 일치 여부 검토.
- Completed: 누락된 연결 감시·유효성 검증·Core 책임을 지적하고 PDF의 로그 정책 및 미등록 Telemetry 처리 충돌을 확인했다.
- Verification run: PDF 텍스트 추출 및 관련 페이지 시각 검토. 제품 테스트는 실행하지 않았다.
- Evidence recorded: 원문 PDF 6·7·9·16페이지 및 이전 대화의 검토 결과. 별도 제품 증거 파일은 없음.
- Commits: 없음.
- Known risks: PDF가 미완성이며 세부 정책이 서로 다르거나 미확정이다.
- Next best action: 과제 1에 한정한 기능 추적기를 작성한다.

### S002 - 2026-09-18 - 과제 1 추적기 작성 (이전 대화에서 소급 기록)

- Goal: 과제 1 COM-01~05의 feature_list.json 작성.
- Completed: 5개 기능과 35개 검증 항목, 구현 순서, 의존성 및 설계 쟁점 6개를 작성했다.
- Verification run: Python으로 JSON 파싱, 5개 ID, 우선순위, 의존성 및 초기 상태를 검사했다. 제품 테스트는 실행하지 않았다.
- Evidence recorded: feature_list.json 및 이전 대화의 검사 출력. 별도 실행 로그 파일은 남기지 않았다.
- Commits: 없음.
- Known risks: 실제 소스와 실행 명령이 미확인이다. 설계 확정을 일괄 선행 조건으로 해석하면 구현이 불필요하게 지연될 수 있다.
- Next best action: 미완성 명세 변경 규칙과 세션 인수인계를 추가한다.

### S003 - 2026-09-18 - 하네스 규칙 및 명세 변경 흐름 정리

- Goal: PDF를 구현 중 수정할 수 있는 작업 규칙과 검증·진행 기록을 마련한다.
- Completed: AGENTS.md, 이 진행 로그 및 scripts/verify_harness.py를 작성했다. feature_list.json에 결정 정책, 변경 이력, 기능별 spec_revision·blocker를 추가했다. 기존 5개 기능과 35개 검증 항목, 미착수 상태는 유지했다.
- Verification run: 2026-09-18 17:15:29 +09:00에 추적기 구조 검사를 실행해 종료 코드 0을 확인했다. 합성 증거 정상 사례, 잘못된 상태·증거·의존성 8개 거부 사례, 잘못된 JSON의 CLI 종료 코드 1을 확인했다. 실제 실행 경로와 결과는 evidence/harness-validation.txt에 기록했다. MQTT/제품 테스트는 미실행이다.
- Evidence recorded: evidence/harness-validation.txt. 기능별 evidence는 계속 빈 배열이다.
- Commits: 없음. 문서 산출물을 실제 저장소에 설치하거나 커밋하지 않았다.
- Known risks: 문서 규칙만으로 실행 결과의 진실성을 보장할 수 없다. 검사기는 증거 내용의 실제 정확성, 제품 동작, 변경된 코드가 증거에 미치는 영향을 자동 판정하지 못한다. 에이전트의 시작 지침에 이 규칙과 진행 로그의 읽기 순서를 연결해야 한다.
- Next best action: 실제 저장소 경로·소스를 확인하고 파일을 루트에 배치한다. 기존 규칙 파일이 있으면 내용을 병합한다. 실행·테스트 명령을 확인해 Current Verified State를 갱신한 뒤 COM-03 하나를 in_progress로 변경하고 구현한다.

### S004 - 2026-09-18 - 두 작업 규칙 문서 통합

- Goal: 사용자가 제시한 개발·아키텍처 규칙과 기존 인수인계·명세 변경 규칙을 하나의 AGENTS.md로 통합한다.
- Completed: 시작 절차, 기준선 복구, 폴더·아키텍처 제약, 단일 기능 작업, 명세 변경, 검증 증거, 완료 정의, 종료·커밋 절차를 통합했다. init.sh와 Windows 실행 환경의 확인·구축 조건을 명시했다. 기존 기능 추적기의 상태와 제품 증거는 변경하지 않았다.
- Verification run: scripts/verify_harness.py가 종료 코드 0으로 통과했다. 통합 문서의 10개 절과 주요 규칙 포함 여부, Markdown 전달 과정의 불필요한 이스케이프 부재를 확인하고 문서의 절차·범위 일관성을 검토했다. 제품 빌드·MQTT·재시작 검증은 문서 통합 범위이므로 실행하지 않았다.
- Evidence recorded: evidence/harness-validation-S004.txt. S003의 검사 로그는 보존했다.
- Commits: 없음. 실제 AmrControlSystem 저장소가 확인되지 않아 복사용 문서만 수정했다.
- Known risks: 실제 솔루션·SDK·Bash 또는 PowerShell 환경을 확인해야 init.sh나 동등 진입점을 구현·검증할 수 있다. 현재 규칙 문서만으로 제품 재시작 가능성을 주장할 수 없다.
- Next best action: 실제 프로젝트 루트에 통합 규칙과 추적기·진행 로그·검사기를 배치하고 기존 규칙이 있으면 병합한다. 저장소·실행 경로를 확인해 진행 로그를 갱신하고 기준선을 준비한 뒤 COM-03을 시작한다.

### S005 - 2026-09-18 - 실제 프로젝트 초기화 및 기준선 복구

- Goal: 초기화 플레이북에 따라 이후 세션이 시작·검증·작업 경계를 재도출하지 않아도 되는 운영 환경과 안전 기준선 커밋 마련.
- Completed: 실제 프로젝트 루트 확인, 기존 5개 프로젝트 보존, 문서 배치, 신규 Git 저장소(main) 구성. 빈 클래스 5개의 잘못된 MqttSubscriber 이름을 파일 책임에 맞게 분리하고 기존 WPF 실험 서비스의 MQTTnet 4 API를 설치된 5.2.0.1603 API로 수정하여 빌드 복구. init.ps1 및 Git Bash 래퍼 init.sh, SDK 기준 global.json, 자동 테스트 프로젝트와 추적기/TRX 검사기, README 및 제외 규칙 추가. 사용자 DB·Broker/Seq 데이터·IDE 상태는 삭제하거나 커밋하지 않는다. COM 기능은 모두 not_started 유지.
- Verification run: 최초 restore는 샌드박스 NuGet.Config 접근 제한으로 실패한 뒤 승인된 재시도에서 성공. 최초 build는 중복 클래스 및 MQTTnet API 오류 10건으로 실패했다. 첫 헬퍼 실행은 Windows Store Python 별칭 때문에 종료 코드 1; 실제 Python을 선택하도록 복구했다. 18:13:55+09:00 PowerShell 전체 기준선 및 콘솔 시작 통과(.NET 4건, Python 8건). Git Bash 재시작은 새 .git의 샌드박스 계정 소유권으로 종료 코드 1; .git 소유자만 실제 Admin으로 복구하고 전역 신뢰 설정은 변경하지 않았다. 18:16:01+09:00 상위 repos에서 Git Bash 절대 경로 호출로 전체 기준선 및 콘솔 재시작 통과.
- Evidence recorded: evidence/initialization/S005/README.md의 실행별 목록. 실패 로그와 재시도 로그 모두 보존. 기능별 evidence 배열은 비어 있으며 초기화 결과를 제품 기능 통과 증거로 등록하지 않았다.
- Commits: `58a53008c02cde8765e37a23375cf54c35f7dc85` — Initialize AMR scaffold with reproducible build and verification workflow. 검증 증거 및 인수인계 갱신은 별도 문서 커밋으로 남긴다(최신 해시는 git log -1로 확인).
- Known risks: WPF의 OpenTK/SkiaSharp NU1701 호환성 경고. 통신 메서드는 빈 골격이며 GUI가 MQTT/SQLite에 직접 접근하는 실험 코드는 목표 아키텍처 미충족. MqttTopics의 cmd/cmd/ack/state와 현재 추적기 계약 차이를 COM-01/04에서 검토해야 한다. Docker CLI 및 실제 Broker 실행 환경, WPF GUI 실행, 실제 MQTT, Core/AMR 전체 통합은 미검증. PDF 및 S003/S004 원본 검사기·증거는 전달되지 않아 해당 과거 세션 기록을 재검증된 사실로 간주하지 않는다.
- Next best action: **COM-03**. 먼저 claude-progress.md, feature_list.json의 COM-03/DN-03/DN-04, src/Amr.Communication/Mqtt/MqttClientService.cs 및 MqttConnectionManager.cs를 읽고 `./init.ps1` 실행. 실제 Mosquitto 환경을 확보한 후 개발용 Broker/KeepAlive/단절 감지 설정만 임시 결정으로 기록하고 COM-03 하나를 in_progress로 바꾼다. 실제 연결·KeepAlive·정상 종료/응답 차단·최초 연결 실패 검증 전체와 증거를 구현한다.

S005 종료 검증: 2026-09-18T18:19:00+09:00에 기준선 커밋만 `.tools/S005-clean-checkout`으로 복제하여 기존 bin/obj 없이 `init.ps1 -Start` 실행. restore/build, Python 8건, .NET 4건, 콘솔 시작 모두 통과했다. 이 검증은 같은 머신의 SDK/NuGet 캐시를 사용했으며 새 머신 설치 또는 실제 Broker 연동 검증을 의미하지 않는다. 증거는 evidence/initialization/S005/clean-checkout/ 및 clean-checkout-run.log. 검증용 복제본은 .tools 아래 제외된 로컬 자료로 보존했다.

### S006 - 2026-09-18 - 사용자 지정 GitHub 저장소에 초기 업로드

- Goal: 업로드할 파일과 제외 대상을 확인하고 사용자가 지정한 indsf/practice 저장소에 프로젝트를 전송한다.
- Completed: 추적 파일 및 제외 상태 확인. 소스·솔루션·문서·테스트·설정·검증 기록을 포함하고 bin/obj, .vs, .tools, 사용자 IDE 설정, 로컬 DB 및 Broker/Seq 실행 데이터를 제외했다. 원격의 기존 master에는 README 제목 practice만 있었으며, 해당 초기 이력을 보존해 병합했다. README 제목에 저장소 이름을 반영하고 로컬 브랜치를 master로 맞췄다. git push -u origin master 성공.
- Verification run: git ls-remote/fetch로 기존 이력 확인, README 충돌 해결 후 git diff --cached --check 및 Python scripts/verify_harness.py 통과. git push가 0d71d33..9c75afc의 정상 갱신을 보고했다. 제품 코드는 변경하지 않았으므로 빌드·MQTT 테스트는 반복하지 않았다.
- Evidence recorded: evidence/repository/S006/upload.txt. COM 기능 상태·revision·evidence는 변경하지 않았다.
- Commits: 병합 및 최초 업로드 9c75afcf190c501a8418367c7cf5f6dcbf80bcc7. 본 인수인계 기록은 별도 문서 커밋으로 보존한다.
- Known risks: 제품의 미구현·미검증 범위는 S005와 동일. 업로드는 제품 기능 통과를 의미하지 않는다.
- Next best action: COM-03. 프로젝트 루트에서 ./init.ps1로 기준선을 확인하고 실제 Mosquitto 환경과 연결 감시 구현을 진행한다. 이후 변경은 master에서 커밋하고 git push로 같은 저장소에 전송한다.

## 다음 세션 작성 양식

### S007 - YYYY-MM-DD - 작업 제목

- Goal:
- Completed:
- Verification run:
- Evidence recorded:
- Commits:
- Known risks:
- Next best action:

위 S007은 미작성 양식이며 실제 수행된 세션이 아니다.
