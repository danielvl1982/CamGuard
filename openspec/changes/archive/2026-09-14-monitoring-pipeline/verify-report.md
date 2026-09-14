```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:205936801c7f1614445028c1486aa2befefc9eb7035b24538003fd65229c20f5
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 19/19
scenarios: 28/28
test_command: dotnet test
test_exit_code: 0
test_output_hash: sha256:0183ba4d3eab09391f328c3766b7beb3444994db915f7b1dd1ed15c47e7b89f3
build_command: dotnet build CamGuard.slnx
build_exit_code: 0
build_output_hash: sha256:578ab611c1b9d684d98facc0f63332af53fdf3e63e830247507726430fe75e11
```

## Verification Report

**Change**: monitoring-pipeline
**Version**: N/A (single, unphased change) -- this pass re-verifies after PR4, a voluntary follow-up on the prior pass_with_warnings verdict
**Mode**: Standard (re-verification of a follow-up)

### Re-Verification Scope

This is a re-verify of a prior PASS WITH WARNINGS result (evidence_revision: sha256:2d2ea525f75025093bba28d6709639b6722f91afb57784a81a47c64c95a43b84), following a PR4 follow-up that targeted 2 of the prior report 3 WARNINGs. No fixes were made in this pass; findings below are read from the current on-disk state and re-run test/build evidence.

### Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 46 |
| Tasks complete | 46 |
| Tasks incomplete | 0 |

All 46 checkboxes in tasks.md are checked, including the new PR4: Verify Warning Follow-up section (8.1, 8.2). Both PR4 tasks independently confirmed against actual file content (not just trusted from the checkbox), see below.

### Build and Tests Execution

Build: PASSED -- 0 errors, 28 warnings (all pre-existing NU1900 NuGet-feed-index noise and one pre-existing NU1902 OpenTelemetry.Api advisory; identical class of warnings as the prior verify pass, no new correctness issues).

Command: dotnet build CamGuard.slnx
Result: Build succeeded. 28 Warning(s), 0 Error(s)

Tests: 84 passed / 0 failed / 0 skipped

Command: dotnet test
Result: Enrollment.UnitTests.dll -- Passed 32/32; Monitoring.UnitTests.dll -- Passed 52/52

This matches the expected count exactly: 82 prior (32 Enrollment + 50 Monitoring) plus 2 new tests in DecideAccessExecutorTests.cs equals 84 total (32 + 52). Independently re-run in this verification pass, not trusted from any prior report or apply-progress note.

Coverage: Not available (unchanged from prior pass -- no coverage-collection command configured in this repo).

### Warning 1 -- Closed (confirmed)

Prior finding: DecideAccessExecutor explicit no-match (Distance is null) to Denied branch had zero direct test coverage.

Verification: tests/Monitoring.UnitTests/DecideAccessExecutorTests.cs is a new file with 2 tests:
- Distance_present_delegates_to_the_access_decision_policy -- asserts IAccessDecisionPolicy.Decide(0.2) is called Times.Once and the result outcome is Allowed.
- No_match_resolves_Denied_directly_without_calling_the_policy -- asserts the result outcome is Denied, Decision.Distance is null, and IAccessDecisionPolicy.Decide(It.IsAny<double>()) is Times.Never invoked.

This is a real behavioral assertion, not merely an outcome check: the Times.Never verification on the policy mock is the exact pattern the prior WARNING asked for, mirroring the zero-face short-circuit test style already used elsewhere (DetectPersonExecutorTests.Zero_faces_detected_never_invokes_identity_verification_in_the_full_workflow). Both tests pass (confirmed in the 84/84 run above; specifically these 2 tests execute as part of Monitoring.UnitTests 52/52). CLOSED.

### Warning 2 -- Closed (confirmed)

Prior finding: no production ingestion-runner host (BackgroundService/IHostedService) exists, and this gap was explained in code comments and tasks.md, but not named in proposal.md Out of Scope list or design.md Open Questions, where a future reader would look first.

Verification (read directly from current on-disk file content, not cached):

proposal.md, Out of Scope section, new line:
"A production hosting entrypoint (BackgroundService/IHostedService) wiring ingestion to workflow to archival together -- this change delivers the components and proves their wiring via an integration test; a runnable host is a follow-up."

design.md, Open Questions section, new line:
"No BackgroundService/IHostedService wires IIngestionSource.WatchAsync to MonitoringWorkflowHost to IImageArchiver together in a real running process; today only MonitoringWorkflowIntegrationTests exercises that wiring. A production hosting entrypoint is deferred to a follow-up change."

Both lines exist in the current file content on disk (both files are disk-only artifacts per this repo established precedent for planning docs other than tasks.md -- same precedent noted in the task description) and explicitly name the missing host as an intentional, documented scope decision rather than an omission. CLOSED.

### Re-Check: No Regression

- Module boundary -- re-confirmed clean: Monitoring.Application.csproj references only Monitoring.Domain plus Enrollment.PublicApi; Monitoring.Infrastructure.csproj references only Monitoring.Application plus Common. Repo-wide grep for Enrollment.(Domain|Application|Infrastructure) across src/Monitoring.* and tests/Monitoring.UnitTests finds only XML-doc-comment mentions (explaining why the boundary is enforced), zero executable references. Unchanged from the prior verify.
- Spec compliance -- spec files under openspec/changes/monitoring-pipeline/specs/ are unchanged (same 6 capability folders: motion-image-ingestion, person-detection, identity-verification, access-decision-policy, access-notification, pending-verification-record); requirement/scenario totals remain 19/19 and 28/28, all previously confirmed compliant by passing tests, none of which regressed (84/84 pass, a superset of the prior 82/82).
- Build/tests -- clean build, 84/84 tests pass (see above), no new failures, no new errors.

### Remaining Non-Blocking Items

WARNING 3 (not in PR4 scope, still accurately open): The Monitoring.DetectPerson span is still emitted twice per detection -- once in DetectPersonExecutor.cs line 32 (StartActivity call) and again in FaceAiSharpPersonDetector.cs line 78 (same span name). Confirmed still present verbatim in both files. Harmless (tags differ), but still produces a duplicate span name in traces. This was not one of the 2 warnings PR4 targeted and remains an open, accurately-documented, non-blocking item; consider renaming one occurrence (e.g. Monitoring.DetectPerson.Adapter) in a future pass.

SUGGESTION 1 (unchanged, still open): FaceAiSharp/OnnxRuntime concurrency remains unproven under an actual concurrent-load test.

SUGGESTION 2 (unchanged, still open): AccessDecisionOptions/WatchFolderOptions have code defaults but no appsettings.json section documenting them for an operator.

Neither SUGGESTION was in PR4 scope; both remain accurately reported as open, non-blocking follow-up ideas.

### Issues Found

CRITICAL: None.

WARNING:
1. The Monitoring.DetectPerson span is still emitted twice per detection (unchanged from prior report, not addressed by PR4, non-blocking).

SUGGESTION:
1. The FaceAiSharp/OnnxRuntime concurrency risk remains unproven under an actual concurrent-load test (unchanged, non-blocking).
2. No appsettings.json section documents AccessDecisionOptions/WatchFolderOptions operator-facing defaults (unchanged, non-blocking).

### Verdict
PASS WITH WARNINGS

Build is clean (0 errors), all 84 tests pass (82 prior plus 2 new, independently re-run), all 46 tasks are genuinely complete, module boundaries remain clean, and all 28 formal spec scenarios across the six capabilities keep passing covering tests. Both WARNINGs targeted by PR4 are confirmed closed with real evidence: the DecideAccessExecutor no-match branch now has a direct test with a Times.Never policy-invocation assertion, and the missing production ingestion host is now explicitly named as an intentional out-of-scope decision in both proposal.md and design.md. One pre-existing, non-blocking WARNING (duplicate Monitoring.DetectPerson span name) and two pre-existing SUGGESTIONs remain open and unchanged -- none of these block archival. This change is ready for sdd-archive.
