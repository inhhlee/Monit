# AMR 개발 문서 읽기 안내

공통 규칙 작성에 이어서 착수할 때는 [공통 규칙 작성 재개 메모](guides/common_rules_worknote.md)를 먼저 읽는다. 초안/채택 상태와 기본/상세 수준을 구분하는 기준, 세 항목의 예시와 다음 작업 순서를 담았다.

현재 구현에 적용한 공통 정의를 문서화할 때는 [공통데이터 명세서 초안](full/common_data_spec.md)을 읽는다. interface_contract의 기존 결정을 추려 과제 1의 식별·시간·다섯 메시지·통신 관측·개발 설정을 표로 정리했으며 원문 기준·개발용 채택·미정을 구분한다. 후속 구현용 기능명세는 이 표를 참조한다.

## 폴더별 문서

| 폴더 | 내용 | 먼저 열 파일 |
| --- | --- | --- |
| `mvp/` | 지금 구현할 Happy Path | [개발 계획](mvp/happy_path_plan.md) |
| `full/` | 원본 전체 범위·기능·계약·잔여 작업 | [PRD](full/prd.md) · [과제 대응표](full/assignment_mapping.md) |
| `guides/` | 실행 환경·프로젝트 소개·설계·시나리오 | [실행 가이드](guides/development.md) |
| `reports/` | 사수 검토·보고 자료 | [검토 요약](reports/mvp_review_summary.md) |
| `study/` | 이전 코드 학습 자료 | [학습 안내](study/Studyreadme.md) · [통신 학습](study/study2.md) |
| `history/` | 지난 세션 기록 | [S001~S031 기록](history/sessions-S001-S031.md) |

현재 진행 상태는 루트 [claude-progress.md](../claude-progress.md), 검증 원본은 루트 `evidence/`에 둔다. 학습 문서는 작성 당시 설명이므로 최신 기능 상태는 진행 기록과 추적기를 따른다.

**지금 개발할 때는 Happy Path, 최종 납품 범위를 확인할 때는 원본 전체 범위**를 읽는다. 서로 다른 프로젝트가 아니라 같은 프로젝트의1차 목표와 최종 목표다. 원문 PDF에서 도출한 요구와 추가 설계 제안은 구분한다.

매일 모든 문서를 다시 읽지 않는다. [진행 기록](../claude-progress.md)의 현재 상태와 마지막 세션을 확인한 뒤, 아래에서 필요한 경로 하나를 선택한다. 아직 실제 MVP 구현은 시작하지 않았으며 문서 작성·검토를 제품 완료로 보지 않는다.

## Happy Path를 개발할 때

목표는 **AMR2대 → 실제 MQTT 좌표 → Core → WPF 마커 → MOVE → 도착 ACK**다.1차에는 DB·Alarm·STOP/RESET·자동 재접속·업무 Timeout을 새로 구현하지 않는다. 이미 완료된 검증·큐·Heartbeat는 재사용한다.

| 순서 | 읽을 문서 | 지금 읽을 부분 |
| --- | --- | --- |
| 1 필수 | [1차 MVP 개발 계획](mvp/happy_path_plan.md) | 3절의1차 범위,4절에서 오늘 할 M1 단계,5절의 시연 기준 |
| 2 해당 단계만 | [기능명세서](full/feature_spec.md) | 아래 단계별 표에 해당하는 기능의1차 부분만 읽기 |
| 3 연결할 때 | [공통 계약](full/interface_contract.md) | Telemetry·MOVE·ACK, Core/GUI 데이터와 DataProvider 경계 |
| 4 기존 통신과 연결할 때 | [수신 계약](../src/Amr.Communication/Validation/CONTRACT.md) · [Command 계약](../src/Amr.Communication/Command/CONTRACT.md) | 현재 채택된 Topic·필드·자료형·검증 규칙 |
| 5 GUI 확인할 때 | [과제 대응표](full/assignment_mapping.md) | 2절의 Mock 단독 실행·공급자 교체·실제 시연 D-GUI-01~03 |

오늘 작업은 다음 표에서 한 줄을 골라 진행한다. 각 기능명세의 전체 예외·DB·장애 처리까지 한꺼번에 구현하지 않는다. 구체적인 종료 조건은 MVP 계획을 따른다.

| 현재 단계 | 필요한 기능명세 또는 문서 | 여기까지만 확인 |
| --- | --- | --- |
| M1-00 기준선 | [실행 안내](guides/development.md), 진행 기록 | 기존 통신과 빌드·검증 경로가 정상 |
| M1-01 임시 마커 | GUI-02, 과제 대응표 D-GUI-01 | Mock 모드에서 마커2개·ID·선택 표시 |
| M1-02 Telemetry/Core | AMR-01·AMR-05·CORE-02 | 실제2대 Telemetry를 Core가 별도로 저장·조회 |
| M1-03 실제 마커 | GUI-02, 공통 계약의 DataProvider, D-GUI-02 | Mock을 실제 Core로 교체해 수신 좌표 표시 |
| M1-04 MOVE/ACK | GUI-04·CORE-04·COM-05·AMR-02·AMR-04 | 화면 요청→직선 이동→도착 ACK→요청 결과 |
| M1-05 시연 | MVP의 M1-AC-01~06, D-GUI-03, INT-01/02의 정상 흐름 | 실제2대 독립 동작·재시작·증거 확인 |

현재 단계가 끝나면 진행 상태와 검증 결과를 남기고 다음 단계로 이동한다. M1 시연 완료는24개 전체 기능의 완료가 아니다. COM-05 전체 passing은 별도의7개 검증을 모두 충족해야 한다.

## 원본 전체 범위를 확인할 때

목표는 **통신·시뮬레이터·GUI·Core·SQLite와 정상/장애/복구 통합까지 완성**하는 것이다. 아래 문서는 원본 PDF 자체가 아니라, 원본을 근거로 작성한 전체 개발 명세와 안내다. 원문에 없는 제안·미정 정책은 PDF 비교서와 설계 결정에서 확인한다.

| 순서 | 읽을 문서 | 확인할 내용 |
| --- | --- | --- |
| 1 과제 구조 | [과제별 산출물 대응표](full/assignment_mapping.md) | 3페이지 과제 PDF의 공통·과제1~5 결과물과 현재 기능의 대응 |
| 2 전체 목표 | [요구사항 정의서](full/prd.md) | 최종 제품에 필요한 범위·책임·수용 결과 |
| 3 기능 상세 | [기능명세서](full/feature_spec.md) | 24개 기능의 입력·처리·출력·예외·93개 수용 기준 |
| 4 데이터 초안 작성 | [공통데이터 명세서 초안](full/common_data_spec.md) | 공통 계약의 기존 결정에서 과제 1 필드·자료형·필수 여부·허용값을 추려 작성 |
| 5 연결 규칙 | [공통 데이터와 인터페이스](full/interface_contract.md) | 메시지·상태·Core/GUI·저장·조회 계약 및 제안 |
| 6 원문 대조 | [PDF 비교와 결정대장](full/pdf_comparison.md) | 17페이지 상세 PDF의 근거·충돌·도출·P-01~21 정책 |
| 7 흐름과 구조 | [사용자 시나리오](guides/user_scenario.md) · [상세 설계](guides/detail_architecture.md) | 모듈 책임과 정상·명령·장애·복구 흐름 |
| 8 잔여 작업 | [전체 개발 작업 목록](full/task.md) | 기존 TASK별 전체 완료 기준과1차 이후 남는 작업 |
| 9 COM-05를 마칠 때 | [COM-05 전체 검증 계획](full/iteration_01.md) | 식별자·역순 응답·실패/불량 ACK 등 기존7개 검증 |

작업 목록의 Phase 번호는 기존 작업 묶음이다. 전체 기능을1번부터 순서대로 완벽하게 구현하라는 뜻이 아니며, 현재 실행 순서는 Happy Path 계획을 따른다. 원본 과제1에는 재접속이 포함되므로 COM-01~04 passing만으로 과제1 전체 완료를 보고하지 않는다.

## 진행 상태와 보고 자료

| 필요한 것 | 문서 | 사용 방법 |
| --- | --- | --- |
| 현재 어디까지 했는지 | [진행 기록](../claude-progress.md) | 맨 위 현재 검증 상태와 마지막 세션만 먼저 확인 |
| 기능별 공식 상태·근거 | [기능 추적기](../feature_list.json) | 계획·구현·passing과 revision·증거를 구분하는 기준 |
| 설치·실행·검증 명령 | [실행 가이드](guides/development.md) | 새 환경 준비 또는 실제 실행 시 사용 |
| 작업 규칙 | [AGENTS](../AGENTS.md) | 착수·단일 기능·검증·변경·인수인계 절차 |
| 프로젝트 개요 | [프로젝트 소개](guides/introduction.md) | 처음 구조와 목적을 파악할 때 |
| 사수님께 보낼 요약 | [1차 MVP 검토 요약](reports/mvp_review_summary.md) | 목표·1차/2차·일정, 마지막 절의 확인 메시지 초안 |
| 사수님께 범위 설명 | [과제 대응표](full/assignment_mapping.md) + [PRD](full/prd.md) | 원본 결과물과 최종 범위가 유지됨을 설명할 때 |

사수 확인 항목은 재접속 개발 시점과 운행 이력의 좌표/경로 저장 포함 여부다. 메시지 초안 작성과 실제 발송·승인은 구분한다.
