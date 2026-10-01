# AMR 현재 구현 기준 공통데이터 명세서 초안

버전 0.1 · 2026-10-01 · 검토용 초안

현재 Communication이 검증·전달하는 데이터를 생산자와 소비자가 같은 의미로 사용할 수 있도록 정리한 명세다. [공통 계약](interface_contract.md)에서 기존 결정에 해당하는 식별·시간·MQTT 메시지·통신 관측·설정을 추려, 채택된 수신·Command·Heartbeat 계약과 대조한다. 기능별 처리 순서와 Alarm 업무 규칙은 기능명세에서 작성한다.

Telemetry·Alarm의 형식 검증과 전달은 지원한다. 실제 AMR의 이동·Alarm 발생·운행 상태 전이, Core·GUI·SQLite 업무 기능의 구현 완료를 뜻하지 않는다. Command/ACK 기본 왕복과 전체 COM-05 검증 완료도 구분한다.

## 1 작성 기준과 적용 상태

| 기준 자료 | 이 문서에서 사용하는 내용 |
| --- | --- |
| [interface_contract.md](interface_contract.md) | 현재 구현에 적용된 공통 정의를 찾는 기준. 원문·기존 결정·신규 제안이 혼재하므로 적용 상태를 확인하고 아래 범위만 추린다. |
| 원본 17페이지 요구사항 정의 및 기능명세 v0.3 | 장비 식별·상태·시간·메시지·Command/ACK의 전체 요구. 원문 쪽수와 구분은 [PDF 비교서](pdf_comparison.md)를 따른다. |
| 2026-10-01 작성 요구사항 정의서 HWP 초안 | 공유 규칙 1~11과 필요한 이유. 특히 식별·시간·상태·메시지·명령 연결·통신 감시 기준을 데이터 항목으로 구체화한다. |
| 과제 1 검증 계획서 HWP 초안 | 적용 범위와 검증 설명을 연결하는 자료. 현재 결과를 근거로 원본 요구나 데이터 계약을 임의 변경하지 않는다. |
| [기능 추적기의 설계 결정](../../feature_list.json) 및 기존 계약 | DN-01/02/03/08/10의 개발용 채택값을 계승한다. 업무 정책과 신규 제안은 [공통 계약](interface_contract.md)에서 구분한다. |

**원문 기준**은 원본에 명시된 의미·값이다. **개발용 채택**은 현재 개발에 적용한 임시 결정이며 운영 확정값이 아니다. **미정**은 값이나 의미를 추가로 결정해야 하는 항목이다. 이 문서 자체는 초안이며 기존 계약을 개정하거나 새 정책을 채택하지 않는다.

| 공통 계약에서 읽을 절 | 현재 구현 기준으로 작성할 부분 |
| --- | --- |
| 1 식별과 시간 | 2절의 AMR ID·cmdId·ts·수신 시각·경과 시간 |
| 2 Topic·3 JSON·4 유형별 필드 | 3~4절의 송수신 방향·자료형·필수 여부·허용값 |
| 11 설정과 시험 프로파일 | 6절의 현재 개발 설정. 원문 요구·미정 프로파일 전체를 채택하지 않음 |
| Broker/Heartbeat의 채택 계약과 관측 모델 | 5절의 내부 통신 관측 데이터. 외부 메시지와 분리 |
| 5 운행·6 Alarm·7 Core/GUI·8 명령 업무·9 저장·10 조회 | 미구현 처리와 제안은 6절의 후속 항목으로 연결. 현재 확정 데이터로 추가하지 않음 |

## 2 식별 좌표와 시간

근거: 원본 p3~6, 요구사항 공유 규칙 1·2·3·8, DN-02/08/10. 자료형은 JSON 표현과 C#에서 보존할 형식을 함께 적는다.

| 항목 | 의미·자료형 | 공통 조건 | 근거·상태 |
| --- | --- | --- | --- |
| AMR ID | 동일 장비를 구분하는 문자열 | 현재 Topic·Payload·전달 데이터에서 같은 값 사용. 후속 설정·Core·GUI·이력도 동일 ID 사용. 빈 값·공백·제어문자·`/`·`+`·`#` 불허. 대소문자 구분, 자동 정규화 없음 | 일관된 식별은 원문 기준, 문자열 제한은 DN-02 개발용 채택 |
| cmdId | 명령 요청 식별 문자열 | 요청 측에서 한 번 생성하고 전달·ACK에서 보존. 빈 값·공백뿐인 값·null 불허. 형식 접두사 강제 없음 | 요청 연결은 원문 기준, 현재 생성·전달 방식은 DN-08 개발용 채택 |
| ts | 메시지 생산자가 기록한 시각. JSON 정수 / signed Int64 | Unix Timestamp. 현재 단위는 밀리초. 문자열·소수·지수 표기 및 단위 자동 추측 불허. Int64 전체 범위 보존 | Int64는 원문 기준, 밀리초는 DN-02 개발용 채택 |
| 관제 수신 시각 | 현재 Communication에서 Heartbeat 수신을 관측한 시각. Int64 | UTC Unix 밀리초. Payload의 ts와 구분하며 MQTT Payload에 새 필드로 추가하지 않음. 모든 업무 메시지의 수신 시각 보관은 후속 Core 계약에서 정함 | 공유 규칙 3, Heartbeat 관측은 DN-10 개발용 채택 |
| 경과 시간 | 동일 관제 프로세스에서 측정한 시간 간격 | Heartbeat 감시는 단조 시계 사용. 원격 ts 차이로 Timeout을 계산하지 않음. 명령 업무 Timeout 기준은 별도 미정 | Heartbeat는 DN-10 개발용 채택, 명령은 P-08 미정 |
| x / y | 2D 위치 또는 이동 목표. JSON 숫자 / 유한 double | 문자열·null·비유한 값 불허. 현재 단위 m. 원점·축 방향·세계 좌표 범위는 미정 | X/Y는 원문 기준, 유한값·m는 DN-02 개발용 채택, 좌표계는 P-07 미정 |
| sp | 현재 속도. JSON 숫자 / 유한 double | 0 이상. 현재 단위 m/s. 위치와 구분해 전달 | 위치·속도 분리는 원문 기준, 유한값·단위는 DN-02 개발용 채택 |

## 3 MQTT Topic과 메시지 공통 조건

근거: 원본 p6, 공유 규칙 5·6·7, [수신 계약 v3](../../src/Amr.Communication/Validation/CONTRACT.md), [Command 계약 v1](../../src/Amr.Communication/Command/CONTRACT.md).

| 메시지 | Topic | 발행 → 수신 |
| --- | --- | --- |
| Telemetry | `amr/{amrId}/telemetry` | 가상 AMR → Communication |
| Alarm | `amr/{amrId}/alarm` | 가상 AMR → Communication |
| Heartbeat | `amr/{amrId}/heartbeat` | 가상 AMR → Communication |
| Command | `amr/{amrId}/command` | Communication → 대상 가상 AMR |
| ACK | `amr/{amrId}/ack` | 가상 AMR → Communication |

Topic과 필드명·허용 문자열은 대소문자를 구분한다. Payload에 id가 있으면 Topic의 AMR ID와 정확히 일치해야 한다. Command와 id 없는 ACK의 대상 AMR은 Topic에서 식별한다. 과제 1의 현재 계약은 운행 상태를 Telemetry.state로 전달하며 별도 `/state` Topic을 정의하지 않는다.

| 구분 | 데이터 조건 | 적용 상태 |
| --- | --- | --- |
| 기본 형식 | strict UTF-8 JSON object. 주석·후행 쉼표·중복 키·정의되지 않은 필드 불허 | DN-02/08 개발용 채택 |
| 필수·선택 | 필수 필드는 존재해야 함. 선택 필드는 생략 가능하며, 존재하면 자료형·값 조건 적용 | DN-02/08 개발용 채택 |
| 문자열 | id/cmdId/code 및 선택 message/reason은 null·빈 값·공백뿐인 값 불허 | DN-02/08 개발용 채택 |
| 자원 제한 | Payload 최대 65,536bytes, JSON 최대 깊이 16 | 개발용 채택, 운영 확정값 아님 |
| 메시지 구분 | Topic과 유형별 구조로 식별. 공통 type 필드는 없고 Command에만 type 사용 | DN-02/08 개발용 채택 |

## 4 유형별 데이터

### 4.1 Telemetry

근거: 원본 p3~4·6, 공유 규칙 2·4·5, DN-02 개발용 수신 계약.

| 필드 | 자료형 | 필수 | 의미·허용값 |
| --- | --- | --- | --- |
| id | string | 예 | 발행 AMR ID, Topic ID와 동일 |
| ts | Int64 정수 | 예 | 위치·속도·상태 스냅샷의 생성 시각 |
| pos | object | 예 | x/y 두 필드로 구성된 현재 위치 |
| pos.x / pos.y | 유한 double | 둘 다 예 | 세계 좌표상의 X/Y. 단위·범위는 2절 참조 |
| sp | 유한 double | 예 | 현재 속도, 0 이상 |
| state | string | 예 | IDLE / BUSY / STOPPED / FAULT |

운행 상태 네 값은 원본 p4 기준이다. 대기·동작 수행·정지·장애를 구분하며, 이동 허용 조건과 State 전이는 AMR 기능명세에서 정한다. 속도와 시각을 pos 내부에 넣지 않는다.

### 4.2 Alarm

근거: 원본 p3~4·6, 공유 규칙 4·5, DN-02. Alarm 데이터는 운행 State나 Broker 연결 상태와 별개다.

| 필드 | 자료형 | 필수 | 의미·허용값 |
| --- | --- | --- | --- |
| id | string | 예 | 대상 AMR ID, Topic ID와 동일 |
| ts | Int64 정수 | 예 | Alarm 사건 시각 |
| code | string | 예 | Alarm 종류를 식별하는 코드. `ALM-OBS-001`은 예시이며 강제 형식 아님 |
| level | string | 예 | INFO / WARN / ERR / FATAL. 원본 p4 기준 |
| alarmState | string | 예 | ACTIVE / CLEARED. 발생·해제를 구분하는 개발용 채택값 |
| message | string | 아니오 | Alarm 설명. 생략 가능, 존재하면 비어 있지 않은 문자열 |

원본은 발생·해제를 요구하지만 전체 alarmState 문자열은 확정하지 않았다. ACTIVE/CLEARED는 개발용 채택값이다. message는 원본 Domain에 있고 예시에서 생략되어 선택 수신한다. 반복 발생·해제·재발·RESET과의 관계는 관련 기능명세에서 다루며, 위치와 발생 회차 ID를 새 Payload 필드로 추가하지 않는다.

### 4.3 Heartbeat

근거: 원본 p3·5·6, 공유 규칙 3·5·9, DN-02/10.

| 필드 | 자료형 | 필수 | 의미·허용값 |
| --- | --- | --- | --- |
| id | string | 예 | 발행 AMR ID, Topic ID와 동일 |
| ts | Int64 정수 | 예 | Heartbeat 발행 시각 |
| status | string | 예 | OK. 원문 예시를 사용한 개발용 채택값 |

Timeout은 ts가 아니라 마지막 정상 수신 시각을 기준으로 측정한다. status=OK와 관제에서 관측한 Timeout·복구, 최종 Offline 판정은 서로 다른 값이다.

### 4.4 Command

근거: 원본 p5~6, 공유 규칙 5·8, DN-08 개발용 Command 계약.

| 필드 | 자료형 | 필수 | 의미·허용값 |
| --- | --- | --- | --- |
| cmdId | string | 예 | 요청 식별자. 전달 과정에서 변경하지 않음 |
| type | string | 예 | MOVE / STOP / RESET |
| ts | Int64 정수 | 예 | 요청 생성 시각 |
| param | object | 예 | MOVE는 x/y만 포함. STOP·RESET은 빈 object `{}` |
| param.x / param.y | 유한 double | MOVE만 둘 다 예 | 같은 세계 좌표계의 이동 목표 |

명령 세 종류는 원문 기준이다. STOP·RESET의 빈 param은 개발용 채택값이다. Command에는 id 필드가 없으며 대상은 Topic으로 식별한다. 명령 수행 가능 조건은 AMR 기능명세에서 정의한다.

### 4.5 ACK

근거: 원본 p5~6·11, 공유 규칙 5·8, DN-02/08.

| 필드 | 자료형 | 필수 | 의미·허용값 |
| --- | --- | --- | --- |
| cmdId | string | 예 | 응답 대상 명령의 원래 식별자 |
| result | string | 예 | SUCCESS / FAILURE. 수행 성공 또는 실패·거부 결과 |
| ts | Int64 정수 | 예 | 결과 발생 시각 |
| id | string | 아니오 | 있으면 Topic ID와 일치. 없으면 Topic에서 대상 식별 |
| reason | string | 아니오 | 실패 이유 등 결과 설명. 존재하면 비어 있지 않은 문자열 |

SUCCESS는 원문 예시, FAILURE는 기존 개발 계약의 허용값이다. reason은 실패 이유 요구를 구체화한 개발용 선택 필드이며 기존 reason 없는 ACK도 수신한다. 현재 TestClient가 보내는 실패 응답에는 reason을 포함한다. reason 코드 목록·중복/늦은 ACK·업무 Timeout 처리는 후속 기능명세에서 정한다. ACK 발행·수신과 실제 동작 완료는 구분한다.

## 5 통신 내부 관측 데이터

공유 규칙 3·4·9·10에 따라 외부 MQTT 메시지와 관제 내부 관측 데이터를 구분한다. 아래 항목은 현재 Communication의 개발 계약이며 MQTT Payload에 추가하는 필드가 아니다.

| 데이터 | 필드·자료형 | 의미 |
| --- | --- | --- |
| Broker 연결 관측 | State: enum | Disconnected / Connecting / Connected / Disconnecting / Faulted / Disposed. AMR 운행 State와 별개 |
| Broker 연결 관측 | Reason: string | 상태 변경 사유 |
| Broker 연결 관측 | TimestampUnixMilliseconds: Int64 | 상태 변경 관측 시각, UTC Unix 밀리초 |
| Broker 연결 관측 | Error: 선택 string | 연결·종료 오류 정보. 오류가 없으면 없음 |
| Heartbeat 관측 | AmrId: string | 감시 대상 AMR |
| Heartbeat 관측 | Kind: enum | MonitoringStarted / TimedOut / Recovered. 감시 시작·수신 중단·수신 재개 |
| Heartbeat 관측 | LastReceivedUnixMilliseconds: Int64 | 마지막 정상 수신 시각, UTC Unix 밀리초 |
| Heartbeat 관측 | ObservedUnixMilliseconds: Int64 | 시작·Timeout·복구를 관측한 시각, UTC Unix 밀리초 |
| Heartbeat 관측 | ElapsedMilliseconds: double | 마지막 정상 수신 이후 단조 시계로 측정한 경과 밀리초 |
| Heartbeat 관측 | TimeoutMilliseconds: double | 적용한 감시 제한 시간, 밀리초 |
| 입력 검증 오류 Log | Topic: 선택 string, Code/Field/Reason: string | 수신 Topic·오류 코드·필드 경로·실패 이유. Topic이 없는 입력도 기록 가능 |

근거: DN-01/03/04/10, [Broker 관측 모델](../../src/Amr.Communication/Mqtt/BrokerConnectionSnapshot.cs), [Heartbeat 관측 모델](../../src/Amr.Communication/Monitoring/HeartbeatNotification.cs), [수신 계약](../../src/Amr.Communication/Validation/CONTRACT.md). 연결·입력 오류 Log는 업무 History와 구분하며 Payload 원문을 기록하지 않는다. Heartbeat 관측은 최종 Offline이나 실제 운행 상태를 확정하지 않는다.

## 6 공통 설정과 미정 사항

아래 값은 공유 규칙 9·11, DN-03/10의 현재 개발 기본값이다. 시험에서 변경한 값은 검증 계획·결과에 별도로 기록한다.

| 설정 | 자료형·단위 | 현재 개발 기본값 | 의미 |
| --- | --- | --- | --- |
| Broker Host | string | 127.0.0.1 | 접속 주소 |
| Broker Port | int | 1883 | TCP 포트 |
| MQTT ClientId | string | `amr-control-` + Guid | MQTT 접속 클라이언트 식별자. AMR ID와 별개 |
| KeepAlive | TimeSpan, 설정 입력 초 | 5초 | MQTT 연결 유지 설정 |
| 접속·통신 Timeout | TimeSpan, 설정 입력 초 | 3초 | Broker 접속·통신 대기 설정. 단절 감지 전체가 항상 3초라는 의미 아님 |
| Heartbeat Timeout | TimeSpan, 설정 입력 초 | 5초 | 정상 Heartbeat가 없는 경과 시간 기준 |
| Heartbeat 확인 주기 | TimeSpan, 설정 입력 초 | 0.5초 | AMR 감시 검사 주기 |
| 인증 정보 | 선택 string | 로컬 익명 접속 | 환경 설정으로 제공. 명세·로그에 실제 비밀번호를 기록하지 않음 |

현재 MQTT 개발 프로파일은 MQTT 5/TCP·QoS 0·Retain false다. 운영 프로파일은 별도 결정한다. Broker 설정은 [연결 설정](../../src/Amr.Communication/Mqtt/BrokerConnectionOptions.cs), Heartbeat 설정은 [감시 계약](../../src/Amr.Communication/Monitoring/CONTRACT.md)을 따른다.

| 미정 항목 | 다음에 연결할 명세 |
| --- | --- |
| 좌표 원점·축·범위·도착 허용 오차 | AMR-02·GUI-02, P-07 |
| 외부 장비의 시간 단위·화면 시간대 | 송수신 계약·GUI, P-10/21 |
| Alarm 코드의 업무 의미·실패 reason 코드 목록 | AMR-03/04·CORE-03/04, P-02/13 |
| 명령 업무 상태·Timeout·중복/늦은 ACK | COM-05·CORE-04, P-08 |
| 장비 등록·업무 History·Core/GUI 조회 데이터 세부 필드 | CORE·GUI 후속 기능. [전체 공통 계약](interface_contract.md)의 제안과 구분해 작성 |

다음 구현용 기능명세는 이 문서의 데이터를 참조하여 **입력 → 처리 조건 → 출력 → 검증 기준**을 작성한다. 데이터와 허용값을 변경할 때는 관련 설계 결정·계약 버전·생산자/소비자 영향과 재검증 범위를 함께 기록한다.
