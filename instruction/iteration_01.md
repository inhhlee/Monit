# AMR Command와 ACK 연계 검증 계획

Iteration01은 **COM-05 전체 검증**을 위한 개별 계획이며 현재 revision1/not_started다. 전체 개발 순서는 S027의 [정상 흐름 MVP 계획](happy_path_plan.md)을 우선한다. MVP에서 MOVE와 매핑 일부를 먼저 구현해도 아래 7항목을 모두 통과하기 전 COM-05를 passing으로 바꾸지 않는다. 기존 개발 이력을 초기화하지 않으며 이번 계획 작성에서 구현 착수·제품 검증을 수행하지 않았다.

이 계획은 현 저장소의 COM-05 재개 범위다. 신규 개발자의 전체 납품 요구는 개정0.2 [PRD](prd.md)·[기능명세](feature_spec.md)·[공통 계약](interface_contract.md)을 따른다. 아래7개는 기존 추적기 항목이며 신규 명세의 전체93개 AC나 CORE-04/INT-02를 대신하지 않는다.

## 1. 목표 시나리오

SC-002: Core 대역이 두 AMR에 서로 다른 cmdId로 요청하고, 시험 AMR이 ACK를 요청 순서와 반대로 반환해도 각각 원래 요청에 연결한다. 성공·실패·이유를 보존하고 전 단계를 cmdId로 추적한다.

## 2. 포함 범위

요청 측 ID 생성·보관, Communication 전달·보존, MOVE/STOP/RESET, 역순 ACK, 성공/실패, 불량 ACK 거부, 실제 Broker 증거와 영향 회귀를 포함한다. 기존 구현을 먼저 검토하고 부족한 코드·검증만 보완한다.

## 3. 제외 범위

실제 이동·Telemetry/Alarm 생성, 실제 Core 상태 저장·ACK Timeout, GUI/DB, 자동 재접속은 후속이다. 시험 SUCCESS를 실제 이동 완료로 표시하지 않는다. 현재 TestClient의 SIMULATION_NOT_IMPLEMENTED 응답은 유지할 수 있으며 성공/역순 시험은 별도 AMR 대역을 사용한다.

## 4. 착수 전에 기록할 결정

| 항목 | 현재 기준 또는 제안 | 처리 |
| --- | --- | --- |
| ID 생성 | 콘솔 Core 대역은 CMD- + GUID 사용 | 전체 책임을 DN-06에 채택 기록 |
| 전달 | Communication/AMR에서 ID 재생성 금지 | DN-08 유지 |
| 연결 | Core 대역이 AMR+cmdId로 원래 요청 확인 | DN-06 제안, 실제 CORE-04와 구분 |
| 전송 | MQTT5/QoS0/Retain false | 적용 범위를 DN-04/06에 기록 |
| ACK | cmdId/result/ts, 선택 id/reason | 수신 계약 유지, 새 실패 대역은 reason 포함 |
| 미요청·중복·타 AMR | 업무 상태 정책은 CORE-04 | 다른 요청에 잘못 연결하지 않는 관측과 구분 |

기존7개 검증의 순서·동작 계약이 바뀌면 revision을 올리고 이유·회귀 범위를 기록한다. 이 계획 작성만으로 DN-06 채택이나 revision 변경을 수행하지 않는다.

## 5. 작업 매핑과 구현 순서

| 순서 | Task | 산출물과 완료 기준 |
| --- | --- | --- |
| 1 | TASK-1.1 | 책임·QoS·대역 경계 기록, 현행 계약과 일치 |
| 2 | TASK-1.2 | 요청 목록·응답 대역·관측 기록, 전 단계 ID 대조 가능 |
| 3 | TASK-1.3/1.4 | 실제 명령·역순 ACK, 두 AMR·세 명령 유형·필드 보존 |
| 4 | TASK-1.5 | 성공/실패 의미 보존, 불량 Core 미전달 |
| 5 | TASK-1.6 | 회귀·증거·진행 기록,7항목과 기준선 통과 |

커밋은 계약/도구/검증·증거 등 검토 가능한 변경 단위로 구성한다. 실패를 완료한 것처럼 기록하거나 사용자 변경을 섞지 않는다.

## 6. 필수 검증 시나리오

번호는 현재 COM-05 verification_index와 일치한다. 모두 **실행 예정**이다.

| 번호 | 절차 | 기대 결과 |
| --- | --- | --- |
| 1 | 책임·QoS 기록, Core 대역 요청 생성·보관 | 요청별 고유 cmdId와 AMR 기록 |
| 2 | 실제 Broker로 MOVE/STOP/RESET 전송 | AMR 수신 ID와 생성 ID 일치 |
| 3 | 같은 ID ACK를 대상 Topic으로 반환 | AMR/cmdId/result/ts 보존, 선택 reason도 확인 |
| 4 | 두 AMR에 요청 후 ACK 순서 반전 | 각 ACK가 해당 AMR의 원래 요청에만 연결 |
| 5 | SUCCESS/FAILURE ACK 반환 | 결과 변경·유효 실패 폐기 없음 |
| 6 | cmdId 누락·잘못된 자료형 ACK 발행 | COM-04 거부·Core 미전달·원인 로그 |
| 7 | 요청부터 Core 매핑까지 대조 | cmdId별 전체 추적, 대역·미검증 범위 명시 |

QoS0에서 시험 시간 내 ACK가 없으면 실패/미완료로 남기고 원인을 조사한다. 시험 대기 제한은 환경값으로 기록하며 실제 업무 ACK Timeout으로 간주하지 않는다.

## 7. 실행 준비와 증거

먼저 [진행 기록](../claude-progress.md), 추적기의 COM-05/DN-06, [Command 계약](../src/Amr.Communication/Command/CONTRACT.md), [MqttPublisher](../src/Amr.Communication/Mqtt/MqttPublisher.cs), [IncomingMessageProcessor](../src/Amr.Communication/Validation/IncomingMessageProcessor.cs)를 읽는다.

```powershell
./init.cmd verify
./init.cmd broker
```

전용 실행 명령은 검증 도구 작성 후 기록한다. 아직 없는 명령을 현재 실행 가능한 절차로 제시하지 않는다. 도구는 tools/, 테스트·대역은 tests/, 증거는 evidence/COM-05/<실행ID>/에 둔다.

증거에 코드 버전/변경 상태, Broker·라이브러리·설정·대역 여부와 항목별 결과·경로를 기록한다. 기존 사용자 프로세스·Broker 데이터를 임의 정리하지 않는다.

## 8. 완료 체크리스트

- [ ] 착수 기준선 통과, COM-05 하나만 in_progress.
- [ ] DN-06·적용 QoS·시험 범위 기록.
- [ ] verification1~7 실제 실행 및 현재 revision 증거 등록.
- [ ] 변경 영향 있는 COM-01/03/04와 Heartbeat 경로 회귀.
- [ ] 최종 verify·tracker·변경 차이 검사 통과.
- [ ] 재시작 절차·제약·커밋 해시 기록.
- [ ] 위 조건 충족 후 COM-05 passing 판정.

## 9. 위험과 후속

Windows DLL 실행 차단은 S024 초기 재현 후 최종 검증에서 재현되지 않았다. 재발 시 환경 실패와 assertion 실패를 구분하고 증거를 보존한다. 로컬 QoS0 시험을 운영 무손실 보장으로 확대하지 않는다. 다음 작업은 정상 흐름 MVP 계획의 실제 완료 지점에 따라 선택한다.
