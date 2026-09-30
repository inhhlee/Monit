# PDF 기반 개발 명세 개정 검토 기록

S026, 2026-09-30. 시작 HEAD df6c809, 작업 트리 clean. 사용자는 기존 구현 정리가 아닌 제3의 개발자가 구현할 요구사항·기능명세를 원문 PDF와 비교하여 도출하도록 요청했다.

## 원문과 접근

원본: 사용자 Downloads의 「260917_SI생태계_요구사항 정의 및 기능명세_이인호 연구원_v0.3.pdf」,17페이지. SHA256 `99CC217588343A3CD2526EB23779DE818DDF9AF75A8516D93073474D5BA0EB72`, S019와 동일. S016의 다른 해시와 구분했다.

번들 pypdf로17페이지 텍스트를 추출하고 Poppler로17페이지 PNG를 렌더링했다. 본문·표·도식·화면이 있는 p2~17을 시각 검토하고 텍스트와 대조했다. 작업용 결과는 Git 제외 .tools/S026-pdf-review/에 있으며 원본 PDF·그림·전체 추출 텍스트를 제품 저장소에 복제하지 않았다. 문서 판독용 번들 도구 사용이며 제품 실행/검증에 Python 의존성을 추가하지 않았다.

## 주요 도출

원문 표23기능(COM5/AMR5/GUI4/CORE5/INT4)과 p15 도식 CORE-06을24개 기능명세로 연결했다. 공통11·기능24·비기능8의43개 요구사항, 미실행 수용 시험93개를 정의했다. 원문 누락·충돌·제안은 P-01~21과 기능별 출처 페이지로 기록했다. 현재 제품 상태는 별도 비교표로 분리했다.

전달 문서는 instruction/prd.md·feature_spec.md·interface_contract.md·pdf_comparison.md. README와 기존 project_guide/task/iteration에는 신규 개발 목표와 현재 저장소 안내의 차이를 표시했다. feature_list에는 DN-11 proposed와 문서 검토 change_log만 추가했다. 기존 DN-01~10과 제품5기능의 계약·상태·revision·verification·evidence는 보존했다.

## 검증

원문 기능 ID→PRD→기능명세→AC를 [traceability.json](traceability.json)으로 연결한다. 모든 AC result는 not_run이며 제품 증거가 아니다. 문서 검증 결과는 [verification.json](verification.json)에 보관한다.

검사 대상은 원문23개 기능 포함 여부, 도식 CORE-06 명시,24개 명세/43개 요구/93개 AC 유일성·연결, P-01~21 결정표, 링크·JSON·Markdown fence, 기존 제품 기능 배열과 DN 보존, tracker와 diff 공백이다. 상세 기대 동작은 본문·표·도식을 수동 대조했다. 숫자·시간·등록·상태 정책이 미정인 경우 제안/결정 필요로 표시했다.

최초 검사는 PDF 추출 텍스트의 ID 뒤에 한글이 붙어 Unicode 단어 경계 정규식이23개 중2개만 찾았고, 생성 Markdown2파일의 EOF 빈 줄도 검출했다. verification-attempt-01.json에 실패를 보존했다. ID 패턴을 명시해 수정하고 EOF만 정리한 뒤 해당 검사를 재실행하여 원문23개 전체 대응과 diff 검사 통과를 확인했다. 기대 기능 수를 줄이거나 원문 요구를 삭제하지 않았다. 나머지 성공 검사는 의미 변경이 없어 결과를 유지했다.

## 미검증과 제한

문서/설계 제안만 바뀌어 제품 빌드·단위·실제 Broker·재시작 시험은 반복하지 않았다. 이전 S024의 통과를 새 제품 실행으로 주장하지 않는다. 이번 신규 명세의93개 AC는 모두 미실행이다. 원문 해석 충돌을 임의 운영 정책으로 확정하지 않았으므로 관련 결정 전에는 최종 개발 기준이 완전히 확정된 문서로 볼 수 없다. 최종 산출물은 편집 가능한 Markdown이며 새 PDF/DOCX 출력은 생성하지 않았다.
