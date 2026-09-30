# AmrControlSystem Progress Log

새 세션은 이 파일을 먼저 읽고 `AGENTS.md`, `feature_list.json`과 관련 소스·테스트를 확인한다. 이 파일과 AmrControlSystem.slnx가 있는 폴더가 실제 프로젝트 루트다. PC별 경로와 폴더 이름은 고정하지 않는다. 과거 세션의 PC 경로·명령은 당시 기록이며 현재 실행 경로는 아래 표와 README를 따른다.

## 현재 검증된 상태 (Current Verified State)

| Field | Verified value |
| --- | --- |
| Last updated | 2026-09-30, Asia/Seoul. S032 문서 폴더 통합·진행 기록 분리; 최신 제품 실행 검증은 S024(2026-09-29). |
| Repository root directory | 이 파일이 있는 디렉터리(.). S009 작업 경로는 C:\practice; 새 PC에서는 clone한 위치를 사용한다. |
| Standard startup path | ./init.cmd broker 후 ./init.cmd start는 연결만 확인. ./init.cmd start -Heartbeat는 수신만 하며 별도 Amr.TestClient가 Heartbeat를 발행한다. 명령은 콘솔 --command MOVE --amr-id AMR_001 --x 30 --y 15로 한 건씩 요청한다(README 참조). Windows PowerShell 5.1에서도 cmd가 PowerShell 7.4+ 호출. Ctrl+C 종료. |
| Standard verification path | ./init.cmd verify: restore/build, .NET 추적기 검사·전체 자동 테스트·TRX 검사. 실제 Broker 검증은 ./init.cmd verify-broker. Python 불필요. 로그는 evidence/bootstrap/실행ID/. |
| Current scope | 과제 1: COM-01~05. 현재 기능별 검증 항목37개(COM-01 과부하·종료2항목 추가). |
| Highest priority unfinished feature | 현재 등록 기능 중 COM-05 Command/ACK 식별자 연계가 미완료. 사용자 요청의 다음 개발 순서는 docs/mvp/happy_path_plan.md의 M1-00~05이며 신규 범위·의존성은 구현 착수 시 등록한다. |
| Active feature | 없음. COM-01 revision4/COM-02 revision2/COM-03 revision2/COM-04 revision4 passing. COM-05 revision1 not_started. |
| Planning documents | docs/README.md에서 위치 확인. docs/mvp/는1차 계획, docs/full/은 전체24기능·93AC·과제 대응, docs/reports/는 보고 자료. D-GUI3항목은 미실행, DN-11~14 proposed 유지. 현재 제품5기능의 계약/revision/증거는 동일. |
| Verified product behavior | S024: 네 유형 수신·명령/ACK·AMR별 비동기 큐 회귀 통과. 첫 정상 Heartbeat 수신부터 AMR별 감시, 불량 입력은 갱신하지 않음, Timeout/복구1회, 느린 Core/큐 포화와 독립. 실제 기본5초 감시는5222.7603ms에 A만 Timeout, 같은 ID 재시작 복구, B수신 지속. 실제 이동 미구현 FAILURE 유지. |
| Current blocker | 현재 필수 검증 blocker 없음. 초기 Windows App Control0x800711C7/CodeIntegrity3077 실패는 보존. 사용자17:58 및 최종18:08 실행에서 DLL 로딩과440건 테스트 통과. 에이전트는 Windows 보안 설정을 변경하지 않았다. |
| Requirements baseline | PDF v0.3을 출발점으로 사용한다. 현재 적용 범위·검증은 feature_list.json, 설계 쟁점은 design_notes에서 관리한다. S019 과제1·2 계획의 첫 작업 네 항목을 S020에서 구현했다. DN-08 Command v1/ACK reason 확장과 개발용 결정은 계약 문서에 기록했다. |
| Harness verification | S024 최종 init.cmd verify440건(제품403/도구37, skip0, Heartbeat19 포함). 실제 Heartbeat8/8, 콘솔5/5, 송수신/큐12/12, 명령9/9, COM-03연결5/5. 솔루션에 Heartbeat Audit 등록; 기존 WPF NU1701 경고6건. |
| Working rules | AGENTS.md 적용. DN-01/02/08/09 유지. S024 DN-10: 첫 정상 Heartbeat부터 단조 시계 감시, 기본5초/500ms, Timeout1회·복구1회·세션 종료 시 감시 중단. 최종 Offline·운행 정책은 후속. 실제 Core는 동시 호출·취소에 협조하고 Heartbeat 알림 콜백은 짧게 반환해야 한다. |
| Environment | Windows10.0.26200, SDK10.0.401, 에이전트 PowerShell7.6.5/사용자17:58로그7.6.6, Git2.55.0.windows.5, MQTTnet5.2.0.1603, Docker29.7.2/Mosquitto2.1.2-alpine. localhost:1883 QoS0/Retain false. COM-03은 격리 임시 포트. |
| Tool dependencies | Windows, Git, global.json의 .NET SDK, PowerShell 7.4+. Docker Desktop은 Broker 사용 시 필요. Python과 Codex 런타임에 의존하지 않는다. |
| Current Windows baseline commit | S024 시작 HEAD b3523c0 + 이번 구현 + 기존 사용자 로컬 변경 상태에서 검증. 최종 증거 evidence/COM-02/S024-heartbeat/README.md 및 final-validation/source-sha256-final.json. S024 구현 커밋4e25f5a. 검증은 기존 사용자 변경이 포함된 작업 트리 기준이며 클린 복제본 검증으로 확대하지 않는다. |
| Initial baseline commit | 58a53008c02cde8765e37a23375cf54c35f7dc85. 이 커밋만 복제한 새 체크아웃에서도 빌드·테스트·콘솔 시작 통과. |
| Git remote / branch | origin = https://github.com/indsf/practice.git, master → origin/master. S006에서 사용자 지정 저장소로 업로드. |

## 지난 기록

[S001~S031 세션 기록](docs/history/sessions-S001-S031.md)을 별도로 보관한다. 과거 판단과 실패 증거는 삭제하지 않았다.

## 세션 레코드 (Session Record)

### S032 - 2026-09-30 - 문서 폴더 통합과 현재 기록 분리

- Goal: 복잡해진 프로젝트를 목적별 폴더와 짧은 진입점으로 정리한다.
- Completed: 시작 HEAD2b14da6, 기존 MVP 계획서 사용자 수정 보존. 문서16개를 docs/mvp·full·guides·reports·study로 이동하고 S001~S031 원문을 docs/history에 분리했다. 루트 README는38줄 시작 안내, 진행 로그는 현재 상태와 이번 세션으로 정리했다. 상대 링크·문서 트리·AGENTS 보관 규칙·feature_list의 현재 문서 색인을 갱신했다. 과거 결정의 당시 경로는 이동 대응표로 연결한다. 기존 빈 임시 폴더는 .tools로 보존 이동했다.
- Verification run: 이동 파일 본문/사용자 차이/31개 세션 보존, 전체 Markdown 상대 링크, 기존5기능 배열·설계 결정 동일, 제품/솔루션/스크립트 변경 없음 확인. tracker5기능/37항목·Git 공백 검사 통과. 상세 수치는 S032 verification.json에 기록한다. 문서 이동이며 실행 의존 경로가 바뀌지 않아 제품 빌드·단위·Broker·GUI 시험은 미실행이다.
- Evidence recorded: evidence/documentation/S032-organization의 migration.json·README.md·verification.json. 기존 documentation README3개의 링크만 새 문서 위치로 수정했으며 제품 실행 결과와 evidence 배열은 유지했다.
- Commits: 33483d1 — Organize documentation by purpose and archive session history. 검증된 문서 이동·링크·규칙·색인만 포함했다. 사용자 MVP 계획서 수정은 새 위치의 미커밋 차이1줄로 보존했다. 실제 해시를 포함한 이 진행 로그는 후속 인수인계 커밋으로 남기며 원격 push는 하지 않았다.
- Known risks: 과거 기록의 instruction/project_guide 경로는 당시 기록으로 남아 있다. 현재 위치는 docs/README.md와 이동 대응표를 따른다. 이동한 MVP 계획서의 기존 사용자 수정은 미커밋으로 남기며 문서 정리를 새 제품 구현 완료로 보지 않는다.
- Next best action: docs/mvp/happy_path_plan.md로1차 개발, docs/full/prd.md로 전체 범위, docs/reports/mvp_review_summary.md로 보고 내용을 확인한다. 실제 개발 착수는 루트에서 ./init.cmd verify 후 M1-00부터 진행한다. 이전 세션은 docs/history/sessions-S001-S031.md에서 확인한다.
