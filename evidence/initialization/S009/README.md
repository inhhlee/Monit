# S009 Windows baseline evidence

Status: all required baseline checks completed successfully; see Completion below.

- Before migration: evidence/bootstrap/20260920T184455642+0900-8b371d/ (20 .NET + 8 Python tests).
- Migrated tool tests: development/migration.trx (37 .NET tests).
- Actual Mosquitto: evidence/COM-03/20260920T185653-164f13bba34041e69e2102a1e397de72/result.json (checks 1-5 passed, 20 product tests, restart).
- Product sources and feature verification criteria are unchanged. Python tools were replaced with .NET tools.
- A same-machine clean checkout is not a test on another physical Windows notebook.

Final entrypoint validation: evidence/bootstrap/20260920T190439753+0900-acf92c/ contains passing restore/build, 20 product tests + 37 tool tests, and actual COM-03 verification.
Latest actual Broker run: evidence/COM-03/20260920T190458-d91d57424d8e4961a95bc777a05436c7/ (5 checks passed; 6.017s idle, 0.198s shutdown, 3.099s response loss).
Windows PowerShell 5.1 entrypoint: evidence/bootstrap/20260920T190542851+0900-88209b/.
Initial Git Bash invocation reported missing dirname despite exit 0 (evidence/bootstrap/20260920T190545455+0900-3e630a/). The helper now uses only Bash builtins to resolve its directory; absolute invocation from C:/ passed (bash-absolute.log and evidence/bootstrap/20260920T190623878+0900-34547c/).
Missing PowerShell: missing-powershell.log (expected exit 2). Invalid tracker JSON: development/bad-tracker.json (expected CLI exit 1).

## Completion

- Passed: init.cmd verify-broker, total 57 automatic tests, actual Mosquitto checks 1-5.
- Passed: init.cmd broker and start -DurationSeconds 2 (evidence/bootstrap/20260920T190746492+0900-4ed5d8 and 20260920T190750725+0900-25e033).
- Passed: clean source snapshot at C:/practice/.tools/S009 clean 한글, invoked from C:/ with no Python on PATH, no initial bin/obj or .git. Full rebuild and 57 tests: clean-verify/ and clean-verify.log. Connected then graceful stop: clean-start/ and clean-start.log.
- Clean snapshot uses the same PC's installed SDK and NuGet cache. POWERSHELL_EXE explicitly selects the locally available PowerShell 7.6.5. This does not certify another physical notebook or a fresh tool installation.
- Product source files under src were unchanged. COM-03 revision/criteria were retained and new evidence appended. COM-04 remains next.
- Git Bash dirname dependency was fixed using Bash builtins; no failed check was suppressed.
- Existing development Broker and Seq were preserved. No Python scripts or Python tests remain.
- Baseline commit hash is recorded in claude-progress.md after commit creation. No remote push is part of this session.
