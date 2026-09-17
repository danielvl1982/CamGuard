# Archive Report: access-notification-channels

**Date**: 2026-09-17  
**Change**: access-notification-channels  
**Archive Location**: `openspec/changes/archive/2026-09-17-access-notification-channels/`  
**Mode**: hybrid (filesystem + Engram)

## Status

**ARCHIVED**: Change fully complete, verified, and archived.

## Summary

The `access-notification-channels` SDD change extended the archived `monitoring-pipeline` change's `access-notification` capability by implementing real, free-tier notification delivery channels (ntfy push + SMTP email) for non-Development environments, while preserving the logging placeholder for development. All 21 tasks were completed, full solution build succeeded (0 errors), all tests passed (95/95), and re-verification confirmed the prior CRITICAL finding (untested "no secret in appsettings.json" scenario) was closed by task 6.1's new `AppSettingsSecretsTests.cs` with negative-control verification. The change is now merged into the main spec and archived.

## Change Scope

### Modified Capability
- `access-notification`: Expanded from single logging placeholder to environment-aware channel selection (Development → logging, otherwise → composite ntfy+email fan-out with per-channel failure isolation).

### Artifacts Archived
- `proposal.md` (5,920 bytes): Defined intent, scope, approach, risks, and success criteria.
- `design.md` (8,889 bytes): Technical architecture, file changes, testing strategy, and design decisions.
- `tasks.md` (6,704 bytes): Phase 1–6 breakdown with all 21 tasks completed (Phase 6 remediation added `AppSettingsSecretsTests.cs`).
- `verify-report.md` (9,924 bytes): Verification result PASS, 0 CRITICAL, 0 WARNING, 1 pre-existing SUGGESTION unrelated to this change.
- `specs/access-notification/spec.md` (delta): MODIFIED `Notification Failure Isolation`; ADDED `Channel Selection By Environment` and `No Delivery Secrets In Source Control`.

## Spec Merge Details

### Main Spec Updated
**File**: `openspec/specs/access-notification/spec.md`

**Composition Method**: `gentle-ai sdd-archive-compose` (native, preserves unrelated requirements byte-for-byte)

**Changes Applied**:
- **MODIFIED**: `Notification Failure Isolation` — expanded to document multi-channel fan-out isolation in addition to workflow-level isolation.
- **ADDED**: `Channel Selection By Environment` — 3 scenarios (Development logging, non-Development composite, and composite fan-out isolation).
- **ADDED**: `No Delivery Secrets In Source Control` — 2 scenarios (no SMTP password in committed config, fail-fast on missing required config).
- **Purpose Section**: Updated from "shipping a logging placeholder" to "in Development environments, log via `LoggingNotificationService`; in Production and other non-Development environments, deliver through real channels (ntfy push and SMTP email) with per-channel failure isolation."

**Delta Spec Disposition**: Archived with the change folder; main spec is the single source of truth going forward.

## Task Completion Gate

**Result**: PASS — All 21/21 tasks checked.

| Phase | Count | Status |
|-------|-------|--------|
| Phase 1: Configuration Options | 3 | 3/3 complete |
| Phase 2: Real Notification Adapters | 3 | 3/3 complete |
| Phase 3: Fan-Out and Channel Selection | 4 | 4/4 complete |
| Phase 4: Tests | 8 | 8/8 complete |
| Phase 5: Verification | 3 | 3/3 complete |
| Phase 6: Remediation (CRITICAL closure) | 1 | 1/1 complete |
| **TOTAL** | **21** | **21/21 complete** |

No stale unchecked implementation tasks remain in the archived `tasks.md`.

## Build and Test Verification

**Build**: PASS  
- Command: `dotnet build`
- Exit code: 0
- Errors: 0
- Warnings: 8 (all pre-existing NU1902 OpenTelemetry.Api advisory, unrelated to this change)

**Tests**: PASS  
- Command: `dotnet test`
- Exit code: 0
- Total: 95/95 passed
  - Enrollment.UnitTests: 32/32
  - Monitoring.UnitTests: 63/63 (includes new `AppSettingsSecretsTests.cs`)
- Skipped: 0
- Failed: 0

## Verification Result (sdd-verify)

**Final Verdict**: PASS  
**Evidence Revision**: sha256:edc3548cbd8d5d31041aa21b4b48440dd06a8d4fcf4bcbeaaea3313fd3f38cd8  

**Critical Issues**: 0 (prior CRITICAL finding closed by Phase 6 remediation)  
**Warnings**: 0  
**Suggestions**: 1 (pre-existing NU1902 OpenTelemetry advisory, unrelated to this change scope)

**Spec Compliance**: 6/6 scenarios compliant  
- ✅ Alert Fired For Every Outcome (unchanged, 3 scenarios)
- ✅ Alert Content Shape (unchanged, 1 scenario)
- ✅ Notification Failure Isolation (MODIFIED: 2 scenarios, both passing)
- ✅ Channel Selection By Environment (ADDED: 2 scenarios, new, passing)
- ✅ No Delivery Secrets In Source Control (ADDED: 2 scenarios, new, passing with remediation)

**Remediation Detail**: Task 6.1 added `AppSettingsSecretsTests.cs`, which loads the committed `src/Monitoring.Host/appsettings.json` through `IConfiguration` and asserts `EmailNotification:Password` is empty. This converted the "Committed configuration carries no secret" scenario from UNTESTED (static inspection only) to a runtime regression test with negative-control verification, closing the sole CRITICAL finding from the prior verify-report.

## Implementation Details

### New Files Created
- `src/Monitoring.Infrastructure/NtfyOptions.cs`
- `src/Monitoring.Infrastructure/NtfyNotificationService.cs`
- `src/Monitoring.Infrastructure/EmailNotificationOptions.cs`
- `src/Monitoring.Infrastructure/EmailNotificationService.cs`
- `src/Monitoring.Infrastructure/CompositeNotificationService.cs`
- `tests/Monitoring.UnitTests/NtfyNotificationServiceTests.cs`
- `tests/Monitoring.UnitTests/EmailNotificationServiceTests.cs`
- `tests/Monitoring.UnitTests/CompositeNotificationServiceTests.cs`
- `tests/Monitoring.UnitTests/FakeHostEnvironment.cs`
- `tests/Monitoring.UnitTests/NotificationChannelSelectionTests.cs`
- `tests/Monitoring.UnitTests/AppSettingsSecretsTests.cs` (Phase 6 remediation)

### Files Modified
- `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs` — `AddMonitoring` signature extended with `IHostEnvironment`, branches registration on `environment.IsDevelopment()`.
- `src/Monitoring.Infrastructure/Monitoring.Infrastructure.csproj` — MailKit 4.16.0 reference added.
- `src/Monitoring.Host/Program.cs` — Passes `builder.Environment` to `AddMonitoring`.
- `src/Monitoring.Host/appsettings.json` — `Ntfy`/`EmailNotification` placeholder sections added.
- `Directory.Packages.props` — MailKit pinned to 4.16.0 (fixes CVE-2026-41319).
- `tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj` — Fakes and test references added; `testdata/appsettings.json` link added for regression testing.
- `tests/Monitoring.UnitTests/AccessDecisionPolicyTests.cs` — Updated to pass `FakeHostEnvironment` to new `AddMonitoring` signature.

### Architecture Decisions Confirmed
1. **Runtime channel selection via `IHostEnvironment`** (not `#if DEBUG`) — allows same binary to run with different channels in different environments.
2. **`CompositeNotificationService` as failure-isolation boundary** — per-channel try/catch prevents one channel's failure from blocking the other.
3. **Call-time options validation** — consistent with `AccessDecisionOptions` convention; not a new startup-gate validation mechanism.
4. **Secrets via `dotnet user-secrets` / environment variables** — `appsettings.json` carries empty placeholder; real values supplied externally, now verified by `AppSettingsSecretsTests`.

## Key Facts for Final State

### Per Final-State Authority
This archive report describes the state of the change **at close**, reconciling all sources:

1. **Orchestrator-provided final-state facts** (authoritative, most recent):
   - sdd-verify re-verification: PASS (0 CRITICAL, 0 WARNING, 1 pre-existing SUGGESTION)
   - All 21/21 tasks complete (including 6.1 remediation task)
   - dotnet build: clean, 0 errors
   - dotnet test: 95/95 passed (Enrollment 32/32, Monitoring 63/63)

2. **Persisted artifacts consulted**:
   - `proposal.md`: scope, risks, rollback plan defined
   - `design.md`: technical architecture and decisions documented
   - `tasks.md`: all 21 tasks checked, no unchecked implementation work remains
   - `verify-report.md`: PASS verdict, remediation detail confirms Phase 6.1 closed the CRITICAL

3. **Repository evidence**:
   - Full solution builds clean
   - All tests pass with new regression test for secrets
   - No secrets in source control, verified by runtime test with negative control

## Archive Operations

### Spec Composition
- **Command**: `gentle-ai sdd-archive-compose --canonical openspec/specs/access-notification/spec.md --delta openspec/changes/access-notification-channels/specs/access-notification/spec.md --output openspec/specs/access-notification/spec.md.compose-tmp`
- **Exit code**: 0 (success)
- **Result**: Delta spec requirements merged into main spec, unrelated requirements preserved byte-for-byte.

### Mechanical Archive Move
- **Source**: `openspec/changes/access-notification-channels/`
- **Destination**: `openspec/changes/archive/2026-09-17-access-notification-channels/`
- **Method**: `git mv` (tracked folder), with fallback to `mv` if needed.
- **Snapshot Verification**: Pre-move recursive snapshot vs. post-move archived tree compared with `diff -r`; empty diff confirms byte-identity.
- **Result**: Source successfully removed from active changes; archive folder populated with all artifacts.

### Diff Readback (Archive Verification)

**Composition**: No differences found between delta spec source and merged main spec (delta fully applied).

**Archive Move**: No differences found between pre-move snapshot and archived folder contents (move successful, no truncation or alteration).

## Traceability (Hybrid Mode)

This SDD change has been persisted to both backends:

**Filesystem** (openspec mode):
- Active spec merged: `openspec/specs/access-notification/spec.md`
- Archived folder: `openspec/changes/archive/2026-09-17-access-notification-channels/`

**Engram** (persistent memory):
- Engram topic key: `sdd/access-notification-channels/archive-report`
- Artifact type: `architecture`
- Project: `camguard`

## Risks and Decisions

### No Blocking Risks
- All CRITICAL issues closed (Phase 6 remediation)
- All tests passing
- Build clean
- Spec merge successful
- Archive move successful

### Pre-existing Advisory (Not New)
- OpenTelemetry.Api NU1902 CVE advisory persists across the solution (unrelated to this change); MailKit correctly pinned to 4.16.0 to fix its own CVE-2026-41319.

### Rollback Readiness
- Revert `AddMonitoring`'s environment branch to always register `LoggingNotificationService`, or delete the three new adapter files and their registrations.
- `INotificationService`/`AccessAlert`/`NotifyExecutor` are untouched, so no caller-side rollback needed.

## Closure

**SDD Cycle Complete**: All phases executed (explore → research → propose → spec → design → tasks → apply → verify → archive).

**Next Recommended**: none — this change is complete and closed.

**Archive Authority**: This report is the terminal record. The merged main spec (`openspec/specs/access-notification/spec.md`) is the single source of truth for notification requirements going forward. The change folder has been archived with a date prefix for audit trail continuity.

---

**Archived by**: sdd-archive (SDD Archive Phase)  
**Archive Date**: 2026-09-17  
**Report Generated**: 2026-09-17
