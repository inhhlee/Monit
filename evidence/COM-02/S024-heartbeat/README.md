# S024 COM-02 Heartbeat Timeout·복구 검증

최종 **COM-02 revision2의7항목 통과**. 표준440건(제품403/도구37, Heartbeat19 포함), 실제 Mosquitto Heartbeat8시나리오, 실제 Console/TestClient5항목 및 COM-01/03/04 회귀를 확인했다. 아래 초기 Windows 정책 차단 실패는 과거 실행 기록으로 보존한다. 에이전트는 Windows 보안 설정을 변경하지 않았으며 현재 통과로 영구적인 정책 변경을 추정하지 않는다.

## 구현 범위

- 첫 정상 Heartbeat부터 AMR별 감시, 개발 기본5초/500ms.
- 수신 검사 직전의 단조 시계와 관제 UTC를 기록하며 Payload ts·Core 큐 대기를 판정에 사용하지 않는다.
- QueueFull이어도 유효한 Heartbeat는 수신 시각을 갱신한다. 잘못된 메시지·다른 유형·새 AMR 한도 초과는 갱신하지 않는다.
- Timeout 구간당1회 알림, 정상 수신 시 복구1회. Broker 단절 감지/정상 종료는 타이머를 닫고 명시적 재접속은 새 감시 객체를 만든다.
- IHeartbeatEventSink 경계 및 콘솔 HeartbeatMonitoringStarted/HeartbeatTimeout/HeartbeatRecovered JSON 출력, 실행 인수 추가.
- 정책: src/Amr.Communication/Monitoring/CONTRACT.md와 feature_list.json의 DN-10. 실제 Core 업무/장비 이동/최종 Offline/자동 재접속은 범위 밖.

## 초기 구현 및 차단 당시 기록

| 절차 | 결과 | 증거 |
| --- | --- | --- |
| 시작 init.cmd verify (샌드박스) | NuGet.Config 접근 제한으로 restore 실패 | evidence/bootstrap/20260929T172602905+0900-97646e/ |
| 동일 기준선 승인 재실행 | 384+37=421건/skip0 통과, 기존 NU1701 경고6건 | evidence/bootstrap/20260929T172617944+0900-e9add5/ |
| 새 Heartbeat18건+기존 큐9건 실행 | 27건 모두 대상 DLL 로딩 차단으로 실패. 제품 assertion 결과가 아님 | development/heartbeat-development.trx |
| 동일 빌드 --no-build로 승인 재실행 | 같은 0x800711C7,27건 실행 불가 재현 | development/heartbeat-development-retry.trx |
| Windows CodeIntegrity 로그 조회 | 이벤트3077에서 이 프로젝트 Amr.Communication.dll 차단 확인 | development/code-integrity.json |
| 이후 정적 검토 및 최종 솔루션 빌드 | 오류0, 기존 NU1701 경고6. 테스트 경고3건 수정, 타이머 최소1ms 설정 방어 및 해당 테스트1건 추가 | development/build.log |
| 실제 Broker 검증 도구 빌드 | 오류0/경고0 | development/audit-build.log |

위 빌드 직후에는 최종19건의 테스트가 미실행이었다. 설정 방어는 System 타이머 주기가1ms 미만일 때0으로 처리될 수 있는 코드 검토 사항을 수정한 것이며 차단 회피용 변경이 아니다. 이후 아래 검증에서19건 모두 통과했다. S022 결과는 이전 코드 버전의 이력이며 현재 통과는 새 증거를 사용한다.

## 최종 실행 결과

| 실행 | 결과 | 증거 |
| --- | --- | --- |
| 사용자17:58 표준 verify | 403+37=440건, skip0. 기존 S024 제품 소스 SHA256 일치 | final-validation/baseline/ (bootstrap/20260929T175804347+0900-d20c14 결과의 복사본) |
| 실제 송수신·큐 회귀 | 12/12. A5초 보류 중 B는 약44ms 뒤 처리, A순서401→402→404 | final-validation/communication-probe/ 및 communication-probe.log |
| 실제 Console/TestClient 명령 회귀 | MOVE/STOP/RESET·ACK ID/실패이유 등9/9 | final-validation/broker-20260929T180351/ |
| 실제 COM-03 연결 회귀 | 5/5. idle6.015초, 정상 종료0.072초, 응답 차단3.089초, 새 콘솔 프로세스 재시작 | evidence/COM-03/20260929T180443-24ec1835438f409aad9e31df228ccdcd/ |
| 실제 Heartbeat 도구 | 8/8. 1500ms 제한에서1514.8281ms 뒤 A만 Timeout, B17건 처리, 불량 입력11건 거부, 정상 수신 복구1회 | final-validation/heartbeat-probe/ 및 heartbeat-probe.log |
| 실제 콘솔/가상 AMR2대 | 5/5. 기본5000ms 제한에서5222.7603ms 뒤 A만 Timeout1회, 같은 ID 재실행 후 복구1회, B수신 지속 | final-validation/console-probe/ |
| 최종 솔루션 등록 후 표준 verify | 샌드박스 NuGet.Config 접근 제한 실패 후 승인 재실행440건/skip0, 오류0/기존 NU1701 경고6 | evidence/bootstrap/20260929T180752536+0900-3272e0/ 실패 및 evidence/bootstrap/20260929T180809400+0900-b03879/ 통과 |

Heartbeat Audit는 같은 Broker의 다른 실습 AMR을 시험 결과에 섞지 않도록 자체 생성한 ID2개만 관측한다. 솔루션 tools에 등록해 편집기의 프로젝트 미등록 안내 원인을 해결했으며 표준 빌드에 포함된다. 실제 Broker 실행은 별도 명령이다. 검증 중 만든 프로세스/임시 컨테이너만 종료했고 기존 Mosquitto/Seq는 유지했다.

## 재현과 검증 범위

1. `init.cmd verify`로 전체 기준선을 확인한다.
2. `init.cmd broker` 후 `dotnet run --project tools/Amr.Heartbeat.Audit -- 127.0.0.1 1883 evidence/COM-02/<새 실행ID>`를 실행한다. 개발 시험값1500ms/100ms, 발행200ms다.
3. `pwsh -NoProfile -File tools/verify-heartbeat-basics.ps1`로 실제 콘솔의 기본5초 감지·복구를 확인한다. 도구가 시작한 AMR 프로세스를 중단·재실행하며 키보드 Ctrl+C 입력 자체는 미검증이다. 수동 실습은 README의3개 터미널 절차를 따른다.
4. COM-03 회귀는 표준 verify 후 같은 helper의 Broker 단계인 `dotnet tools/Amr.Verification/bin/Debug/net10.0/Amr.Verification.dll broker <저장소루트> <Docker경로>`로 실행했다. 최초 준비부터 재현하려면 `init.cmd verify-broker -Docker <Docker경로>`를 사용한다.

제어 시계 단위 테스트는4999/5000/5001ms 경계, UTC 변경, 과거/미래 Payload ts, 불량 입력·큐 포화·느린 Core·중복 억제·종료를 검증한다. Broker 단절 시험은 자기 시험 ClientId의 SessionTakenOver로 감지된 연결 상실과 AMR Timeout을 구분한다. 연결 상실 감지 전의 네트워크 지연과 최종 업무 Offline/MqttAlive 결정은 계약의 범위를 따른다.

시작 HEAD b3523c0와 기존 사용자 init.ps1·솔루션·주석 변경이 있는 작업 트리에서 수행했다. 초기 해시 source-sha256.json과 재개/최종 해시 final-validation/source-sha256*.json을 구분한다. 실제 Broker 도구는 AMR 발행자와 Core/알림 대역을 쓰며 실제 장비/GUI 검증이 아니다. 사용자 staged init.ps1과 이전 실행 로그를 보존한다. COM-01~04 passing, 다음 기능은 COM-05다. 실제 이동·자동 재접속·다른 PC/클린 체크아웃은 이번 통과 범위가 아니다.
