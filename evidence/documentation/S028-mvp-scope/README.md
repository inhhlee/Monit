# 1차 MVP 범위 정리 검토 기록

- 시작: e817ac3, 작업 트리 clean. 사용자 요청에 따라 문서 전체의 1차/2차 범위를 일치시켰다.
- 변경: 마커 표시를 1차에 포함하고 SQLite를 2차로 이동. 현재 계획은 [MVP 개발 계획0.3](../../../instruction/happy_path_plan.md)의 M1-00~05와 M1-AC-01~06이다. [사수 검토 요약](../../../instruction/mvp_review_summary.md)은 발송하지 않은 초안이다.
- 보존: 전체24기능의 기능 본문·93AC는 시작 커밋과 동일하다. feature_list의 기존5기능 배열과 DN-01~12도 동일하다. DN-13과 변경 이력만 추가했다. S027의 HP 단계·증거는 해당 시점 기록으로 보존하며 새 시연 기준에 소급 적용하지 않는다.
- 검증: [verification.json](verification.json)에 Markdown 링크,24기능 배치·6단계·6시연 기준, 전체 AC 수, 기존 내용 보존, 추적기 검사 결과를 기록했다. git diff 공백 검사 통과.
- 추적기 명령: `dotnet tools/Amr.Verification/bin/Debug/net10.0/Amr.Verification.dll tracker feature_list.json`. 결과 PASS:5기능/37항목, 구조·증거 경로 검사에 한정한다.
- 미실행: 문서 작업이므로 제품 빌드·단위·실제 Broker·GUI·재시작은 실행하지 않았다. M1 시연은 전부 not_run이며 제품 기능 passing 근거가 아니다. 기존 제품 결과는 S024를 참조한다. 공수는 실측이 아닌 조건부 추정이다.
