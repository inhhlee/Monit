# AMR 관제 시스템 프로젝트 소개

여러 가상 AMR의 생존·위치·상태·장애를 관찰하고 명령과 결과를 추적하는 Windows/.NET 기반 학습·개발 시스템이다. 통신 기반을 검증한 뒤 가상 AMR, Core, GUI와 저장소를 단계적으로 연결한다.

이 소개의 구현 상태는 기존 저장소 안내다. 다른 개발자에게 전달할 전체 제품 요구는 PDF를 직접 대조한 [요구사항0.3](../instruction/prd.md)·[기능명세0.3](../instruction/feature_spec.md)·[공통 계약](../instruction/interface_contract.md)·[원문 비교서](../instruction/pdf_comparison.md)를 사용한다. 미구현 항목도 전체 납품 범위에 포함된다.

## 1. 배경과 문제

MQTT 송수신 외에도 입력 검증, 장비 간 독립 처리, 요청·결과 연결, 실제 상태 생성과 화면 표시가 필요하다. 각 단계는 별도 기능·증거로 관리한다.

## 2. 사용자와 목표

관제 사용자는 상황을 파악하고 명령 결과를 확인한다. 개발자는 계약과 책임에 따라 구현하고, 검증 담당자는 정상·장애·복구를 재현한다. 학습자는 콘솔 실습부터 전체 흐름을 익힌다.

## 3. 현재 상태

| 영역 | 현재 상태 |
| --- | --- |
| Broker 연결·감시 | COM-03 passing |
| 네 유형 입력 검사 | COM-04 passing |
| 송수신·AMR별 큐 | COM-01 passing |
| Heartbeat Timeout·복구 | COM-02 passing |
| Command/ACK 전체 연결 | 기본 왕복 존재, COM-05 not_started |
| Telemetry·Alarm | 관제 수신/검증 구현, 가상 AMR 생성 미구현 |
| 가상 AMR 이동·장애·복구 | 미구현, 정상 명령도 실패 ACK |
| 자동 복구·실제 Core·GUI·DB 통합 | 후속. WPF는 실험용 |

S024에서 자동 테스트440건과 실제 Broker 시나리오가 통과했다. 당시 작업 트리의 기록이며 이번 문서 작성의 재실행 결과가 아니다. 최신 상태는 [진행 기록](../claude-progress.md)과 [추적기](../feature_list.json)를 따른다.

## 4. 목표 범위

전체 목표는 가상 AMR2대의 설정·이동·Telemetry·Alarm, Core·SQLite·GUI 통합과 통신 복구다. 실행 순서는 [정상 흐름 MVP 계획](../instruction/happy_path_plan.md)에 따라 임시 마커 → 실제 Telemetry/Core → 실제 마커 → MOVE/도착 ACK → 통합 시연을 우선하고 DB·복구·장애는 2차로 확장한다. 물리 로봇과 고급 경로 탐색·충돌 회피는 범위 밖이다. 전체 요구와 결정 대기 사항은 [PRD](../instruction/prd.md)에 유지한다.

## 5. 데이터 흐름

AMR이 Heartbeat/Telemetry/Alarm/ACK를 생성하고 Communication이 검사한다. Core는 정상 데이터와 감시 이벤트로 업무 상태를 관리한다. 명령은 GUI → Core → Communication → AMR, 결과는 반대 방향으로 전달한다.

## 6. 기술

C#/.NET·MQTTnet·Mosquitto는 기존 구현 기반이다. Windows/WPF는 화면 실험 환경이며 SQLite는 후속 저장소다. 실행·검증에 Python이 필요하지 않다. 참고 ZIP의 AI Agent 기술을 AMR에 도입하지 않는다.

## 7. 프로젝트 구성

```text
프로젝트 루트/
├── README.md                         실행과 문서 진입점
├── AGENTS.md                         작업·검증 규칙
├── feature_list.json                 상태·계약·증거 기준
├── claude-progress.md                진행 기록
├── instruction/
│   ├── README.md                     Happy Path와 원본 전체 문서 읽기 안내
│   ├── prd.md                        요구사항 정의서
│   ├── feature_spec.md               기능명세서
│   ├── interface_contract.md         공통 데이터·인터페이스
│   ├── pdf_comparison.md             원문 비교·결정대장
│   ├── task.md                       개발 작업
│   ├── happy_path_plan.md            1차 MVP의 주 개발 문서
│   ├── mvp_review_summary.md         사수 검토용 범위·일정 요약
│   ├── assignment_mapping.md         과제별 산출물과 GUI 공급자 검증 기준
│   └── iteration_01.md               COM-05 전체 검증 계획
├── project_guide/
│   ├── introduction.md               소개
│   ├── user_scenario.md              사용자 시나리오
│   └── detail_architecture.md        상세 설계
├── src/                              기존 제품·실험 소스
├── tests/                            테스트·대역
├── tools/                            실행·검증 도구
├── evidence/                         실행 증거
└── AmrControlSystem.slnx              기존 솔루션
```

Core/저장소 프로젝트의 이름·배치는 해당 단계에서 결정한다. 빈 제품 폴더를 만들어 구현 상태와 혼동하지 않는다.

## 8. 읽는 순서

읽는 경로는 [문서 읽기 안내](../instruction/README.md)에서 선택한다. 현재 개발은 **진행 기록 → Happy Path 계획 → 해당 단계의 기능명세·계약** 순서다. 최종 범위 검토는 **과제 대응표 → PRD → 전체 기능명세 → 공통 계약·PDF 비교서** 순서다. 사수 보고 자료와 진행 상태 문서는 별도로 구분했다. 전체 문서를 매일 다시 읽을 필요는 없으며 실제 구현 재개는 AGENTS에 따라 진행 로그부터 확인한다.

## 9. 실행과 검증

선행 조건과 실제 명령은 [루트 README](../README.md)를 따른다. init.cmd verify는 표준 검증, init.cmd broker는 Broker 준비, init.cmd start는 연결 실습이다. Telemetry·Alarm 생성이나 GUI 통합을 실행하는 명령이 아니다.

## 10. 기대 결과와 제약

기능별 책임·입출력·예외·인수 기준을 확인하고 실패를 재현하며 다음 세션에서 같은 기준으로 이어가는 프로젝트를 만든다. 운영 성능·보안·외부 호환성·운행 정책은 개발 기본값과 구분해 확정한다.
