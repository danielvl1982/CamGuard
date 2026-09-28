# Monitoring module — audit fixes (confirmed, not up for redesign)

## Objective
Implement 3 confirmed fixes from a completed code-quality audit of the Monitoring module: 2 MEDIUM findings + 1 user-approved retry/backoff addition. Design is settled; execute precisely, no scope expansion.

## Source of truth
Full precise specs for Fix A/B/C were given directly by the user in this session (magic-string coupling constant, fail-fast options validation scoped to non-Development, and workflow retry/backoff before quarantine). Not re-derived here in full; see task breakdown below for the acceptance points.

## Tasks

- [x] **A. Shared constant for "No face detected" reason**
  - Add `public const string` in `Monitoring.Application` (on `DetectPersonExecutor` or a small shared static class).
  - Reference it from `DetectPersonExecutor.cs` (~line 48) and `IncidentSinkAdapter.MapKind` (~line 73).
  - Update `tests/Monitoring.UnitTests/IncidentSinkAdapterTests.cs` and `DetectPersonExecutorTests.cs` to reference the constant instead of the literal.

- [x] **B. Fail-fast validation for NtfyOptions/EmailNotificationOptions**
  - In `MonitoringServiceCollectionExtensions.cs`, mirror the existing `AccessDecisionOptions` validation mechanism for `NtfyOptions` (Topic non-empty) and `EmailNotificationOptions` (SmtpHost/FromAddress/ToAddress non-empty).
  - Validation registered only in the non-Development branch (where real channels are actually registered), not unconditionally.

- [x] **C. Retry/backoff before quarantine in MonitoringHostedService**
  - Extract workflow-run logic into `RunWorkflowWithRetryAsync`, `MaxWorkflowAttempts = 3`, fresh file stream per attempt, linear backoff `200ms * attempt`, retry only when `attempt < MaxWorkflowAttempts` and not `OperationCanceledException`; final attempt propagates naturally; trailing `throw new UnreachableException()`.
  - `ProcessFileAsync` calls the helper; outer try/catch (log+quarantine) unchanged.
  - New test in `MonitoringHostedServiceTests.cs`: fails N-1 times then succeeds, asserts processed (not quarantined) + warning logged.

## Route
Delegated direct — one writer agent (general-purpose, sonnet) implements all of A/B/C across ~7 files (mapping trigger: 4+ files; writer trigger: 2+ non-trivial files fired).

## Verification (mandatory, run in foreground)
- `dotnet build src/Monitoring.Infrastructure/Monitoring.Infrastructure.csproj`
- `dotnet test tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj`
All must pass; do not report done on red build/tests.

## TDD mode
Not explicitly configured for this task; user asked for tests added per fix (esp. Fix C) — functional checks + new/updated tests, not a strict RED-GREEN-REFACTOR cycle since A/B are refactors of existing passing code.

## Status
Done. All 3 fixes implemented by delegated writer, independently spot-checked (diff review + re-ran build/test myself). Verified:
- `dotnet build src/Monitoring.Infrastructure/Monitoring.Infrastructure.csproj` -> 0 Errors, 10 pre-existing NU1900/NU1902 warnings.
- `dotnet test tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj` -> Passed: 76, Failed: 0, Skipped: 0 (baseline 71 + 1 retry test + 4 fail-fast theory cases).

Adaptation: `tests/Monitoring.UnitTests/NotificationChannelSelectionTests.cs` needed updating because Fix B's new validation made its previously-empty Production-environment test config invalid; fixed by supplying valid config + adding an explicit fail-fast theory test, per user's own instruction to read the whole file and get it exactly right.

Note: repo working tree also carries unrelated, pre-existing/concurrent changes to Enrollment.* files (not part of this task, not touched by the delegated writer — confirmed via diff --stat matching exactly the 7 Monitoring-scoped files reported).
