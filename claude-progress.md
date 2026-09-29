# AmrControlSystem Progress Log

새 세션은 이 파일을 먼저 읽고 `AGENTS.md`, `feature_list.json`과 관련 소스·테스트를 확인한다. 이 파일과 AmrControlSystem.slnx가 있는 폴더가 실제 프로젝트 루트다. PC별 경로와 폴더 이름은 고정하지 않는다. 과거 세션의 PC 경로·명령은 당시 기록이며 현재 실행 경로는 아래 표와 README를 따른다.

## 현재 검증된 상태 (Current Verified State)

| Field | Verified value |
| --- | --- |
| Last updated | 2026-09-29, Asia/Seoul |
| Repository root directory | 이 파일이 있는 디렉터리(.). S009 작업 경로는 C:\practice; 새 PC에서는 clone한 위치를 사용한다. |
| Standard startup path | ./init.cmd broker 후 ./init.cmd start는 연결만 확인. 기본 송수신 실습은 ./init.cmd start -Heartbeat와 별도 Amr.TestClient 실행. Windows PowerShell 5.1에서도 cmd가 PowerShell 7.4+ 호출. Ctrl+C 종료. |
| Standard verification path | ./init.cmd verify: restore/build, .NET 추적기 검사·전체 자동 테스트·TRX 검사. 실제 Broker 검증은 ./init.cmd verify-broker. Python 불필요. 로그는 evidence/bootstrap/실행ID/. |
| Current scope | 과제 1: COM-01~05. 현재 기능별 검증 항목 35개. |
| Highest priority unfinished feature | COM-01: 명령 검증, /command Topic 정합, AMR별 독립 처리 보완. |
| Active feature | COM-01 in_progress. S018 실제 실행에서 verification 5/6/7 실패하여 S017의 근거 부족 passing 정정. COM-03/04 passing, COM-02/05 not_started. |
| Verified product behavior | S018: 2 AMR × Heartbeat/Telemetry/Alarm/ACK 수신·검사, 비정상 입력 거부 후 정상 처리, 기본 Command/ACK 왕복 동작. 실제 Broker 연결·KeepAlive·끊김 감지 통과. 명령 발행 전 검증·AMR별 독립 처리 실패, /cmd와 /command 불일치. 고정 cmdId와 잘못된 명령의 SUCCESS 응답 확인. 전체 COM-01 통과 아님. |
| Current blocker | 외부 blocker 없음. COM-01 수정 가능한 결함은 in_progress로 관리한다. 자동 재접속과 개별 AMR Heartbeat Timeout은 미구현이며 정상 송수신만으로 완료 처리하지 않는다. |
| Requirements baseline | PDF v0.3을 출발점으로 사용한다. 현재 적용 범위와 검증은 feature_list.json, 설계 쟁점은 design_notes에서 관리한다. |
| Harness verification | S018 표준 verify-broker: restore/build/tracker/343 자동 테스트/TRX 통과, COM-03 실제 Mosquitto 5항목 통과. 시작 시 NuGet 접근 제한과 missing artifacts 실패도 보존했다. 사용자 init.ps1 로컬 수정이 있는 상태의 검증이며 클린 복제본/다른 PC/Git Bash는 이번에 미실행. |
| Working rules | AGENTS.md 적용. DN-01 오류 로그 유지; DN-02 PDF 반영 계약 v2, COM-04 revision 3. DN-07 Heartbeat status=OK 필수. 시간 단위·세부 enum 미정값은 provisional. |
| Environment | S018 Windows 10.0.26200, SDK 10.0.401, PowerShell 7.6.5, Git 2.41.0, MQTTnet 5.2.0.1603, Docker 29.7.2/Mosquitto 2.1.2-alpine. 기본 송수신 localhost:1883, QoS0/Retain false. COM-03은 격리된 임시 포트. WPF NU1701 경고 유지. |
| Tool dependencies | Windows, Git, global.json의 .NET SDK, PowerShell 7.4+. Docker Desktop은 Broker 사용 시 필요. Python과 Codex 런타임에 의존하지 않는다. |
| Current Windows baseline commit | 6746d44df66b3d3dea11bec13f8896254ed757f2 (S016 COM-04 revision 3, 자동343건 및 실제 Heartbeat 회귀). S018 점검 코드는 HEAD 9160f144fe6d351d2bd1753b516c8bf8dd7f6a8f + 사용자 init.ps1 로컬 수정이며 COM-01 결함이 있어 전체 완료 기준선으로 삼지 않는다. 사용자 수정은 점검 커밋에서 제외. 이전 S010 eb296f376c04d441e5d067a1cb8e060ccb199eac, S009 cc356d980a94b9e3a76b546250e34e3533ce827f. |
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

### S007 - 2026-09-18 - COM-03 연결 수명주기 구현

- Goal: COM-03 Broker 연결, KeepAlive 유지, 정상 종료/응답 차단 감지 및 최초 연결 실패를 구현·검증한다.
- Completed: 설정 기반 MqttClientService와 MqttConnectionManager 구현. 연결/실패/단절/정상 종료 상태 및 Int64 Unix milliseconds 로그, 제한 시간, 취소, 명시적 재시도, 이전 연결 이벤트 무시, 자원 해제, 관측자 오류 격리를 추가했다. 싱글톤 골격을 연결별 소유 구조로 변경했다. 콘솔은 실제 Broker에 연결하고 JSON 상태·이유 및 종료 코드(정상 0/설정 오류 1/접속 실패 2/연결 후 단절 3)를 제공한다. 자동 재접속·업무 메시지·AMR Heartbeat는 추가하지 않았다. DN-03/04를 부분 임시 채택하고 COM-03 revision 2의 5개 검증 증거를 기록하여 passing으로 전환했다.
- Verification run: 시작 시 init.ps1는 NuGet.Config 접근 제한으로 실패(기존 Python 8건은 통과). 전체 헬퍼 권한 요청 거부 후 좁힌 restore가 승인되어 성공했고 초기 build 및 기존 .NET 4건이 통과했다. 사용자 폴더에서 Docker Desktop을 발견하고 엔진을 시작했다. 개발 중 단위 테스트 20건 통과. 19:15:48+09:00 실제 Mosquitto 자동 시나리오에서 연결/6초 idle 및 PINGREQ/PINGRESP/정상 종료 0.063초/응답 차단 3.078초/최초 연결 실패/프로세스 재시작 전부 통과했다. 응답 차단은 TCP 프록시에서 Broker→Client 방향만 폐기했으며 이후 직접 접속으로 Broker 생존도 확인했다. 19:18:40+09:00 최종 ./init.ps1의 restore/build/.NET 20건/Python 8건 통과. 기존 WPF NU1701 경고 6건 외 신규 경고 없음.
- Evidence recorded: evidence/COM-03/20260918T191548-3fbb94/result.json 및 연결/Broker 로그·TRX·타이밍·소스 SHA-256. 최종 기준선 evidence/bootstrap/20260918T191840700+0900-54e1a4/. 최초 권한 실패 evidence/bootstrap/20260918T185931831+0900-973bc4/와 S007-baseline/S007-development 기록도 보존했다. Core/AMR 대역은 사용하지 않았으며 단위 테스트에서만 IBrokerClient 대역 사용.
- Commits: `c3454467bf46064e3aa86adfa11affefcd0b8c12` — Implement COM-03 broker lifecycle with real Mosquitto verification. 인수인계 커밋 `a762a39`까지 사용자 요청에 따라 `git push origin master`로 GitHub 업로드 완료(원격 9c6802e → a762a39).
- Known risks: 적용 값은 로컬 개발용이며 운영 TLS/인증과 지연 환경은 별도 검증 필요. 연결 상태 콜백은 짧게 처리하고 수명주기 메서드를 동기 대기하지 않는다. MQTTnet 단절 reason enum만으로 정상/비정상을 판단하지 않고 상태 원인 ConnectionLost 및 Exception을 함께 본다. 테스트용 컨테이너만 정리하며 기존 서비스·데이터는 보존했다. COM-01/04의 기존 Topic·Payload 계약 차이, WPF NU1701, GUI의 실험용 직접 DB/MQTT 접근은 후속이다.
- Next best action: **COM-04**. feature_list.json의 COM-04/DN-01/DN-02와 src/Amr.Communication/Validation/ 및 Models/를 읽고 ./init.ps1 실행. 필수 필드 누락 로그 및 현재 사용할 메시지 계약을 기록한 뒤 COM-04 하나를 in_progress로 전환하고 정상/비정상 수신 유효성 검증 구현. COM-03 회귀가 필요하면 python scripts/verify_com03.py --docker <실제 Docker 실행 파일> 실행.

## 다음 세션 작성 양식

### S008 - 2026-09-20 - PowerShell 한글 출력 인코딩 수정

- Goal: Windows PowerShell 5.1에서 PowerShell 7 실행 후 발생한 한글 출력 깨짐을 해결한다.
- Completed: 현재 체크아웃 C:\practice의 기존 로그에서 깨진 한글 경고를 확인했다. init.ps1에서 콘솔 입출력과 PowerShell 파이프 인코딩을 UTF-8로 일치시키고 Tee-Object 로그 인코딩도 명시했다. README에 Windows PowerShell 5.1에서 pwsh로 실행하는 명령을 추가했다. 기존 솔루션 src 배치 수정과 미추적 검증 폴더는 보존했다. 제품 기능 상태·revision·증거는 변경하지 않았다.
- Verification run: 시작 기준선은 샌드박스 NuGet 소켓 접근 제한으로 restore 실패; 승인된 전체 재실행은 통과. 수정 후 입력·출력·파이프를 CP949로 설정한 PowerShell 7.6.5에서 ./init.ps1 -Python C:\msys64\mingw64\bin\python.exe 실행, restore/build/.NET 20건/Python 8건 및 TRX 검사 통과. 저장된 UTF-8 build.log에서 '프로젝트 대상 프레임워크' 문자열과 대체 문자 부재를 확인했다. git diff --check 통과. 실제 MQTT와 사용자 터미널의 글꼴 표시는 이번 범위에서 미검증이다.
- Evidence recorded: evidence/bootstrap/20260920T182911700+0900-37eddc/ (샌드박스 실패), evidence/bootstrap/20260920T182948643+0900-807927/ (수정 전 기준선), evidence/bootstrap/20260920T183043149+0900-80b621/ (CP949 환경에서 수정 후 기준선 및 한글 로그).
- Commits: 없음. 앞선 솔루션 수정의 커밋 권한 요청이 사용자에게 거절되어 이번에는 커밋을 재요청하지 않았다. 변경은 작업 폴더에 보존한다.
- Known risks: 이미 깨져 저장된 이전 로그는 복원하지 않는다. 기존 WPF NU1701 경고 6건은 그대로다. 현재 검증은 .NET SDK 10.0.401, PowerShell 7.6.5, Python 3.12.11 환경이며 제품 COM-03 통과 범위를 확대하지 않는다.
- Next best action: 기존 실행이 남아 있으면 Ctrl+C로 종료한 뒤 pwsh -NoProfile -File "C:\practice\init.ps1" -Start로 새 출력을 확인한다. 기능 개발 재개는 COM-04이며 feature_list.json 및 Validation/Models 확인 후 ./init.ps1 실행부터 시작한다.

### S009 - 2026-09-20 - Windows 재현 기준선 및 Python 제거

- Goal: 초기화 플레이북에 맞춰 다른 Windows 노트북·다른 경로에서 사용 가능한 기준선 구축. 사용자 확인에 따라 scripts 제거 및 필요한 검증의 .NET/PowerShell 이관.
- Completed: init.cmd check/verify/broker/start/verify-broker, 공통 PowerShell 헬퍼, .NET 추적기·TRX·실제 Mosquitto 검사, 검증 도구 테스트 37건 추가. Python scripts 및 Python 테스트 제거. README와 AGENTS의 현재 절차를 새 경로로 정리했다. 기존 제품 코드·기능 status·spec_revision은 유지한다.
- Verification run: 시작 기준선 evidence/bootstrap/20260920T184455642+0900-8b371d 통과(.NET 20건/Python 8건). 이관 도구 테스트 37건 통과. 실제 Mosquitto COM-03 5항목·제품 테스트 20건·프로세스 재시작 통과. 정상 종료 0.203초, 응답 차단 3.096초, idle 6.010초. 새 init.cmd verify-broker 전체 파이프라인 통과(총 57건 및 실제 Broker 5항목). Windows PowerShell 5.1 호출·PowerShell 누락 종료 코드 2·잘못된 JSON 종료 코드 1 확인. 최초 Bash check에서 dirname 미발견 경고가 있어 내장 경로 계산으로 수정 후 C:/에서 절대 경로 호출 통과. C:\practice\.tools\S009 clean 한글의 bin/obj 없는 소스 복제본을 C:/에서 절대 경로로 실행했다. PATH에서 Python을 제거하고 전체 빌드/57건 테스트 및 2초 Broker 연결·정상 종료 통과. 같은 PC의 SDK/NuGet 캐시를 사용했고 PowerShell 경로는 POWERSHELL_EXE로 명시했다. 다른 실물 노트북 검증은 아니다.
- Evidence recorded: evidence/initialization/S009/development/migration.trx; evidence/COM-03/20260920T185653-164f13bba34041e69e2102a1e397de72/result.json. 최종 인덱스 evidence/initialization/S009/README.md. 새 전체 진입점 evidence/bootstrap/20260920T190439753+0900-acf92c, 최신 COM-03 evidence/COM-03/20260920T190458-d91d57424d8e4961a95bc777a05436c7, clean-verify/ 및 clean-start/에 로그·TRX 보존.
- Commits: 안전 기준선 커밋: cc356d980a94b9e3a76b546250e34e3533ce827f (Create portable Windows baseline with .NET verification tools). 이전 S008 미커밋 수정 중 관련 솔루션 배치·UTF-8 설정·문서를 포함하며, 사용자의 기존 실행 로그 3개는 미추적으로 보존한다.
- Known risks: 새 Windows 노트북 실물 검증은 수행하지 않았다. 기존 WPF NU1701·미완성 제품 범위 유지. 과거 증거 속 Python 명령과 PC별 경로는 역사 기록으로 보존한다. 사용 한도 중단 시 이 레코드에서 이어간다.
- Next best action: COM-04 재개. claude-progress.md → AGENTS.md → feature_list.json의 COM-04/DN-01/DN-02 → src/Amr.Communication/Validation 및 Models를 읽고 ./init.cmd verify 실행. 실제 다른 노트북에서는 README의 준비 도구를 설치하고 check → verify → broker → start로 재현한다. 원격 업로드는 이번 작업에서 수행하지 않는다.

### S010 - 2026-09-21 - COM-04 수신 메시지 검증

- Goal: 내부 테스트용 수신 계약과 오류 로그 정책을 기록하고 COM-04 검증기 및 Core 전달 경계를 구현·검증한다.
- Completed: 지정 순서로 인수인계·규칙·추적기와 소스 확인. 시작 HEAD는 f28c407936a0fa802a8d8b3f5b152b7bcdb21511. COM-04 하나만 진행해 DN-01/02 임시 계약, 불변 메시지 모델, Topic/JSON/필수 필드/자료형/범위 검증, Core 전달·실패 로그 인터페이스 및 244개 검증 사례 구현. 잘못된 Unicode escape 예외 누출도 수정했다. COM-04 revision 2의 8항목 완료 후 passing. 표준 helper의 restore/build를 작업자 하나·재사용 비활성화로 제한해 기준선 복구. README 및 추적기·인수인계 갱신. 사용자 global.json 빈 줄 변경과 기존 실행 로그는 보존했다.
- Verification run: 최초 init.cmd 및 승인된 Git Bash 실행은 SDK 부재로 실패. 재개 후 SDK 인식; 21:31 restore 실패(상세 로그 미생성), 전체 권한 재실행 요청은 거절되어 실행하지 않았다. 중단 사이의 21:37 표준 verify 로그·TRX에서 제품 20건/도구 37건 통과를 검사해 시작 기준선으로 사용했다. 개발 테스트 259건 통과 후 Unicode 추가 검사 2통과/3실패, 수정 후 264건 통과. 최종 기본 helper 복원 2회 실패; DOTNET_PROCESSOR_COUNT 제한도 실패, 직렬/no-reuse 복원 성공. init.ps1에 그 설정을 적용한 21:52 표준 init.cmd verify는 환경변수 덮어쓰기 없이 restore/build/제품 264건/도구 37건/추적기/TRX 전체 통과. 기존 WPF NU1701 6건 외 신규 빌드 경고 없음. 최종 pipeline의 새 테스트 프로세스에서 재실행 통과했으며 실제 애플리케이션 MQTT 수신 재시작은 COM-01 후속이다.
- Evidence recorded: evidence/COM-04/20260921T215259-S010/README.md, result.json, source-sha256.json 및 starting-baseline/ 복사본. 개발 성공·Unicode 실패·수정 결과는 evidence/COM-04/S010-development/. 최종 전체 기준선 evidence/bootstrap/20260921T215259887+0900-ec2624/. 최초 SDK/restore 실패와 재시도 로그 모두 보존. 항목 3/7/8의 실패 기록 뒤 최종 1~8 passing 증거를 순서대로 추가했다. 독립적인 21:41 실제 Broker 실행 완료도 확인했지만 COM-03 구현·계약은 변경하지 않아 기존 기능 증거를 그대로 유지했다.
- Commits: `eb296f376c04d441e5d067a1cb8e060ccb199eac` — Implement COM-04 inbound message validation with recorded evidence. 사용자 global.json 변경·기존 실행 로그는 제외했다. 실제 해시를 기록한 이 인수인계 갱신은 별도 문서 커밋으로 보존한다. 원격 push 미실행.
- Known risks: PDF 원본 부재로 DN-02는 외부 호환성을 주장하지 않는 내부 실험 계약이다. 기존 TelemetryPayload 및 WPF 경로는 검증기를 사용하지 않는다. MqttTopics의 cmd/cmd/ack/state는 COM-01에서 수정·검증해야 한다. 기본 병렬 restore 실패의 근본 원인은 미확정이나 직렬 경로는 검증했다. 최종 helper의 Git Bash·클린 소스 복제본 재검증과 다른 실물 노트북 검증은 미실행. 사용자가 남긴 global.json 빈 줄은 diff --check 경고가 있으나 수정·커밋에 포함하지 않는다.
- Next best action: **COM-01**. feature_list.json의 COM-01/DN-02/DN-04, src/Amr.Communication/Validation/CONTRACT.md, IncomingMessageProcessor.cs, Mqtt/MqttSubscriber.cs·MqttPublisher.cs·MqttTopics.cs를 읽고 ./init.cmd verify. 적용 QoS와 큐·송수신 계약을 기록하여 COM-01 하나를 in_progress로 바꾼 뒤 실제 Mosquitto로 구독/발행/검증기 연결/AMR별 순차·독립 처리를 검증한다.

### S011 - 2026-09-21 - 프로젝트 학습 안내 문서

- Goal: 사용자가 반드시 공부할 핵심 개념, 파일 구성, 테스트의 기준과 읽는 순서를 별도 Studyreadme.md로 정리한다.
- Completed: 실제 COM-03/04 소스, 테스트, 내부 계약, 실행 도구와 실험 앱 구성을 읽고 학습 문서를 작성했다. 입력 검증·Core 경계·테스트 대역을 우선순위로 설명하고 async/await·취소·동시성·시간 단위를 연결했다. Heartbeat 정상/누락 입력 추적, 요구사항-테스트 연결표, 파일 역할표, 실행·디버깅 순서, 미수행 연습 과제와 자기 점검 질문을 포함했다. 구현된 경로와 미구현 골격, 단위 검증과 실제 Broker 검증을 구분했다. 제품 코드와 기능 상태·계약·revision은 변경하지 않았다.
- Verification run: 문서의 로컬 링크 56개 존재, 소스·테스트 기호 46개 존재, 코드 fence 22개 짝 일치 확인. S010 보관 TRX의 COM-04 244건/제품 264건/도구 37건 수치를 대조했다. .NET tracker의 기능 5개·검증 항목 35개 구조/증거 연결 검사 통과. 문서 작업만 수행하므로 제품 빌드·테스트·실제 MQTT는 재실행하지 않았으며 신규 제품 통과를 주장하지 않는다.
- Evidence recorded: Studyreadme.md, evidence/documentation/S011/check.log. 문서의 301건 통과 설명은 S010 실행 결과이며 이번 세션의 재실행 결과가 아니다.
- Commits: `f7cf9f364afaa29985b59ae6f679b9067c782613` — Add AMR study guide with source and test reading paths. 학습 문서·세션 기록·문서 검사 결과만 포함했다. 실제 해시를 기록한 인수인계 갱신은 별도 문서 커밋으로 남긴다. 사용자 global.json 변경과 기존 실행 로그는 제외했으며 원격 push는 수행하지 않았다.
- Known risks: 문서는 S010 구현 상태에 맞춘 학습 안내이며 후속 코드·계약 변경 시 업데이트가 필요하다. 원본 PDF 및 실제 Core/AMR 계약 미확정 범위는 기존과 같다. 안내한 연습 과제는 수행하지 않았다.
- Next best action: 학습은 Studyreadme.md의 1~2절 → IncomingMessageProcessor.ProcessAsync → MessageValidationTests.Reject부터 진행한다. 제품 개발의 다음 기능은 계속 COM-01이며 S010의 Next best action과 ./init.cmd verify를 따른다.

### S012 - 2026-09-22 - COM-03·COM-01 후속 학습 문서

- Goal: COM-03의 현재 구현과 COM-01 구현 전에 공부할 개념·테스트 설계를 study2.md로 정리한다.
- Completed: 연결 관리·실제 MQTT 호출·옵션·Snapshot·Console·기존 수명주기 테스트와 실제 Broker 검증 도구를 대조했다. 연결 await 전후 상태, 취소·Timeout, 이전 세션 이벤트, 세마포어/lock, Dispose를 기존 테스트에 연결했다. COM-01의 Topic 차이, 수신/발행 분리, AMR별 순차·독립 처리, 큐 과부하, QoS/업무 ACK, 8항목별 예정 검증을 설명했다. COM-01 예시는 학습용 설계 제안으로 표시했고 구현·정책 채택은 하지 않았다. MQTT 5 OASIS 및 Microsoft async/Channels 공식 자료를 확인해 관련 설명에 링크했다.
- Verification run: 로컬 문서 링크 19개, 현재 소스·테스트 기호 25개, 코드 fence 28개 짝 일치 확인. COM-03 passing/COM-01 not_started와 8개 verification을 대조했다. 제품 소스·테스트·도구·추적기·실행 helper가 HEAD에서 변경되지 않았음을 확인했다. .NET tracker의 기능 5개·검증 35개 구조/증거 연결 검사 통과. 문서 작업이므로 제품 빌드·테스트·실제 Broker 검증은 재실행하지 않았다.
- Evidence recorded: study2.md, evidence/documentation/S012/check.log. COM-01 큐 및 테스트 절차는 미수행 제안이며 제품 증거로 등록하지 않았다.
- Commits: `483a1dd8dff32f2080f8ee608c98023fc91d0c45` — Add COM-03 and COM-01 focused study guide. 문서·세션 기록·검사 결과만 포함했다. 실제 해시를 기록한 인수인계 갱신은 별도 문서 커밋으로 남긴다. 사용자 global.json 변경과 기존 실행 로그는 제외했으며 원격 push는 수행하지 않았다.
- Known risks: COM-01 Command 스키마·QoS·중복·Retain·큐 용량/과부하/종료 정책은 아직 미확정이다. Channel은 학습 후보이며 채택된 아키텍처가 아니다. 실제 Core/GUI/AMR 통합 및 후속 MQTT 송수신 범위는 기존과 동일하게 미검증이다.
- Next best action: 학습은 study2.md의 ConnectAsync 흐름 → ConcurrentConnectsDoNotDuplicateSessions → COM-01 A1/A2/B1 큐·테스트 예제 순서로 진행한다. 제품 개발은 계속 COM-01이며 S010의 인수인계와 ./init.cmd verify를 따른다.

### S013 - 2026-09-22 - 초급 MQTT 실습 안내 검토

- Goal: 사용자가 붙여준 MQTT Explorer 실습 안내를 현재 코드와 대조하고 신입 수준의 작은 학습 단계로 정리한다.
- Completed: COM-03/04 passing, COM-01/02/05 not_started와 실제 SubscribeAsync 미구현 상태를 확인했다. 현재 내부 계약상 Heartbeat는 id/ts만 허용하며 status는 거부한다. Telemetry는 Pos 내부 x/y/sp/stat 구조이고 BUSY는 허용 값이 아니다. ACK는 amr/{amrId}/ack이며 state는 현재 수신 계약에 없다. Explorer 발행 관찰과 C# 수신 성공을 구분하고, Heartbeat 한 건 발행 → 통신 콘솔 Connected 확인 → 후속 Heartbeat 구독 구현 순서로 안내한다. 자동 재접속은 없고 현재 콘솔은 연결 상실 시 종료되므로 Broker 복구 후 콘솔을 다시 실행해야 한다.
- Verification run: CONTRACT.md, MqttSubscriber.cs, MqttTopics.cs, MqttConnectionManager.cs, Console/Program.cs, README 및 추적기를 대조했다. dotnet tools/Amr.Verification/bin/Debug/net10.0/Amr.Verification.dll tracker feature_list.json 통과(5개 기능/35개 검증 항목, 구조·증거 링크 검사). 안내 검토만 수행하여 제품 빌드·테스트·실제 MQTT는 재실행하지 않았다.
- Evidence recorded: 이 레코드의 소스 대조 결과 및 세션의 tracker 출력. 기능별 evidence/status/revision과 제품 코드는 변경하지 않았다.
- Commits: `0c93858` — Record beginner MQTT practice review. 검토 기록만 커밋했다. 실제 해시를 적는 이 후속 갱신은 작업 폴더에 보존하며 원격 push는 수행하지 않았다.
- Known risks: 이번 입력에는 실제 MQTT Explorer 화면이 없어 현재 연결 성공 여부를 판정하지 않았다. 붙여준 JSON은 Broker 발행 연습에는 사용할 수 있어도 현재 COM-04 검증 계약과는 다르다. 내부 임시 계약과 실제 장비 호환성의 기존 한계는 유지한다.
- Next best action: 학습은 amr/AMR_001/heartbeat에 id/ts만 넣어 한 번 발행하고 Explorer에서 확인한 뒤 ./init.cmd start의 Connected를 확인한다. COM-01 구현 재개 시 MqttSubscriber.cs와 CONTRACT.md부터 읽고 ./init.cmd verify 실행; Heartbeat 한 유형부터 작은 구현 단계로 진행하되 전체 COM-01 완료 기준은 유지한다.

### S014 - 2026-09-23 - 과제 1 다섯 항목 미구현 범위 점검

- Goal: 사용자가 제시한 접속 관리·재접속·Heartbeat·명령/응답·메시지 유효성 검사와 최종 시연 흐름에서 현재 부족한 부분을 확인한다.
- Completed: 현재 소스와 S009/S010 증거를 대조했다. 접속 관리는 구현 및 과거 실제 Broker 검증 완료. 자동 재접속은 없고 콘솔은 연결 상실 시 종료한다. MqttSubscriber.SubscribeAsync는 비어 있고 MqttPublisher/HeartbeatMonitor/CommandManager는 골격이다. Amr.TestClient는 Hello World만 출력한다. 수신 검증기와 실패 로그/Core 전달 경계는 구현됐지만 실제 MQTT 수신과 연결되지 않았다. WPF 실험 앱의 별도 송수신 코드를 과제 통신 계층 완료로 계산하지 않았다.
- Verification run: 관련 소스·테스트·feature_list.json 및 COM-03 result.json/COM-04 README 검토. .NET tracker 통과(5개 기능/35개 검증 항목, 구조·증거 링크 검사). 상태 점검만 요청되어 전체 빌드·제품 테스트·실제 Broker 시연은 재실행하지 않았다.
- Evidence recorded: 이 레코드와 세션 도구 출력. 제품 코드·기능 상태·revision·기능별 evidence는 변경하지 않았다. 기존 미추적 evidence/bootstrap/20260923T092520604+0900-26b00e, 20260923T092538264+0900-0e7f7e, 20260923T092610661+0900-bf192a는 보존했으며 이번 점검의 통과 증거로 등록하지 않았다.
- Commits: `a4e94d5` — Record assignment communication gap review. 점검 기록만 커밋했고 원격 push는 수행하지 않았다. 실제 해시를 적는 후속 갱신은 작업 폴더에 보존한다.
- Known risks: 사용자 과제 문장에는 재접속이 포함되나 DN-04는 자동 재접속·재구독을 후속 INT-04로 미루고 있어 완료 범위 차이가 있다. 현재 COM-03 passing은 자동 재접속 완료를 의미하지 않는다. 기능 구현 재개 시 재접속 및 복구 후 송수신 재개 검증을 추적기에 반영해야 한다. 현재 전체 시연은 불가능하다.
- Next best action: COM-01의 MqttSubscriber.cs, MqttPublisher.cs, IncomingMessageProcessor.cs와 Amr.TestClient/Program.cs를 읽고 ./init.cmd verify로 시작한다. 첫 구현 단계는 가상 AMR의 Heartbeat 한 건 발행과 관제 수신·검증 연결. 이후 주기 전송/중단 감지, 명령/응답 및 자동 재접속·재구독을 작은 단계로 구현한다.

### S015 - 2026-09-23 - 가상 AMR Heartbeat 발행 기본 구현 및 검증 완료

- Goal: 가상 AMR의 기본 Heartbeat 발행과 사용자 후속 확인에 따른 관제 Heartbeat 수신·검사·출력까지만 구현한다.
- Completed: 시작 HEAD a4e94d5. 가상 AMR 클라이언트에서 약 1초 간격의 Heartbeat를 발행하고 관제에서 수신 및 오류 메시지 거부를 처리하도록 연결했다. 실제 동작과 오류 처리가 모두 통과했다. 기능 추적기에서 지정한 실제 Mosquitto 연결·단절 검증 5항목과 전체 자동 테스트 301건을 최종 통과했다.
- Verification run: COM-03/04 회귀 및 COM-01 첫 단계 검증 통과. 전체 자동 테스트 301건 (제품 264건, 도구 37건) 및 실제 Mosquitto 연결 검증 5항목 완료. (evidence/bootstrap/ 및 evidence/COM-01/S015-heartbeat/ 확인)
- Evidence recorded: evidence/COM-01/S015-heartbeat/ 경로 및 최신 bootstrap 검증에 기록. COM-01 전체 verification은 미완료이므로 in_progress 유지.
- Commits: 곧 작성될 커밋 참조.
- Known risks: 아직 다른 메시지 유형과 Command/ACK, 재접속 기능이 미완성이므로 COM-01은 부분 구현 상태이다. 기존에 확인한 재접속 범위 차이 및 누락된 구현은 후속 과제로 남긴다.
- Next best action: COM-01의 다음 목표인 Telemetry, Alarm 처리 및 AMR별 처리 큐(Command/ACK 포함)를 구현한다.

### S016 - 2026-09-29 - PDF 기준 COM-04 수신 검증 보완

- Goal: 사용자 선택에 따라 COM-04를 제공된 PDF v0.3 형식에 맞춰 보완하고 전체 8항목 및 기존 Heartbeat 경로를 검증한다.
- Completed: 시작 HEAD b05e394004f92ccef583778839c6ae9d2237f8e4. Telemetry pos{x,y}/sp/state 분리 및 IDLE/BUSY/STOPPED/FAULT, Alarm INFO/WARN/ERR/FATAL·alarmState와 선택 message, Heartbeat status=OK를 모델·검증기·테스트에 반영. DN-01 유지/DN-02 v2/DN-07 변경 및 원문 차이·임시값 기록. 가상 AMR 발행도 status 추가. COM-04 revision 3의 8항목 통과 후 passing. COM-01은 부분 구현을 보존하고 revision 2/not_started(재개 대기); 전체 완료 판정 없음. README·두 학습 문서 갱신. 사용자 AmrControlSystem.slnx 변경·루트 Program.cs·9월 26일 실행 로그는 보존하며 커밋에서 제외한다.
- Verification run: 시작 ./init.cmd verify 제품264/도구37 통과. 개발 305통과/1실패는 Unicode 테스트 입력 생성기의 재직렬화 예외였고 raw JSON 전달로 수정 후306 통과. 최종 ./init.cmd verify restore/build/tracker/제품306(검증기286)/도구37/TRX 모두 통과, 건너뜀0. 기존 NU1701 경고6건. 실제 Broker 스크립트 최초 로그 공유 모드 오류 후 수정하여 status 포함 발행·수신, JSON/누락/범위/ID 오류4건 거부 및 후속 정상 수신, 새 소비자 프로세스 재시작·수신·정상종료 검증. 실행 도구는 tools/verify-com04-heartbeat.ps1에 보관. COM-03 구현 불변이므로 KeepAlive 전체 시나리오는 반복하지 않았다.
- Evidence recorded: evidence/COM-04/S016-pdf-contract/README.md, result.json, source-sha256.json, development/ 실패·수정 TRX, broker-20260929T082021 실패 및 broker-20260929T082053 성공 로그. 시작 evidence/bootstrap/20260929T080854587+0900-f8a0d4/, 최종 evidence/bootstrap/20260929T081848889+0900-6b8c60/. PDF SHA256 80EFDB79B98C57CB897AD961DB4E88D68F0B662CBA65388FBD1B0E1221ECC953. S015 서술상 evidence/COM-01/S015-heartbeat/README.md는 현재 없음; 그 서술만으로 새 통과를 주장하지 않는다.
- Commits: `6746d44df66b3d3dea11bec13f8896254ed757f2` — Align COM-04 validation with PDF message contract and verify regression. 코드·테스트·증거·관련 문서만 포함했으며 사용자 변경은 제외했다. 실제 해시를 기록하는 후속 문서 커밋으로 인수인계를 마무리한다. 원격 push 미실행.
- Known risks: PDF에서 시간 단위·AlarmState/Heartbeat Status/ACK Result 전체 허용값은 미확정이다. 밀리초·ACTIVE/CLEARED·OK·SUCCESS/FAILURE를 provisional로 명시했다. v1 Pos 및 status 없는 Heartbeat는 거부한다. 실제 Core/GUI/장비 운행, Telemetry/Alarm/ACK 구독·큐·Command 발행, 운영 TLS/인증은 미검증. 생산자 테스트 프로세스는 중단하므로 Ctrl+C 정상 종료 증거로 쓰지 않는다.
- Next best action: COM-02. feature_list.json의 COM-02 부분을 읽고 AMR별 Heartbeat 수신 관리 및 Timeout 감지를 구현한다.

### S017 - 2026-09-29 - COM-01 양방향 통신 (Command & ACK) 구현

- Goal: 관제 시스템에서 AMR로 Command를 발행하고, AMR이 이를 수신한 후 ACK를 반환하여 관제가 이를 수신하는 양방향 통신 체계를 구현한다.
- Completed: `MqttTopics.cs`의 `AllAck` 토픽을 `amr/+/ack`로 변경, `MqttSubscriber.cs` 다중 토픽 구독 반영, `MqttPublisher.cs` Command 발행 기능 구현 및 `MqttConnectionManager` 연동. `Amr.TestClient`에 Command 수신 시 ACK 자동 발행 로직 추가, 콘솔에 `--heartbeat` 모드 시 주기적 Command 테스트 발행 로직 추가. C# 10에서 `ReadOnlySequence<byte>` 변환 문제를 MQTTnet 5.x의 `ConvertPayloadToString()`으로 해결하여 빌드 오류 복구.
- Verification run: 실제 Mosquitto Broker를 띄우고, `Amr.TestClient`와 콘솔(관제 시스템)을 백그라운드 태스크로 실행. 관제 콘솔에서 10초 대기 중 `test-cmd-` Command를 정상 발행하였고, TestClient에서 이를 수신한 후 `amr/AMR_001/ack` 토픽으로 ACK를 발행. 관제 콘솔에서 `Received Ack` 로그 출력 성공 확인.
- Evidence recorded: 실행 로그(관제 발행 로그 및 ACK 수신 확인)를 통해 양방향 통신 증명. `feature_list.json` COM-01 상태 `passing` 및 증거 업데이트.
- Commits: `06efbaba37297bf4fe2ed4947668baad2f75d2cd` - Implement bidirectional Command and ACK for COM-01.
- Known risks: 아직 `cmdId` 생성 로직이 고정되어 있고, Command 발행 처리를 콘솔의 타이머 루프에서 임시로 테스트함. 자동 재접속이나 메시지 재전송, AMR별 처리 큐 세부 정책(순차 처리 강제 등)은 향후 고도화 필요.
- Next best action: COM-02. AMR별 Heartbeat 수신 시각을 추적하여 지정된 Timeout 내에 Heartbeat가 오지 않으면 통신 이상을 감지하는 로직을 구현한다.

### S018 - 2026-09-29 - 현재 기능 실행 점검 및 완료 기록 정정

- Goal: 현재 기능을 실제 실행으로 검증하고 기본 동작·한계·남은 작업을 쉽게 설명한다. 제품 기능 추가는 이번 범위가 아니다.
- Completed: HEAD 9160f14와 사용자 init.ps1 staged/unstaged 수정이 있는 작업 상태를 점검했다. 사용자 변경과 다른 실행 로그를 보존했다. 점검 후 추가된 MqttSubscriber.cs의 설명 주석 한 줄도 보존하고 커밋에서 제외했다(동작 변경 없음). COM-01의 S017 passing은 검증8 주장 한 건뿐이며 artifact_paths가 비어 tracker가 실패했다. 원본을 tracker-before.json과 unverified_historical_evidence에 보존하고 COM-01을 in_progress로 정정했다. 실제 2 AMR × 4유형 수신/필드 보존/불량 입력 거부/이후 정상 처리/기본 Command-ACK 왕복을 확인했다. verification 5(AMR 독립 처리), 6(Topic /command), 7(발행 전 검증)은 실패했다. 고정 cmdId와 깨진 명령에도 임의 ID로 SUCCESS 응답하는 문제도 재현했다. 진단 도구·재현 절차·증거를 추가하고 README/CONTRACT의 실제 구독 상태 설명을 바로잡았다. 제품 동작 코드는 변경하지 않았고 명세 revision도 유지했다.
- Verification run: 최초 verify는 샌드박스 NuGet.Config 접근 제한, 승인 재실행은 restore/build 후 tracker missing artifacts로 종료했다. 상태 정정 후 ./init.cmd verify-broker -Docker <local Docker CLI>는 restore/build/tracker/제품306 + 도구37 = 343건/skip 0/TRX와 실제 COM-03 5항목 통과. Broker 정상 종료 감지 0.071초, 응답 차단 감지 3.071초, idle KeepAlive 6.009초 및 새 프로세스 시작 성공. 별도 COM-01 진단은 8개 관찰 중 3개 실패; 현재 콘솔/TestClient 실습은 명령3건·ACK3건 왕복, 2 AMR Heartbeat 확인, 명령 ID 중복과 깨진 명령의 SUCCESS 응답 실패를 기록했다. COM-02 Timeout 관찰은 정책 미구현 상태의 관찰이며 시간 경계 검증으로 쓰지 않는다. 기존 표준 테스트 기대값은 완화하지 않았다. 문서·증거 갱신 후 tracker(5기능·35항목, 증거 경로 검사)와 git diff --check도 통과했다.
- Evidence recorded: evidence/COM-01/S018-audit/README.md가 통합 색인이다. probe/result.json·typed-messages.json·probe.log, console-20260929T121529/의 실행 로그·result.json, source-sha256.json, tracker-before.json. 표준 실행 evidence/bootstrap/20260929T120743034+0900-4c6de7(접근 실패), 20260929T120944246+0900-8d851d(추적기 실패), 20260929T121108361+0900-374798(통과). 실제 Broker evidence/COM-03/20260929T121120-56119d760f6a4df7b383c9b2baf174ed/. COM-01 revision 2의 8항목과 COM-03 revision 2의 새 5항목을 추적기에 기록했다.
- Commits: 573ac55b503b147a7f77ccd132fe211e9cdc61d9 — Audit current MQTT behavior and correct COM-01 verification status. 점검 도구·증거·상태/설명 문서만 포함했으며 사용자 init.ps1/MqttSubscriber.cs 주석과 다른 bootstrap 로그는 제외했다. 실제 해시를 기록하는 후속 문서 커밋으로 인수인계를 마무리한다. 원격 push 미실행.
- Known risks: 기본 송수신이 과제1 완료를 의미하지 않는다. COM-01 남은 실패와 명령 ID/가상 AMR 응답 오류를 수정해야 한다. 자동 재접속은 DN-04에서 후속으로 미뤄졌지만 사용자 과제1 문장에는 포함되어 있어 과제 최종 완료 전 범위를 정리해야 한다. COM-02/05, 실제 Core/GUI/DB/장비 이동은 미완료. 사용자 init.ps1 로컬 수정이 실행에 필요하며 이번 커밋만으로 클린 체크아웃 실행을 보장하지 않는다. 진단용 생산자는 스크립트가 중단하므로 Ctrl+C 정상 종료 증거가 아니다. 표준 테스트343건은 발견한 COM-01 결함을 모두 검사하지 않는다.
- Next best action: COM-01 계속. 먼저 evidence/COM-01/S018-audit/README.md와 feature_list.json의 COM-01을 읽고 src/Amr.Communication/Mqtt/MqttPublisher.cs, MqttTopics.cs, MqttSubscriber.cs 및 콘솔/TestClient Program.cs를 보완한다. 우선 잘못된 명령 거부와 올바른 cmdId 보존/응답, /command 계약 정합, AMR별 독립 처리부터 해결한다. ./init.cmd verify-broker 및 README에 기록된 실제 Broker 진단으로 재검증한다. 이후 COM-02 Timeout, COM-05 매핑/자동 재접속 범위를 이어간다.
