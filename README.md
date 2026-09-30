# AMR 관제 시스템

가상 AMR의 통신·이동·관제를 개발하는 Windows/.NET 프로젝트다. 지금은 **두 AMR의 좌표를 WPF 마커로 표시하고 MOVE와 도착 ACK까지 연결하는 Happy Path**를 먼저 구현할 계획이다.

| 할 일 | 열 문서 |
| --- | --- |
| 지금 개발할 내용 | [MVP 개발 계획](docs/mvp/happy_path_plan.md) |
| 현재 진행 상태 | [진행 기록](claude-progress.md) |
| 원본 전체 요구 확인 | [전체 요구사항](docs/full/prd.md) · [과제 대응표](docs/full/assignment_mapping.md) |
| 설치·실행·검증 | [개발 환경과 실행 가이드](docs/guides/development.md) |
| 사수님께 보고 | [MVP 검토 요약](docs/reports/mvp_review_summary.md) |
| 문서 전체 위치 | [문서 안내](docs/README.md) |

## 폴더 구분

```text
docs/          문서: mvp · full · guides · reports · study · history
src/           제품 코드
tests/         자동 테스트와 테스트 대역
tools/         실행·검증 도구
docker/        Broker·Seq 개발 환경
evidence/      검증 결과와 실행 증거
.tools/        로컬 임시 작업 자료 (Git 제외)
```

루트에는 솔루션·시작 스크립트·작업 규칙·현재 진행 기록·기능 추적기를 둔다. Happy Path와 최종 제품은 같은 솔루션을 사용한다. 현재 기능 상태·revision·증거는 [feature_list.json](feature_list.json)이 기준이다.

## 빠른 실행

Windows, .NET SDK10.0.401, PowerShell7.4+, Git이 필요하다. Broker는 Docker Desktop을 실행한 뒤 준비한다. 프로젝트 루트에서 다음 명령을 사용한다.

```powershell
./init.cmd verify
./init.cmd broker
./init.cmd start
```

`start`는 현재 통신 콘솔을 실행한다. 실제 AMR 이동·관제 GUI 통합은 아직 미구현이다. 전체 설치·AMR2대 실행·통신 실습은 [실행 가이드](docs/guides/development.md)를 따른다.
