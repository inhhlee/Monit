# 단계별 개발 과제 PDF 비교 기록

- 검토일: 2026-09-30, 시작 HEAD86ebd01. 기존 사용자 변경 instruction/happy_path_plan.md를 읽기만 하고 보존했다.
- 출처: Downloads의 가상 AMR_관제 시스템 단계별 개발 과제 구성.pdf. 표지 날짜2026-09-04,3페이지, SHA256 222047958AF67E8F10510FF91C5CAA15AF54D2E470E7544C5E7B44B19D9DB366. S026의17페이지 요구사항 PDF와 다른 문서다.
- 방법: pypdf 추출은 한글 문자 매핑이 깨져 근거로 사용하지 않았다. 번들 Poppler로3페이지를 렌더링하고 전 페이지를 직접 확인했다. 렌더링은 Git 제외 .tools/S029-assignment-pdf/에 보관했다. 원본 PDF 수정·재출력은 하지 않았다.
- p2: Windows Application 기반, 가상 AMR·통신·Core·GUI·SQLite의 역할과 입출력이 있다. 재접속·장비 등록·Alarm·명령·이력·Dashboard·Map·조건별 DB 조회는 전체 목표에 이미 대응한다. 개발환경 목록에는 Visual Studio2026, Git/TortoiseGit, Docker Mosquitto, MQTT Explorer/Wireshark, SQLite/DB Browser, Seq, LiveCharts2, 개발 지원 도구가 있다. 이 목록을 새 설치 실행 지시로 취급하지 않았다.
- p3: 공통 요구/명세와 과제1통신·2시뮬레이터·3GUI·4Core·5통합으로 구성한다. 통신 과제에 재접속이 있고, GUI 산출물에 Mock AMR 화면과 DataProvider 인터페이스가 명시돼 있다. 시뮬레이터 수량·초기 좌표·속도·이동 경로·장애 시나리오는 설정 파일로 바꿀 수 있어야 한다. Core는 GUI와 통신 사이의 업무 서비스이며 검증 가능한 라이브러리다.
- 비교 결론: 현재 PRD/기능명세는 큰 기능 영역을 포함한다. DataProvider라는 산출물 이름은 명시되지 않았지만 Mock/실제 공통 논리 인터페이스와 공급자 교체 개념은 있다. GUI 독립 Mock 실행과 교체 가능한 인터페이스를 납품 체크 항목으로 명확히 하는 것이 보완점이다. 개발환경 전체 목록은 현재 최소 실행 README와 일대일 대응 표가 없다. 운행 이력은 이 PDF에 구체적인 데이터 정의가 없고 기존 명세는 업무 이벤트 이력 중심이므로 연속 좌표 저장 요구로 임의 확대하지 않는다.
- PDF에 없는 구체화: COM/AMR/GUI/CORE/INT24개 ID,93개 AC, 정확한 JSON/Topic/상태 코드와 ACK 완료 정책은 이3페이지에 없다. 기존17페이지 상세 PDF·설계 결정에서 온 내용과 구분해야 한다.2대·3일·1차DB제외·직선MOVE·AMR당1요청·주기값·Canvas/ConcurrentDictionary는 MVP의 시험 범위/구현 제안이며 이 PDF 지정값이 아니다. WPF도 이 파일은 Windows Application까지만 명시하므로 기존 기술 선택으로 구분한다.
- 순서 차이: 원문 과제1에 있는 재접속과 최종 SQLite/Alarm 등을 MVP 이후로 미룬 것은 사용자와 합의한 개발 순서다.1차시연을 과제1~5 전체 완료나 원문 범위 변경 승인으로 표현하지 않는다.
- 검증 경계: 비교 검토만 수행했다. 제품 코드·요구사항·계획·추적기 상태/계약/증거를 변경하지 않았다. 제품 빌드·단위·Broker·GUI 시험 미실행. 종료 추적기 구조 검사만 실행한다.
