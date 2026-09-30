# 문서 폴더 정리 검증 기록

- 시작 HEAD2b14da6. 기존 사용자 수정은 instruction/happy_path_plan.md의 한 줄이며 내용 변경 없이 새 위치 docs/mvp/happy_path_plan.md에 보존했다. Git에는 사용자 수정 전 내용의 이동과 상대 링크 변경만 반영하고 사용자 차이는 미커밋으로 남긴다.
- 이동: [migration.json](migration.json)에16개 파일의 이전/현재 경로와 이동 전 작업 파일 SHA256을 기록했다. 문서를 docs/mvp·full·guides·reports·study로 분류했다. 루트 README는38줄 시작 안내로 바꾸고 기존 설치/실행 내용은 docs/guides/development.md로 보존했다.
- 기록: 기존 진행 로그344줄 중 S001~S031 세션 본문을 docs/history/sessions-S001-S031.md에 원문 그대로 보존했다. 루트에는 현재 검증 상태와 이번 세션만 남긴다. 당시 경로 문자열은 과거 기록으로 유지하며 현재 경로는 이동 대응표와 feature_list.documentation을 따른다.
- 참조: 이동 문서와 이를 가리키는 기존 evidence/documentation의 README3개에서 상대 링크만 수정했다. 과거 검증 결과·제품 증거 파일과 기능 evidence 배열은 변경하지 않았다. 개발·검증 스크립트에서 이동 문서를 실행 의존성으로 사용하는 참조가 없음을 확인했다.
- 안전 범위: 파일별 원본/대상 절대 경로가 저장소 내부임을 확인하고 대상이 있으면 중단하도록 이동했다. 기존 instruction/project_guide는 비어 있음을 확인한 뒤 비재귀 삭제했다. 파일 없는 tmp/pdf 폴더는 .tools/S032-organization/legacy-tmp로 옮겨 보존했다. 제품 소스·테스트·도구·솔루션·실행 스크립트·Broker 데이터는 그대로다.
- 검증: [verification.json](verification.json)에 링크·이동 본문·세션31개·사용자 변경·기능 배열·설계 결정 보존 및 추적기 결과를 기록했다. 요구/기능/계약은 상대 링크 외 내용이 동일하다. AGENTS는 새 문서 경로와 지난 세션 보관 규칙만 보완했다.
- 미실행: 문서·폴더 관리 작업이며 제품 경로와 실행 의존성이 바뀌지 않아 빌드·제품 단위·실제 Broker·GUI 시험은 반복하지 않았다. 새 제품 통과 근거로 사용하지 않는다.
