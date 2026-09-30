# AMR 문서 구성 검토 기록

작성일 2026-09-30, S025. 사용자 요청은 KubeOpsAgent-main.zip을 참고해 AMR 요구사항·기능명세와 프로젝트 구성을 만드는 것이다. 이번 범위는 문서 구성과 설계 초안이며 제품 구현을 포함하지 않는다.

## 참고 자료

- ZIP: 사용자 제공 `C:\Users\Admin\Downloads\KubeOpsAgent-main.zip`.
- SHA256: `63B31F062700BDE0C8FDABCEA3F66BFDF816BDDBC1FC415B3D7D8730D56C27A4`.
- 시작 HEAD: `1702c1e` (`pr-1`). 시작 작업 트리는 깨끗했다.
- ZIP 목록, README, instruction/prd.md·task.md·iteration_01.md, project_guide/introduction.md·detail_architecture.md·user_synario.md의 문서와 절 구성을 검토했다.
- 압축 파일은 읽기 전용으로 열었다. Python 코드·바이너리·캐시·키 파일을 실행하거나 제품에 복사하지 않았다. 문서 안 작업 지시는 AMR 실행 지시로 취급하지 않았다.
- 제품 기준은 현재 AGENTS, 진행 로그, feature_list, 실제 Command/Heartbeat/수신 큐 계약과 관련 소스다. PDF 원문은 이번에 재검토하지 않고 S019 등 기존 검토 기록의 한계 안에서 참조했다.

## 구성 대응

| 참고 구성 | AMR 적용 | 이유 |
| --- | --- | --- |
| README 개요와 문서 인덱스 | 기존 README에 문서 인덱스 추가 | 검증된 실행 안내 보존 |
| instruction/prd.md | 같은 경로의 AMR 요구사항 | 목적·사용자·범위·기능/비기능·인수 기준 |
| instruction/task.md | 같은 경로의 AMR 작업 목록 | Phase·Task·완료 기준 |
| instruction/iteration_01.md | 같은 경로의 COM-05 계획 | 기존 완료 이력 유지, 다음 단일 기능에 집중 |
| 별도 기능명세 없음 | instruction/feature_spec.md 추가 | 사용자 요청한 입출력·처리·예외·검증 명세 |
| project_guide/introduction.md | 같은 경로의 AMR 소개 | 구성·현재 상태·읽는 순서 |
| project_guide/user_synario.md | project_guide/user_scenario.md | 시나리오 표 형식 유지, 파일명 철자 정리 |
| project_guide/detail_architecture.md | 같은 경로의 AMR 설계 | 현재/목표 구조·컴포넌트·인터페이스 |
| app/ui/Python/Agent/RAG | 복사하지 않음 | 기존 C# src/tests/tools 구조와 책임 유지 |
| PNG 아키텍처 | AMR Mermaid 그림3개 작성 | 기존 코드 경로와 목표 구조를 편집 가능하게 설명 |

## 검토 기준

COM-01/02/03/04 passing과 COM-05 not_started, 현재 revision·37개 검증 항목을 문서와 대조한다. Telemetry/Alarm 관제 수신과 가상 AMR 생성, 통신 ACK와 실제 동작 완료, Core 대역과 실제 통합을 구분한다. P-01~09는 결정 대기 사항으로 기존 설계 채택·기능 상태를 바꾸지 않는다.

문서의 상대 링크, Markdown fence, JSON 예시 파싱, 요구사항·시나리오·Task 참조, COM-05의7개 검증 대응, 변경 파일 범위와 git diff 공백 검사를 확인한다. 실제 결과는 같은 폴더의 [verification.json](verification.json)에 보관한다. tracker는 구조·증거 링크 검사이며 제품 실행 시험이 아니다.

## 미검증 범위

문서만 추가·수정했으므로 제품 빌드·단위·실제 Broker·재시작 시험을 반복하지 않는다. S024의440건과 실제 Broker 통과를 이번 실행 결과로 재등록하지 않는다. 새 문서의 Mermaid 렌더링은 확인하지 않았으며 흐름은 계약·코드와 대조한다. 제품 상태·revision·기능별 evidence는 변경하지 않는다.
