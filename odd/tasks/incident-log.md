# Feature: Incident log (DRAFT — scope to confirm)

> ODD feature document — single source of truth for this feature's life. Every phase/task
> outcome, verification evidence, and decision is logged here automatically as work progresses.

## Objective (draft)

Keep a queryable incident list for CamGuard: each incident with its description,
date and time, and related data — so access events that matter (denied entries,
unknown faces, annulled-member attempts) don't vanish without a trace.

## Problem / Why

Today nothing is recorded when access goes wrong: an unknown face at the door, a
denied entry, an annulled member trying to get in — all of it happens and leaves no
record. The user asked for an incident list as the next task (session 2026-09-27).

## Open decisions

- **OI1 — RESOLVED (user, 2026-09-27): BOTH.** Incidents come from the
  monitoring/recognition flow (unknown face, denied access, annulled-member
  attempt) AND from manual operator reports in the web UI.
- **OI2 — Incident fields (defaults, revisable at implementation):** `Id`,
  `OccurredAtUtc` (fecha y hora), `Kind` (unknown-face / denied /
  annulled-attempt / manual-note…), `MemberId?` (when a known member is involved),
  `Note?`, `SnapshotImagePath?`.
- **OI3 — Retention (default, revisable):** keep all in v1, paginate the UI.

## Scope (tentative, revisable after OI1–OI3)

### In scope (proposal)
- Incident record in the store + repository port/adapter (new collection or table).
- Writer hook: recognition flow (or manual form) persists incidents.
- `/incidents` page: list ordered by date/time desc, kind badge, member link when known.
- Tests for persistence + listing.

### Out of scope (v1)
- Real-time alerts/notifications, exports (CSV/PDF), incident resolution workflow,
  photo snapshots unless OI2 includes them.

## Constraints

- .NET 10, hexagonal boundaries (Domain free of ASP.NET and preview vector package).
- UI copy in English. Token system + zone layout preserved.
- Tailwind: one-shot `npm run build` after new utility classes — NEVER a watcher.
- Pre-push hook blocks direct pushes to `main`.

## TDD & checks

- **TDD mode: OFF** (no strict-TDD configuration). Ordinary functional checks apply.
- Test runner: `dotnet test`; build: `dotnet build CamGuard.slnx`.

## Delivery strategy

- TBD after scope confirmation. Expect 2–4 tasks on a `feat/incident-log` branch.

## Tasks

- [x] **T1 — Scope confirmation (OI1)** — DONE 2026-09-27: user chose BOTH
  (automatic recognition-flow events + manual operator reports).
- [x] **T2 — Incident persistence (domain + store) — DONE**
  Record + repository port/adapter, OI2 field set.
  *Acceptance:* `dotnet test` green incl. new tests.
  *Route:* delegated (writer trigger).
  *Evidence:* 2026-09-27. 9 paths: `Incident` (EntityBase, UTC normalization,
  blank→null) + `IncidentId` + `IncidentKind` (4 variants) + `IIncidentRepository`
  (`SaveAsync` upsert, `ListRecentAsync(limit)` newest-first, `FindByIdAsync`) +
  `SqliteIncidentRepository` (plain SQLite via Microsoft.Data.Sqlite, NOT SqliteVec:
  no vector search needed, dead-weight embedding avoided; TEXT ISO-8601 ordering +
  `rowid` tie-break; T1/T2 migration-guard precedent) + `IncidentsTableName` option
  + DI (TryAddScoped) + `IncidentRepositoryTests` (10 tests) + DI test update.
  Verified: build 0 errors; Enrollment 104/104 (94 + 10 new). Parent spot-check:
  build 0 errors + `Incident.cs` boundary readback (no ASP.NET/preview refs).
  *Notes for T3:* `SaveAsync` is INSERT OR REPLACE — generate a fresh `IncidentId`
  per event. *Incident:* stale server PID 20728 stopped by writer (DLL locks).
- [ ] **T3 — Automatic capture hook** — DONE 2026-09-28: `IIncidentSink`
  port (primitives-only, Application never refs Enrollment.Domain) +
  `IncidentSinkAdapter` (Monitoring.Infrastructure, refs Enrollment.Domain) +
  `NotifyExecutor` failure-isolated hook + DI (`TryAddScoped`) + 7 new tests.
  Mapping v1: Denied/no-distance → UnknownFace, Denied/with-distance →
  AccessDenied (+member), zero-face HVR → UnknownFace; Allowed + gray-zone HVR
  ignored. AnnulledAttempt RESERVED (annulled filtered in FindNearestAsync, so
  an annulled attempt surfaces as UnknownFace in v1).
  *Acceptance:* build 0 errors; Monitoring 70/70 (63 + 7 new), Enrollment
  104/104. *Route:* direct inline (delegation `task` unavailable: free-tier
  transport refusal; mapping + writer triggers recorded here instead).
  *Evidence:* `IIncidentSink.cs`, `NotifyExecutor.cs` (+sink ctor arg, fixed 2
  manual constructions + hosted-service DI test), `IncidentSinkAdapter.cs`,
  `Monitoring.Infrastructure.csproj` (+Enrollment.Domain ref),
  `MonitoringServiceCollectionExtensions.cs`, `NotifyExecutorTests.cs` (+2),
  `IncidentSinkAdapterTests.cs` (+5 cases), `Monitoring.UnitTests.csproj`
  (+Enrollment.Domain ref). *Incident:* hosted-service test failed first
  (manual ServiceCollection lacked the new sink) — fixed with
  `Mock.Of<IIncidentSink>()`.
- [ ] **T4 — Manual report form + `/incidents` list page** — operator form
  (kind + note) + list newest-first with kind badge + member link. *Route:*
  delegated.
- [ ] **T5 — Verification & closure** — build + full tests + smoke, final
  commit(s), evidence refresh. *Route:* inline.

## Acceptance criteria (feature-level)

1. Incidents are recorded with date and time and listed newest-first.
2. `dotnet build` 0 errors; `dotnet test` green.
3. Work-unit commits on the feature branch; merge + push only on user order.

## Progress log

- 2026-09-27 — Draft doc created from the user's "lista de incidencias" request.
  Blocked on OI1–OI3 (source, fields, retention) before any implementation.
- 2026-09-27 — **OI1 resolved (user): BOTH sources.** Tasks T2–T5 planned
  (persistence, auto hook, manual form + list, closure). Ready to branch
  `feat/incident-log` on user order.
- 2026-09-27 — T2 landed (plain-SQLite incidents table + 10 tests, 104/104).
  Parent spot-check: build 0 errors + boundary readback. Server restart owed
  (writer stopped PID 20728).
