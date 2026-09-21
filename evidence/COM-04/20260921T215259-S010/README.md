# S010 COM-04 verification — revision 2

2026-09-21 Asia/Seoul. Final standard command: `./init.cmd verify`.
264 product tests (244 COM-04 + existing 20), 37 tool tests, restore/build, tracker and TRX checks passed.
Final run: `evidence/bootstrap/20260921T215259887+0900-ec2624/`.

## Scope and repeatability

- Provisional internal contract: `src/Amr.Communication/Validation/CONTRACT.md`, DN-01/02. Core and validation log are recording test doubles. No actual Core, GUI, AMR, MQTT subscription or message transport is certified by this run.
- .NET SDK 10.0.401/runtime 10.0.12; Windows 10.0.26200 x64; PowerShell 7.6.5; MQTTnet package 5.2.0.1603 remains installed but is not called by the parser.
- Start at repository root and run `./init.cmd verify`. No temporary data or source edits are needed. Final run used a fresh test process after the development run, without environment overrides. The component has no persisted state/startup service; application receive-path integration is deferred to COM-01.
- HEAD before edits: f28c407936a0fa802a8d8b3f5b152b7bcdb21511. `source-sha256.json` identifies tested source and helper contents; `result.json` records item-level results and environment.
- Existing WPF NU1701 warnings: 6. COM-03 connection implementation, console and lifecycle tests were unchanged. Existing 20 tests passed. The independent broker run completed at 21:41 during this session; COM-04 does not extend its scope or replace existing COM-03 evidence.

## Requirement-to-test map

TRX result output contains fixture inputs, Core call counts and typed values, structured failure records, and recovery assertions. All input strings are synthetic fixtures. Product validation logs do not include the raw payload.

| Verification | Evidence in MessageValidationTests |
| --- | --- |
| 1 | CONTRACT.md, Sample/Fields fixtures, 17 EveryRequiredFieldMustBePresent cases |
| 2 | 4 NormalMessagesPreserveAllFieldsAtCoreBoundary; UnicodeIdentifierAndMessageContentArePreserved; OptionalAckIdAndValidFailureResultArePreserved |
| 3 | 12 InvalidJsonAndRootShapesDoNotReachCore; InvalidUtf8IsRejectedAndNextMessageWorks; 3 UnpairedUnicodeEscapesAreRejectedWithoutEscapingExceptions; PayloadLimitIncludesExactBoundaryAndRejectsOversizeAndExcessiveDepth |
| 4 | 17 required-field omissions, 85 wrong-type cases, 18 blank-string cases; OptionalAckIdIsValidatedWhenPresent |
| 5 | 14 UnsupportedTopicsAreRejected; 12 PayloadForDifferentTopicKindIsRejected; 4 PayloadIdWhenPresentMustMatchTopic |
| 6 | 10 invalid range/enum cases, all 9 allowed enum values, 20 Int64 boundary cases, 20 invalid timestamp cases |
| 7 | Reject helper asserts zero Core calls and one Topic/reason log, then immediately sends a valid heartbeat through the same processor. Invalid UTF-8 has the same explicit recovery check. |
| 8 | Final standard pipeline summary, product/tool TRX; all 244 validation tests passed; actual MQTT receive-path check remains COM-01. |

## Preserved failures and recovery

1. Initial SDK absence: bootstrap `20260921T210339934+0900-63bf5f` and `20260921T210425375+0900-385e25`. SDK became available after user interruption; the agent did not install it.
2. Default restore failures without a detailed log: bootstrap `20260921T213117796+0900-1e23ad`, `20260921T215138782+0900-66bf31`, `20260921T215208845+0900-9ebb6a`. Limiting DOTNET_PROCESSOR_COUNT did not resolve it. A broader elevated rerun request had been denied and was not executed.
3. Starting baseline was independently run during interruption at `20260921T213720316+0900-dc0524`. Its summary/build/restore/TRX files are copied under `starting-baseline/`; 20 product + 37 tool results were inspected by the agent. Original user files are preserved separately.
4. `../S010-development/development.trx`: first 259 product tests passed. Extra Unicode tests then found 3 uncaught InvalidOperationException failures in `unicode-edge.trx` (2 other Unicode cases passed). Those failed results are recorded for verification 3/7/8 before final passing results.
5. String/property decoding was narrowed to catch invalid Unicode escape errors. `development-fixed.trx`: all 264 product tests passed.
6. `restore-diagnostic.log`: serial restore succeeded. The shared init.ps1 now bounds restore/build to one worker and disables worker reuse; every required pipeline stage remains. Final standard pipeline then passed. The root cause of default parallel restore failure is not conclusively identified.

Git Bash was attempted before SDK recovery: sandbox process startup failed, approved external retry reached the SDK-missing failure. The final modified helper was verified through init.cmd, not a second Git Bash or clean-source-copy run. S009 portability evidence remains historical and is not represented as rerun here.
