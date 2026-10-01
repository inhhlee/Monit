# AmrControlSystem Progress Log

새 세션은 이 파일을 먼저 읽고 `AGENTS.md`, `feature_list.json`과 관련 소스·테스트를 확인한다. 이 파일과 AmrControlSystem.slnx가 있는 폴더가 실제 프로젝트 루트다. PC별 경로와 폴더 이름은 고정하지 않는다. 과거 세션의 PC 경로·명령은 당시 기록이며 현재 실행 경로는 아래 표와 README를 따른다.

## 현재 검증된 상태 (Current Verified State)

| Field | Verified value |
| --- | --- |
| Last updated | 2026-10-01, Asia/Seoul. S035 공통데이터 v0.111 검토·JSON 관련355건 통과·집에서 재개할 문서 인수인계. 최근 전체 기준선·실제 Broker 검증은 S024(2026-09-29). |
| Repository root directory | 이 파일이 있는 디렉터리(.). S009 작업 경로는 C:\practice; 새 PC에서는 clone한 위치를 사용한다. |
| Standard startup path | ./init.cmd broker 후 ./init.cmd start는 연결만 확인. ./init.cmd start -Heartbeat는 수신만 하며 별도 Amr.TestClient가 Heartbeat를 발행한다. 명령은 콘솔 --command MOVE --amr-id AMR_001 --x 30 --y 15로 한 건씩 요청한다(README 참조). Windows PowerShell 5.1에서도 cmd가 PowerShell 7.4+ 호출. Ctrl+C 종료. |
| Standard verification path | ./init.cmd verify: restore/build, .NET 추적기 검사·전체 자동 테스트·TRX 검사. 실제 Broker 검증은 ./init.cmd verify-broker. Python 불필요. 로그는 evidence/bootstrap/실행ID/. |
| Current scope | 과제 1: COM-01~05. 현재 기능별 검증 항목37개(COM-01 과부하·종료2항목 추가). |
| Highest priority unfinished feature | 현재 등록 기능 중 COM-05 Command/ACK 식별자 연계가 미완료. 사용자 요청의 다음 개발 순서는 docs/mvp/happy_path_plan.md의 M1-00~05이며 신규 범위·의존성은 구현 착수 시 등록한다. |
| Active feature | 없음. COM-01 revision4/COM-02 revision2/COM-03 revision2/COM-04 revision4 passing. COM-05 revision1 not_started. |
| Planning documents | docs/README.md에서 위치 확인. docs/mvp/는1차 계획, docs/full/은 전체24기능·93AC·과제 대응, docs/reports/는 보고 자료. D-GUI3항목은 미실행, DN-11~14 proposed 유지. 현재 제품5기능의 계약/revision/증거는 동일. |
| Documentation next action | docs/guides/common_rules_worknote.md의 ‘2026-10-01 집에서 이어할 작업’을 먼저 읽고 내부 관측 필드표 작성·공통 정의/JSON 중복 표 통합·PDF7곳 정정을 사용자 문서에 반영한다. 대화 수정 문안과 HWP/PDF 실제 반영 완료는 구분한다. 다음은 COM-03 구현용 기능명세이며 개발용 채택·미정을 유지한다. |
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

### S035 - 2026-10-01 - 현재 공통데이터 PDF 누락·차이 검토

- Goal: 현재 프로젝트와 사용자 공통데이터 v0.111 PDF를 대조해 누락·차이를 검토한다. 사용자 답변으로 새 문서 작성 대신 검토 범위를 확정했다.
- Completed: 시작 HEAD fdc535a, 시작 작업 트리 clean. 요구사항 HWP의 ZIP/XML 본문, 공통데이터 PDF 5페이지와 요구사항 PDF 6페이지 텍스트를 읽고 공통데이터 p2~5·요구사항 p4~5 이미지를 확인했다. 추적기·DN·수신/Command/Heartbeat 계약·PayloadValidator·내부 관측 모델·관련 테스트를 대조했다. 공통데이터 p2 C의 좌표/시간 행, p3 Alarm level/alarmState 및 Command ts, p4 ACK id/result 총7곳의 차이를 확인했다. JSON 구조·선택 문자열 조건, p5 D 내부 필드표, 감시 경계·단위·개발 설정·적용 범위 보완점을 사용자에게 보고한다. MQTT 방향은 일치한다. 요구사항 HWP의 좌표 미정 목록 누락과 운행 상태 행의 상태/이유 혼재도 확인했다. 원본·제품·계약·추적기는 수정하지 않았다.
- Verification run: tracker 5기능/37항목 구조·증거 링크 PASS. 기존 COM-01~04 passing/COM-05 not_started 유지. 문서 검토로 빌드·단위·실제 Broker·GUI 시험은 재실행하지 않았다. 과거 실행 증거를 이번 실행 결과로 확대하지 않는다. HWP는 텍스트만 검사했으며 레이아웃 미검증이다.
- Evidence recorded: 이 Session Record 및 대화의 위치별 검토표. 공통데이터 PDF SHA256 BA1180AAFAE0716B0CAE758409DEA5D45020661F147C8EFFA5CF94D9A1068213, 요구사항 PDF B30FFEC36679B3DEBBC9BEEFB67952C790CE8FDDD976FF53934F2B88C9B0E879, HWP 773483E6AE1876969EBA27F07A51C76202FE4063768886BC551803C6DE36DA21. HWP는 S034에서 읽은 파일과 해시가 다르며 이번 본문을 사용했다. feature_list.json 2A448A0CE96C84FBE4912604170FE52508CF84C7CDA8994953506E0BD540C3EC, common_data_spec.md 0583C19FE0CB571ADB32AB984BCDC77BE21AA2C91DC1D8FF2DAE494F62BC63CF, interface_contract.md 4477367A6B5BA7391527F7C707EED79D742234CA4B5134345F6D07A72610015E를 대조 기준으로 기록했다. 제품 evidence는 추가하지 않는다.
- Commits: 초기 검토 단계에서는 S034 staging 승인 거부를 유지했다. 이후 사용자가 ‘집에서 하게 정리해줘 git에’라고 요청하여 이번 인수인계·관련 검토 기록·TRX의 커밋과 원격 업로드를 승인했다. 아래 종료 체크포인트에 실제 결과를 기록한다.
- Completed reporting checkpoint: 사용자 요청에 따라 common_rules_worknote.md의 일일 보고 양식(목표·수행·산출물/상태·변경·검증·미정/영향·다음 작업)으로2026-10-01 책임자 보고 문안을 대화에 준비했다. S034의 보고 체계·공통데이터 초안 작성/축약 및390건 테스트 실행과 S035의 PDF 검토를 구분했다. PDF7곳의 차이·중복 설명·빈 내부 데이터 표는 수정 대상 도출이며 정정 완료가 아니다. 신규 요구/계약 채택·제품 코드 변경 없이 초안 작성/검토로 보고하며 PDF 보완과 COM-03 구현용 기능명세를 후속으로 둔다. 외부 발송은 하지 않았다.
- Verification reporting checkpoint: 일일 보고 양식과 당일 세션 기록·현재 추적기 상태를 대조했다.390건 통과는 S034의 당일 관련 자동 테스트이며 이번 보고 작성에서 재실행하지 않는다. 실제 Broker/GUI·전체 기능 완료로 확대하지 않는다. 문서·계약·추적기 SHA256은 위 대조 기준과 동일하고 보고용 새 산출물 파일은 만들지 않았다. 보고 준비는 문서 검토이므로 제품 기준선은 재실행하지 않는다.
- Known risks: 이번 260916 요구사항 PDF는6페이지 작성본이며 최초17페이지 원문이 아니다. 개발용 채택값은 운영 확정값이 아니다. 기존 interface_contract.md ACK 방향 행의 오타도 남아 있으므로 올바른 수신 계약 및 common_data_spec.md와 함께 확인한다.
- Next best action: 사용자가 문서 수정을 요청하면 공통데이터 PDF p2~4의7곳을 정정하고 p5 D를 docs/full/common_data_spec.md 4절 및 실제 모델로 보완한다. 좌표 원점·축·범위·도착 오차와 업무 정책은 미정으로 유지한다. 제품 개발 재개 시 ./init.cmd verify부터 수행한다.
- JSON rationale checkpoint: 사용자가 UTF-8·객체·필드명·필수/중복/미정의 필드·주석/후행 쉼표·whitespace·크기/깊이·선택 필드 조건의 근거와 증명을 요청했다. RFC8259 2/4/8.1/8.3/9절과 PayloadValidator, 수신 계약, MessageValidationTests 및 CommandValidationTests를 대조했다. 표준 문법과 프로젝트 스키마 정책을 구분하고, 65,536bytes/깊이16은 계약에 명시된 개발용 자원 제한이며 정확한 수치의 최적성·운영 적정성을 입증한 근거는 이번 확인 자료에서 찾지 못했다. JSON 구조 사이 공백은 표준 허용이며 문자열 내부 공백은 필드별 조건으로 구분한다.
- Verification JSON checkpoint: `dotnet test tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj --no-restore --filter 'FullyQualifiedName~MessageValidationTests|FullyQualifiedName~CommandValidationTests' --logger 'trx;LogFileName=json-contract.trx' --results-directory evidence/documentation/S035-json-contract --verbosity minimal` 실행 결과355건 통과, 실패0/skip0. 관련 프로젝트 빌드를 포함한 단위검증이며 전체 기준선·실제 Broker·GUI 검증은 아니다. 증거는 evidence/documentation/S035-json-contract/json-contract.trx. 65,536byte 수용/65,537byte 거부·깊이17 거부와 잘못된 UTF-8·필수 필드·중복/미정의 필드·선택값 검사를 포함한다. 구현의 일치성을 보여주며 제한값의 최적성을 증명하지 않는다. 제품 코드·추적기 상태는 유지했다.
- Completed home handoff checkpoint: 사용자 Git 인수인계 요청에 따라 기존 common_rules_worknote.md 앞부분에 공통 정의/JSON 중복 통합안·규칙별 근거·5유형 정정표·내부 관측 데이터 표·집에서 pull/clone 후 이어할 순서를 기록하고 docs/README 진입점을 갱신했다. 기존 S035 보고 체크포인트도 보존했다. 저장소 밖 HWP/PDF는 복사·업로드하지 않았으며 실제 반영 완료로 표시하지 않는다. 다음 시작 지점은 내부 관측 표 작성이다.
- Verification home handoff checkpoint: 변경 문서3개 상대 링크65개 존재, Git 공백 검사, tracker5기능/37항목 및 기존 TRX355건 검증 PASS. feature_list.json/common_data_spec.md/interface_contract.md SHA256은 위 기준과 동일하다. 이번 인수인계는 문서 변경이므로 제품 테스트를 중복 실행하지 않았다. 관련 문서3개와 이번 TRX1개만 커밋 대상으로 선택한다.

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

- Goal: 사용자 오늘 계획의 보고 양식을 준비하고, 후속 설명에 맞춰 공통 계약·요구사항·기능명세를 공유 규칙 상세 정의와 연결한 뒤 현재 구현 기준 공통데이터 명세 초안을 작성한다.
- Completed: 시작 HEAD 2bec65d, 시작 작업 트리 clean. ‘기능 명세 만료’는 사용자 답변으로 ‘기능 구현 완료 보고’임을 확인했다. 이번 첨부 260916 PDF6페이지를 읽고 관련 p3~5를 이미지로 확인했다. SHA256 B30FFEC36679B3DEBBC9BEEFB67952C790CE8FDDD976FF53934F2B88C9B0E879이며 기존17페이지 PDF와 구분했다. 기존 common_rules_worknote.md에 네 양식·보고 시점·기록 위치·오늘 완료 기준을 추가했다. 단순 상세 구체화와 계약/합격 기준 변경, 명세 작성과 현재 범위 제품 검증 완료를 구분한다. 문서 수를 늘리지 않았으며 PRD·기능명세·공통 계약·제품·추적기는 유지했다.
- Completed checkpoint: 후속 작업 시작 HEAD c1124ff, 작업 트리 clean. 사용자는 공통 계약을 보고 현재6페이지 PDF에 공유 규칙을 어떻게 작성하고 이를 기준으로 기능명세를 작성할지가 핵심이라고 설명했다. 같은 메모 앞부분에서 작성 목적을 정리하고 PDF쪽별 배치, 기존11개 규칙과 공통 계약/관련 기능의 대응, 장비 식별 본문·COM-04 기능 연결 예시, 시간·Alarm의 채택/제안 구분을 추가했다. 기존 보고 계획은 보조 자료로 유지한다. PDF·공통 계약·PRD·기능명세·추적기·제품은 수정하지 않았다.
- Completed PDF checkpoint: 별도 공통데이터 v0.1 PDF3페이지(SHA256 BB23C560ADF26ADC5F94B6054634DEC7A672A42C9FBE6CAA426140FF79196710)를 읽고 본문 p2~3 이미지를 확인했다. 데이터·통신 계약은 별도 명세, 업무 공유 규칙은 요구사항 PDF, 실행 과정은 모듈 기능명세로 연결하는 검토를 같은 메모에 추가했다. 시간·좌표 설명의 혼재, ACK 방향, 사용처가 들어간 근거/상태 칸, 미작성 Alarm 등 유형별 표를 정정·보완 대상으로 기록했다. 시작 HEAD afcafdb; 기존 사용자 interface_contract.md ACK 행 미커밋 변경은 보존했다. 검토는 read-only이며 PDF를 수정·재출력하지 않았다.
- Completed common data checkpoint: 사용자는 원본17페이지 → 자신의 요구사항·공유 규칙 → 공통데이터 명세 → 구현용 기능명세로 작성하고, 과제1 설계·검증 초안을 함께 보고하는 흐름을 확인한 뒤 공통데이터 작성에 착수했다. 시작 HEAD7870cb0, 기존 사용자 interface_contract.md ACK 행 변경 보존. HWP 두 파일은 내부 ZIP/XML의 텍스트를 읽었다. 요구사항 HWP SHA25628610B1D541FCB88343B1E2B42937E8EBC7134CA110DDB70817B31378057CDC2, 과제1 검증 HWP SHA25684E7433B4D63C616C9A9B8D1FD5721CE8435219FFECBD1393638766C54B8C302. docs/full/common_data_spec.md에 표 중심0.1초안과 문서 안내를 작성했다. 식별·좌표·시간, MQTT 방향,5유형 필드, Broker/Heartbeat 관측, 개발 설정과 미정을 기존 DN-01/02/03/08/10 및 수신/Command 계약과 대조했다. 원본 HWP·PDF·제품·추적기는 수정하지 않았다.
- Scope clarification checkpoint: 작성 중 사용자는 interface_contract.md를 보고 현재 구현한 내용의 공통 정의서를 만들겠다고 범위를 구체화했다. 초안의 기준을 공통 계약의 현재 채택 부분으로 명시하고 원문/HWP는 요구 근거로 연결했다. Core/GUI/DB·운행/Alarm 업무 제안은 현재 데이터로 확정하지 않으며, Telemetry/Alarm 형식 검증·전달 지원과 실제 상태 생성·업무 구현을 구분했다. Heartbeat 수신 시각 관측은 현재 구현이고 모든 업무 메시지의 수신시각 보관은 후속 범위로 명시했다.
- Completed readability checkpoint: 사용자는 공통데이터5절의 필드표만으로 필요한 이유·사용 상황이 보이지 않는다고 지적하고 다시 작성을 요청했다. 시작 HEAD68bf8e0, 기존 사용자 interface_contract.md 변경 보존. 같은 명세서5절을 필요한 이유·생성/변경 상황·현재 사용처, Broker/Heartbeat/입력 오류 예시, 실제 필드표 순서로 다시 작성했다. 필드·자료형·enum·밀리초 단위는 기존 모델과 동일하고, 오류 정보의 경로만 실제 Error 객체의 Code/Field/Reason으로 명확히 했다. 정상 신호의 Timeout·수신 재개와 최종 Offline/운행 상태를 구분했다. 새 파일·기능·정책은 추가하지 않았다.
- Completed minimum checkpoint: 사용자 요청에 따라 같은 공통데이터 명세를 공통 항목·5개 MQTT 메시지·입력 검사·내부 관측·시험 설정과 확인 기준으로 축약했다. 반복 근거표·작성 과정·중복 설명을 줄이고 자료형·필수/선택·허용값·시각 단위·오류 처리·내부 모델 필드는 보존했다. 기존 계약과 원문/개발용 채택/미정을 구분하며 확인 항목이 과제1 전체 완료 기준을 대체하지 않음을 명시했다. 새 파일·제품 코드·기능 상태·명세 revision은 변경하지 않았다.
- Completed PDF overlap checkpoint: 사용자 공통데이터 v0.111 PDF5페이지(SHA256 BA1180AAFAE0716B0CAE758409DEA5D45020661F147C8EFFA5CF94D9A1068213)를 읽고 본문 p2~5를 렌더링·확인했다. 현재 common_data_spec.md와 같은 계약 내용이며, p2 A/C 공통 조건 및 p5 B/C 감시·오류 처리 설명이 반복된다. 유형별 필수/선택 행은 유지하고 공통 조건만 한 곳에 정의하는 축약을 제안한다. p2 C 좌표 행에 cmdId 규칙·시간 단위 행에 이력 용도가 들어간 오류, p3 Alarm level·alarmState·Command ts, p4 ACK id/result 필수 여부 오류를 확인했다. p5 D는 제목만 있고 내부 데이터 표가 없다. PDF와 명세서는 수정하지 않았다.
- Verification run: tracker5기능/37항목 구조·증거 링크 PASS, 변경 문서2개 상대 링크17개 존재·Git 공백 검사 통과. feature_list.json SHA256 2A448A0CE96C84FBE4912604170FE52508CF84C7CDA8994953506E0BD540C3EC가 작업 전후 동일하다. PDF 렌더링은 번들 pdftoppm.cmd의 경로 오류 후 실제 Library/bin/pdftoppm.exe로 성공했다. 제품 실행 경로·계약 변경이 없는 보고 기획이므로 빌드·단위·실제 Broker·GUI 검증은 미실행이다.
- Verification checkpoint: 공유 규칙 상세 구성 추가 후 tracker5기능/37항목·Git 공백 검사 PASS, 변경 문서2개의 상대 링크20개 존재 확인. 이번6페이지 PDF·공통 계약·추적기의 SHA256이 작업 전후 동일하다. 기존11개 규칙 번호·관련 기능 ID·DN-01/02/10과 P-04/06/08/16/21의 적용 상태를 대조했다. 문서의 당일 구성 안내 링크는 해당 소제목과 일치한다. 계약·제품 변경이 없어 제품 실행 시험은 추가하지 않았다.
- Verification PDF checkpoint: 새 공통데이터 PDF의 본문 p2~3 전체를 렌더링해 표와 추출 텍스트를 대조했다. tracker5기능/37항목·관련 문서 Git 공백 검사 PASS, 상대 링크20개 존재. 새 PDF·추적기 및 사용자 변경이 있는 공통 계약 SHA256이 검토 전후 동일하다. 사용자 공통 계약의 ACK 행 변경은 미커밋 상태로 보존하며 이번 기록에 포함하지 않는다. 문서 검토만 수행해 제품 실행 시험은 추가하지 않았다.
- Verification common data checkpoint: 작성한 초안·docs/README·진행 기록의 상대 링크51개 존재, Markdown 표 열 수·초안 공백·Git diff 공백 검사 PASS. 필수/선택 필드와 enum은 수신/Command 계약 및 실제 모델, Broker 기본값·관측과 Heartbeat 알림은 해당 소스 및 DN과 대조했다. tracker5기능/37항목 PASS(구조·증거 링크만). 추적기 SHA2562A448A0CE96C84FBE4912604170FE52508CF84C7CDA8994953506E0BD540C3EC와 사용자 변경 포함 interface_contract SHA2564477367A6B5BA7391527F7C707EED79D742234CA4B5134345F6D07A72610015E가 작업 전후 동일하다. 제품 계약·실행 경로를 변경하지 않은 문서 작업으로 빌드·단위·Broker·GUI 시험은 미실행이고 passing 판정·revision·제품 evidence를 변경하지 않았다. HWP의 시각적 레이아웃은 검증하지 않았고 산출물은 Markdown 초안이다.
- Verification readability checkpoint: 공통데이터 명세서의 변경은5절에만 있는 것을 HEAD와 대조했다. Broker/Heartbeat 필드·enum, 검증 오류의 Error 객체 구조, 콘솔 알림 경로를 소스와 확인했다. 명세서·문서 안내·진행 기록의 상대 링크51개, Markdown 표 열 수, Git 공백 검사와 tracker5기능/37항목 PASS. 추적기 및 기존 사용자 공통 계약 변경의 SHA256은 위 기록과 동일하다. 설명 보완만 수행해 빌드·단위·Broker·GUI 시험은 미실행이며 제품 상태·revision·evidence는 변경하지 않았다.
- Verification minimum checkpoint: `dotnet test tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj --no-restore --filter 'FullyQualifiedName~MessageValidationTests|FullyQualifiedName~CommandValidationTests|FullyQualifiedName~HeartbeatMonitorTests|FullyQualifiedName~ConnectionLifecycleTests' --verbosity minimal`에서 대상 프로젝트 빌드와390건 테스트 통과(실패0·skip0). 축약본·진행 기록의 상대 링크13개, Markdown 표 열 수, Git 공백 검사 및 tracker5기능/37항목 PASS. 추적기와 사용자 공통 계약의 SHA256은 위 기록과 동일하다. 데이터 계약과 제어 시계·연결 대역 검증이며 실제 Mosquitto·GUI·전체 솔루션 기준선은 재실행하지 않았다. 새 제품 evidence나 passing 판정으로 확대하지 않는다.
- Verification PDF overlap checkpoint: PDF 텍스트와 p2~5 표 이미지를 대조하고 수신·Command 계약에서 Alarm 허용값 및 ACK 필수/선택을 확인했다. tracker5기능/37항목 PASS. PDF·공통데이터 초안·사용자 공통 계약·추적기의 SHA256 보존 확인. read-only 문서 검토로 제품 빌드·단위·실제 Broker·GUI 시험은 추가하지 않았다. 검토 기록만 갱신하며 기존 staging 승인 거부를 유지한다.
- Evidence recorded: 이번 문서 검토는 이 Session Record에 기록한다. PDF 검토 이미지는 임시 디렉터리에 있으며 제품 evidence로 등록하지 않는다.
- Commits: f17aa225cf713dcdfab94392d67578a6dcecd7a7 — Plan requirements and feature reporting workflow. 보고 체계 초안과 진행 기록2파일. 후속 5d5bfe0935a5398116afdcb00154a07438f5cfb9 — Clarify shared rules structure for requirements PDF. 공유 규칙 상세 구성과 진행 기록2파일. 추가 45c0f63a78e8c7accd2a8160934871445d0fdb48 — Review standalone common data specification. 검증된 별도 공통데이터 PDF 검토 메모와 진행 기록2파일을 로컬 커밋했다. 사용자 interface_contract.md 변경은 포함하지 않았다. 실제 해시 기록은 후속 인수인계 커밋으로 남기며 원격 push는 하지 않았다.
- Commits common data checkpoint: 89fd2e2 — Document current communication data contracts. 검증된 현재 구현 기준 공통데이터 초안·문서 안내·진행 기록3파일을 로컬 커밋했다. 사용자 interface_contract.md 변경은 포함하지 않았고 원격 push는 하지 않았다. 실제 해시 기록은 후속 인수인계 커밋으로 남긴다.
- Commits readability checkpoint: 없음. 문서5절과 진행 기록은 저장·검토했으나 git add 승인 요청이 거부되어 staging·커밋을 실행하지 않았다. 수정본은 미커밋 상태로 보존한다.
- Commits minimum checkpoint: 없음. 앞선 git add 승인 거부를 유지해 문서 축약본과 진행 기록을 미커밋 상태로 보존한다.
- Known risks: 보고 양식은 검토용 제안이며 사수에게 발송·승인되지 않았다. 이번6페이지 PDF를 기존17페이지 명세의 교체본으로 채택하지 않았다. p5 운행 상태 행의 ‘상태’ 칸에는 필요한 이유가 들어 있어 이유와 채택 상태를 구분할 필요가 있다. COM-01~04 passing은 기존 추적기 범위이며 새 전체 명세의 모든 AC 또는 과제1 전체 통과를 뜻하지 않는다.
- Next best action: 사용자 공통데이터 v0.111 PDF의 p2 C 중복·복사 오류, p3 Alarm/Command, p4 ACK 표를 현재 common_data_spec.md와 대조해 바로잡는다. p5 D는 내부 데이터 항목을 넣거나 미작성 제목을 정리한다. 이후 COM-03부터 구현용 기능명세의 입력·처리·출력·검증 기준을 짧게 작성한다. 원본17페이지의 요구와 기존 개발용 채택·제안·미정 상태를 유지한다. 제품 구현 재개는 ./init.cmd verify와 현재 MVP 순서를 따른다.
