# AmrControlSystem Progress Log

새 세션은 이 파일을 먼저 읽고 `AGENTS.md`, `feature_list.json`과 관련 소스·테스트를 확인한다. 이 파일과 AmrControlSystem.slnx가 있는 폴더가 실제 프로젝트 루트다. PC별 경로와 폴더 이름은 고정하지 않는다. 과거 세션의 PC 경로·명령은 당시 기록이며 현재 실행 경로는 아래 표와 README를 따른다.

## 현재 검증된 상태 (Current Verified State)

| Field | Verified value |
| --- | --- |
| Last updated | 2026-10-01, Asia/Seoul. S034 공유 규칙 상세 작성·기능명세 연결 후 별도3페이지 공통데이터 PDF의 역할과 정정 대상을 검토; 최신 제품 실행 검증은 S024(2026-09-29). |
| Repository root directory | 이 파일이 있는 디렉터리(.). S009 작업 경로는 C:\practice; 새 PC에서는 clone한 위치를 사용한다. |
| Standard startup path | ./init.cmd broker 후 ./init.cmd start는 연결만 확인. ./init.cmd start -Heartbeat는 수신만 하며 별도 Amr.TestClient가 Heartbeat를 발행한다. 명령은 콘솔 --command MOVE --amr-id AMR_001 --x 30 --y 15로 한 건씩 요청한다(README 참조). Windows PowerShell 5.1에서도 cmd가 PowerShell 7.4+ 호출. Ctrl+C 종료. |
| Standard verification path | ./init.cmd verify: restore/build, .NET 추적기 검사·전체 자동 테스트·TRX 검사. 실제 Broker 검증은 ./init.cmd verify-broker. Python 불필요. 로그는 evidence/bootstrap/실행ID/. |
| Current scope | 과제 1: COM-01~05. 현재 기능별 검증 항목37개(COM-01 과부하·종료2항목 추가). |
| Highest priority unfinished feature | 현재 등록 기능 중 COM-05 Command/ACK 식별자 연계가 미완료. 사용자 요청의 다음 개발 순서는 docs/mvp/happy_path_plan.md의 M1-00~05이며 신규 범위·의존성은 구현 착수 시 등록한다. |
| Active feature | 없음. COM-01 revision4/COM-02 revision2/COM-03 revision2/COM-04 revision4 passing. COM-05 revision1 not_started. |
| Planning documents | docs/README.md에서 위치 확인. docs/mvp/는1차 계획, docs/full/은 전체24기능·93AC·과제 대응, docs/reports/는 보고 자료. D-GUI3항목은 미실행, DN-11~14 proposed 유지. 현재 제품5기능의 계약/revision/증거는 동일. |
| Documentation next action | docs/guides/common_rules_worknote.md의 ‘별도 공통데이터 PDF가 있는 경우’에서 문서 역할을 확인한다. 데이터·Topic·유형별 필드는 공통데이터 명세, Alarm 업무 처리 규칙은 요구사항 PDF, 기능별 실행은 기능명세에 연결한다. 현재3페이지 데이터 초안의 시간·좌표·ACK 방향·출처/상태 칸을 대조한다. 새 정책 채택·제품 구현을 뜻하지 않는다. |
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

### S033 - 2026-10-01 - 공통 규칙 작성 재개 메모

- Goal: 공통 규칙에 대한 대화 내용을 다음 작업일에 이어갈 수 있는 메모로 정리한다.
- Completed: 시작 HEAD a6a56ad3f687a3761fa889c91ec26531d2c44c64, 시작 작업 트리 clean. 받은 과제와 작성한 PDF의 역할, 기본/상세 수준과 초안/채택 상태의 구분, AMR 식별·시간·Alarm 예시, 다음 작업 순서를 docs/guides/common_rules_worknote.md에 기록하고 docs/README.md에서 연결했다. 원본 PDF·PRD·계약·feature_list.json·제품 코드는 유지했다.
- Verification run: 변경 문서3개의 상대 링크 대상45개 존재 확인, 출처·상태 표현 검토, feature_list.json SHA256 6D6DA7CCD6581BDA1451C241C3185730C1BD0C71F348A12B1200CEE97DE804ED 보존 확인. tracker5기능/37항목 및 Git 공백 검사 통과. 실행 경로와 제품 계약에 영향이 없는 작성 메모이므로 빌드·단위·Broker·GUI 검증은 미실행이다.
- Evidence recorded: 위 문서 검사 관찰 결과를 이 Session Record에 기록했다. 제품 실행 증거는 추가하지 않는다.
- Commits: 5d6bb1cd9fa389ee7ba3d40a773d45acbc5f75a7 — Record common rules writing handoff. 검증된 메모·문서 안내·진행 기록3파일을 커밋했다. 실제 해시 기록은 후속 인수인계 커밋으로 남긴다.
- Known risks: 예시는 새 확정 규칙이 아니다. Alarm 임계값·전이·등록·운영 시간 정책 등은 미정 상태를 보존해야 한다. 현재 COM-01~04 passing은 기존 계약 범위이며 전체 관제 통합 완료가 아니다.
- Next best action: 2026-10-02 docs/guides/common_rules_worknote.md를 읽고 세 항목을 자신의 말로 작성한다. 기존 PRD 6절·interface_contract와 채택된 계약을 대조하고 미정 사항은 명시한다. 제품 구현 재개 시에는 AGENTS의 표준 기준선 절차를 따른다.

### S034 - 2026-10-01 - 업무 보고 체계와 상세 작성 기준 기획

- Goal: 사용자 오늘 계획의 보고 양식을 준비하고, 후속 설명에 맞춰 공통 계약·요구사항·기능명세를6페이지 PDF의 공유 규칙 상세 정의와 연결한다.
- Completed: 시작 HEAD 2bec65d, 시작 작업 트리 clean. ‘기능 명세 만료’는 사용자 답변으로 ‘기능 구현 완료 보고’임을 확인했다. 이번 첨부 260916 PDF6페이지를 읽고 관련 p3~5를 이미지로 확인했다. SHA256 B30FFEC36679B3DEBBC9BEEFB67952C790CE8FDDD976FF53934F2B88C9B0E879이며 기존17페이지 PDF와 구분했다. 기존 common_rules_worknote.md에 네 양식·보고 시점·기록 위치·오늘 완료 기준을 추가했다. 단순 상세 구체화와 계약/합격 기준 변경, 명세 작성과 현재 범위 제품 검증 완료를 구분한다. 문서 수를 늘리지 않았으며 PRD·기능명세·공통 계약·제품·추적기는 유지했다.
- Completed checkpoint: 후속 작업 시작 HEAD c1124ff, 작업 트리 clean. 사용자는 공통 계약을 보고 현재6페이지 PDF에 공유 규칙을 어떻게 작성하고 이를 기준으로 기능명세를 작성할지가 핵심이라고 설명했다. 같은 메모 앞부분에서 작성 목적을 정리하고 PDF쪽별 배치, 기존11개 규칙과 공통 계약/관련 기능의 대응, 장비 식별 본문·COM-04 기능 연결 예시, 시간·Alarm의 채택/제안 구분을 추가했다. 기존 보고 계획은 보조 자료로 유지한다. PDF·공통 계약·PRD·기능명세·추적기·제품은 수정하지 않았다.
- Completed PDF checkpoint: 별도 공통데이터 v0.1 PDF3페이지(SHA256 BB23C560ADF26ADC5F94B6054634DEC7A672A42C9FBE6CAA426140FF79196710)를 읽고 본문 p2~3 이미지를 확인했다. 데이터·통신 계약은 별도 명세, 업무 공유 규칙은 요구사항 PDF, 실행 과정은 모듈 기능명세로 연결하는 검토를 같은 메모에 추가했다. 시간·좌표 설명의 혼재, ACK 방향, 사용처가 들어간 근거/상태 칸, 미작성 Alarm 등 유형별 표를 정정·보완 대상으로 기록했다. 시작 HEAD afcafdb; 기존 사용자 interface_contract.md ACK 행 미커밋 변경은 보존했다. 검토는 read-only이며 PDF를 수정·재출력하지 않았다.
- Verification run: tracker5기능/37항목 구조·증거 링크 PASS, 변경 문서2개 상대 링크17개 존재·Git 공백 검사 통과. feature_list.json SHA256 2A448A0CE96C84FBE4912604170FE52508CF84C7CDA8994953506E0BD540C3EC가 작업 전후 동일하다. PDF 렌더링은 번들 pdftoppm.cmd의 경로 오류 후 실제 Library/bin/pdftoppm.exe로 성공했다. 제품 실행 경로·계약 변경이 없는 보고 기획이므로 빌드·단위·실제 Broker·GUI 검증은 미실행이다.
- Verification checkpoint: 공유 규칙 상세 구성 추가 후 tracker5기능/37항목·Git 공백 검사 PASS, 변경 문서2개의 상대 링크20개 존재 확인. 이번6페이지 PDF·공통 계약·추적기의 SHA256이 작업 전후 동일하다. 기존11개 규칙 번호·관련 기능 ID·DN-01/02/10과 P-04/06/08/16/21의 적용 상태를 대조했다. 문서의 당일 구성 안내 링크는 해당 소제목과 일치한다. 계약·제품 변경이 없어 제품 실행 시험은 추가하지 않았다.
- Verification PDF checkpoint: 새 공통데이터 PDF의 본문 p2~3 전체를 렌더링해 표와 추출 텍스트를 대조했다. tracker5기능/37항목·관련 문서 Git 공백 검사 PASS, 상대 링크20개 존재. 새 PDF·추적기 및 사용자 변경이 있는 공통 계약 SHA256이 검토 전후 동일하다. 사용자 공통 계약의 ACK 행 변경은 미커밋 상태로 보존하며 이번 기록에 포함하지 않는다. 문서 검토만 수행해 제품 실행 시험은 추가하지 않았다.
- Evidence recorded: 이번 문서 검토는 이 Session Record에 기록한다. PDF 검토 이미지는 임시 디렉터리에 있으며 제품 evidence로 등록하지 않는다.
- Commits: f17aa225cf713dcdfab94392d67578a6dcecd7a7 — Plan requirements and feature reporting workflow. 보고 체계 초안과 진행 기록2파일. 후속 5d5bfe0935a5398116afdcb00154a07438f5cfb9 — Clarify shared rules structure for requirements PDF. 공유 규칙 상세 구성과 진행 기록2파일. 추가 45c0f63a78e8c7accd2a8160934871445d0fdb48 — Review standalone common data specification. 검증된 별도 공통데이터 PDF 검토 메모와 진행 기록2파일을 로컬 커밋했다. 사용자 interface_contract.md 변경은 포함하지 않았다. 실제 해시 기록은 후속 인수인계 커밋으로 남기며 원격 push는 하지 않았다.
- Known risks: 보고 양식은 검토용 제안이며 사수에게 발송·승인되지 않았다. 이번6페이지 PDF를 기존17페이지 명세의 교체본으로 채택하지 않았다. p5 운행 상태 행의 ‘상태’ 칸에는 필요한 이유가 들어 있어 이유와 채택 상태를 구분할 필요가 있다. COM-01~04 passing은 기존 추적기 범위이며 새 전체 명세의 모든 AC 또는 과제1 전체 통과를 뜻하지 않는다.
- Next best action: docs/guides/common_rules_worknote.md의 ‘별도 공통데이터 PDF가 있는 경우’를 읽고 공통데이터 명세의 시간·좌표·ACK 방향을 대조한 뒤 Alarm 등의 필드 표를 작성한다. 요구사항 PDF에는 참조 절과 업무 공유 규칙을 적고 관련 기능의 처리·검증에 연결한다. 원문 출처와 기존 개발용 채택/제안·미정 상태를 유지한다. 제품 구현 재개는 ./init.cmd verify와 현재 MVP 순서를 따른다.
