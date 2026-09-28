# S016 - PDF 반영 COM-04 계약 v2 검증

- 시작 코드: b05e394004f92ccef583778839c6ae9d2237f8e4 + S016 변경. 파일 해시는 source-sha256.json.
- 계약: COM-04 spec_revision 3, DN-01/02, Validation/CONTRACT.md. COM-01 revision 2는 Heartbeat 입력 변경만 반영하며 전체 미완료.
- 원문 PDF: 260917_SI생태계_요구사항 정의 및 기능명세_이인호 연구원_v0.3.pdf. SHA256 80EFDB79B98C57CB897AD961DB4E88D68F0B662CBA65388FBD1B0E1221ECC953. p4 State/Level/Position 분리, p6 예시·검증, p7 로그 충돌, p9 COM-04를 대조했다.
- 환경: Windows 10.0.26200, .NET SDK 10.0.401, PowerShell 7.6.5, MQTTnet 5.2.0.1603; 실제 Heartbeat는 Docker 24.0.6, eclipse-mosquitto:2.1.2-alpine, localhost:1883, MQTT 5/QoS 0/Retain false.

## 실행과 재현

1. 수정 전 `./init.cmd verify`: evidence/bootstrap/20260929T080854587+0900-f8a0d4, 제품 264/도구 37 통과.
2. 개발 `dotnet test tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj --no-restore -m:1 -nr:false --logger "trx;LogFileName=development.trx" --results-directory evidence/COM-04/S016-pdf-contract/development`: 305 통과/1 실패. 잘못된 Unicode를 JsonNode로 재직렬화하던 테스트 입력 생성 단계에서 예외. 제품 파서 실패로 판정하지 않는다.
3. 해당 입력은 raw JSON으로 직접 전달하도록 수정. 같은 명령에서 로그 이름 corrected.trx: 제품 306건 전부 통과.
4. 최종 `./init.cmd verify`: evidence/bootstrap/20260929T081848889+0900-6b8c60, 제품 306(검증기 286 + 연결/골격 20)/도구 37 = 343 통과. 건너뜀 0, 빌드 오류 0, 기존 WPF NU1701 경고 6건. 새 테스트 프로세스에서 검증했다.
5. 실제 Heartbeat: broker-20260929T082021은 스크립트의 실행 중 로그 공유 모드 문제로 실패. 읽기 스트림에 FileShare.ReadWrite 적용 후 broker-20260929T082053 통과. 실패 결과와 로그 모두 보존.

실제 Heartbeat 재현: 기본 개발 Broker를 `./init.cmd broker`로 준비한 뒤 `pwsh -NoProfile -File tools/verify-com04-heartbeat.ps1`. 당시 실행 위치는 이 evidence 폴더의 verify-heartbeat.ps1이었으며 검증 후 실행 도구 책임에 따라 tools로 옮겼다(루트·출력 경로 계산만 조정). 스크립트는 기존 Broker를 중단하지 않고 임의 S016_ 접두 AMR ID로 검증한다. 가상 AMR 발행 프로세스는 테스트에서 중단하므로 생산자 Ctrl+C 정상 종료를 검증한 것은 아니다. 소비자 콘솔은 duration 종료 코드 0 및 새 프로세스 재시작 수신을 확인했다.

## COM-04 verification 연결

| 항목 | 검증 근거 | 결과 |
| --- | --- | --- |
| 1 계약·입력 준비 | CONTRACT.md, DN-01/02, Sample/Fields/PdfExamples | PDF 차이·임시 결정·18개 필수 필드 기록 |
| 2 정상 전달·보존 | NormalMessagesPreserveAllFieldsAtCoreBoundary, PdfPageSixExamplesPreserveData, OptionalAckIdAndValidFailureResultArePreserved | 네 유형 및 원문 네 예시가 올바른 모델로 전달; 실패 ACK 보존 |
| 3 JSON·빈 입력·구조 | InvalidJsonAndRootShapesDoNotReachCore, InvalidUtf8IsRejectedAndNextMessageWorks, UnpairedUnicodeEscapesAreRejectedWithoutEscapingExceptions, PayloadLimitIncludesExactBoundaryAndRejectsOversizeAndExcessiveDepth | 오류 로그 1회, Core 0회; 크기·깊이·Unicode 경계 포함 |
| 4 필수 필드·자료형 | EveryRequiredFieldMustBePresent 18건, EveryRequiredFieldRejectsWrongJsonTypes 90건, OptionalAlarmMessageIsValidatedWhenPresent 8건 | 누락·오타입·잘못된 선택 필드 모두 거부 |
| 5 Topic·유형·ID | UnsupportedTopicsAreRejected 14건, PayloadForDifferentTopicKindIsRejected 12건, PayloadIdWhenPresentMustMatchTopic 4건 | Topic과 Payload 대응, ACK 선택 id 포함 검사 |
| 6 범위·Int64 | EveryAllowedEnumValueReachesCore 13건, InvalidEnumsAndNumericRangesAreRejected 20건, Timestamp 경계 20건/거부 20건, NonnegativeFiniteSpeedAndCoordinatesArePreserved 3건 | enum·유한 수·속도·시간 경계 통과, 시간값 자동 변환 없음 |
| 7 오류 후 정상 처리 | Reject 보조 함수가 각 잘못된 입력 다음 동일 processor에 정상 Heartbeat 입력, 실제 Broker consumer.log | 입력 오류 격리 및 재수신; Topic/code/field/reason 로그 |
| 8 전체 실행·증거 | 최종 bootstrap TRX/summary.log, broker result.json 및 restart.log | 자동 343건 통과 및 기존 실제 Heartbeat 경로 회귀 통과 |

## 검증 한계

Core와 Log는 단위 테스트 대역, 실제 Broker 경로에서는 콘솔 대역이다. 실제 Telemetry/Alarm/ACK 구독·AMR별 큐·Command 발행은 COM-01 후속이다. COM-03의 전체 KeepAlive/단절 시나리오는 이번에 재실행하지 않았고 연결 코드도 변경하지 않았다. GUI/Core 업무·실제 AMR 이동·운영 TLS/인증·다른 PC 검증은 포함하지 않는다.

PDF의 시간 단위 및 AlarmState/Status/ACK Result 전체 목록은 확정되지 않았다. 밀리초·ACTIVE/CLEARED·OK·SUCCESS/FAILURE는 명시된 개발용 결정이다. v1 입력은 자동 변환하지 않는다. 과거 증거를 새 계약의 통과 근거로 사용하지 않는다.
