# Archive Report: Monitoring Pipeline (Detect → Verify → Decide → Notify)

**Change**: monitoring-pipeline  
**Archived**: 2026-09-14  
**Mode**: openspec  
**Status**: ARCHIVED — Ready for delivery (subject to ordinary repository policy)

---

## Final State Summary

This change delivers the monitoring (detect→verify→decide→notify) vertical slice that consumes `Enrollment.PublicApi.IMemberDirectoryReader` (the result of the archived `ai-access-control` change). The change is complete, verified, and archived with no blocking issues.

| Dimension | Value |
|-----------|-------|
| **Tasks** | 46/46 complete (44 original phases 1–7 + 2 PR4 follow-ups 8.1–8.2) |
| **Verification Verdict** | PASS WITH WARNINGS (0 CRITICAL findings) |
| **Tests** | 84/84 passing (32 Enrollment legacy + 52 Monitoring new) |
| **Build** | Clean — 0 errors, 28 warnings (pre-existing NU1900/NU1902 NuGet noise) |
| **Spec Compliance** | 19/19 requirements, 28/28 scenarios across 6 new capabilities |
| **Module Boundary** | Verified twice — Monitoring.* references ONLY Enrollment.PublicApi; zero Enrollment.Domain/Application/Infrastructure refs |
| **New Capabilities** | 6 domains: motion-image-ingestion, person-detection, identity-verification, access-decision-policy, access-notification, pending-verification-record |

---

## Specs Archived and Synced

All 6 new domain specs from `openspec/changes/monitoring-pipeline/specs/` have been mechanically copied to `openspec/specs/`:

| Domain | Action | Files | Status |
|--------|--------|-------|--------|
| motion-image-ingestion | Created | 1 spec.md | ✓ Copied, diff-verified |
| person-detection | Created | 1 spec.md | ✓ Copied, diff-verified |
| identity-verification | Created | 1 spec.md | ✓ Copied, diff-verified |
| access-decision-policy | Created | 1 spec.md | ✓ Copied, diff-verified |
| access-notification | Created | 1 spec.md | ✓ Copied, diff-verified |
| pending-verification-record | Created | 1 spec.md | ✓ Copied, diff-verified |

**No existing specs were modified** — all six domains are new to this change; no delta merge was required.

All copy operations used mechanical `cp -R` + `diff -r` verification; empty diffs confirm byte-for-byte integrity.

---

## Archive Folder Move

**Source**: `openspec/changes/monitoring-pipeline/`  
**Destination**: `openspec/changes/archive/2026-09-14-monitoring-pipeline/`

Move completed via `git mv` (tracked in repository history). Archive contains all artifacts:
- `proposal.md` ✓
- `design.md` ✓
- `tasks.md` (46/46 tasks checked) ✓
- `specs/` (6 domain directories, mirrored in main specs) ✓
- `verify-report.md` ✓
- `exploration.md` (from sdd-explore)
- `research.md` (from sdd-research)

Active changes directory (`openspec/changes/monitoring-pipeline/`) no longer exists.

---

## Verification Summary

Per `verify-report.md` (re-verified after PR4 follow-up on 2026-09-14):

### Build and Tests
- **dotnet build CamGuard.slnx**: PASSED (0 errors, 28 pre-existing warnings)
- **dotnet test**: PASSED (84/84 tests: 32 Enrollment + 52 Monitoring)
- **Spec Compliance**: All 19 requirements and 28 scenarios across the 6 capabilities verified

### Findings

**CRITICAL**: None (0 blockers)

**WARNINGS**: 1 open (non-blocking)
- Duplicate `Monitoring.DetectPerson` OpenTelemetry span name emitted in two locations (harmless; tags differ; cosmetic follow-up only)

**SUGGESTIONS**: 2 open (non-blocking, unchanged from prior verify)
- FaceAiSharp/OnnxRuntime concurrent `Run()` safety remains unproven under load test (mitigated by design's Channel<T> serialization)
- No `appsettings.json` section documents `AccessDecisionOptions`/`WatchFolderOptions` operator defaults

### PR4 Follow-Up Validation

Two warnings from the prior `pass_with_warnings` verdict were targeted by PR4 and confirmed closed:

1. **DecideAccessExecutor no-match branch** (Distance is null → Denied)
   - **Prior**: Zero direct test coverage
   - **Fixed**: New `DecideAccessExecutorTests.cs` with 2 tests:
     - `Distance_present_delegates_to_the_access_decision_policy` — asserts policy invoked once
     - `No_match_resolves_Denied_directly_without_calling_the_policy` — asserts Times.Never on policy mock
   - **Evidence**: Both tests pass as part of 84/84 suite; independent mock verification confirms correct behavior

2. **Missing production ingestion host**
   - **Prior**: Explained in code comments/tasks but not in discovery artifacts
   - **Fixed**: Explicit out-of-scope documentation added:
     - `proposal.md`, Out of Scope section: "A production hosting entrypoint (BackgroundService/IHostedService) wiring ingestion → workflow → archival together — this change delivers the components and proves their wiring via integration test; a runnable host is a follow-up."
     - `design.md`, Open Questions section: "No BackgroundService/IHostedService wires IIngestionSource.WatchAsync → MonitoringWorkflowHost → IImageArchiver together in a real running process; today only MonitoringWorkflowIntegrationTests exercises that wiring. A production hosting entrypoint is deferred to a follow-up change."
   - **Evidence**: Both lines exist in current on-disk files (not git-tracked, per repo established precedent for openspec planning docs); verification read confirmed their presence

### No Regression
- Module boundary clean: Monitoring.* references only Enrollment.PublicApi; no Enrollment internal projects
- Spec compliance unchanged: all 28 scenarios still verified by passing tests
- Test count: 84/84 (82 prior + 2 new), all pass, no failures

---

## Final State Authority

This archive report records the state AT CLOSE of the SDD cycle. Source ranking for facts:

1. **Persisted tasks artifact** (`openspec/changes/archive/2026-09-14-monitoring-pipeline/tasks.md`): all 46 tasks marked complete with [x]
2. **Explicit final-state facts from orchestrator launch prompt**: verification re-run after PR4 follow-up, two warnings fixed, one remaining non-blocking WARNING, module boundary re-verified
3. **Intermediate snapshots** (`verify-report.md`, `apply-progress`): consulted for detail but superseded by final facts above

No contradictions between sources. All facts are consistent and mutually corroborating.

---

## Git State (EXPLICIT FINAL STATUS)

**Important for delivery planning**:

- **Branch structure**: 4 stacked local branches:
  - `main` (real, at tip of `ai-access-control`: commit `cb2651f`)
  - `pr1-monitoring-scaffolding-domain-application` (stacked on main)
  - `pr2-monitoring-infrastructure-observability` (stacked on PR1)
  - `pr3-monitoring-tests-verification` (stacked on PR2)
  - `pr4-monitoring-verify-warnings` (current tip, stacked on PR3)

- **Remote**: No remote configured in this repository
- **Merge status**: **Nothing merged to main yet** — all 4 PR branches remain local stacks, no push to origin, no PR opened
- **Delivery**: Merging/pushing to remote is a separate, still-pending human decision under ordinary repository policy

This change is **archived and ready for delivery**, but actual deployment to main/remote remains user-controlled.

---

## Repository Hygiene Note (Carry-Forward Gap)

**Observation**: Planning artifacts in `openspec/changes/` have never been git-tracked in this repository for EITHER completed change (`ai-access-control` or `monitoring-pipeline`):
- Only `tasks.md` gets committed
- `proposal.md`, `design.md`, `research.md`, `exploration.md`, and `specs/` directories exist on disk but are not tracked

**For this change**: The two edits made in PR4 (Out of Scope line in `proposal.md`, Open Questions line in `design.md`) exist on disk and were verified by reading the current files, but were never part of any git commit. They will not appear in `git log`, `git show`, or future git-based code review systems.

**Recommendation**: Future repository improvements may address this by:
- Adding `openspec/` tree to `.gitignore` (if documents are ephemeral per-change artifacts)
- OR explicitly git-adding the `openspec/` tree at archive time (if documents should be auditable in git history)

This gap does not block archival but is worth noting for future process improvements.

---

## Carry-Forward Items (Non-Blocking, Explicitly Accepted)

The following items remain open and are documented as intentional deferred work, not blocking issues:

1. **FaceEmbedding.ExpectedDimension=512 unconfirmed** (inherited from `ai-access-control`)
   - Tests use assumed 512-dimensional embeddings; never verified against a real human face photo
   - Deferred for testing with actual image data

2. **FaceAiSharp/OnnxRuntime concurrent `Run()` safety unproven** (WARNING #1 from suggestions)
   - Design uses Channel<T> serialization (conservative fallback) per research findings
   - Medium-confidence safety; actual load-test proof deferred to future change

3. **`CommunityToolkit.VectorData.SqliteVec` remains `-preview`** (inherited from `ai-access-control`)
   - NuGet package is not GA; production readiness review deferred

4. **No production hosting entrypoint** (CLOSED in PR4 docs but deferred in implementation)
   - No BackgroundService/IHostedService wires the workflow to a runnable process
   - Integration test proves component wiring; production host is an explicit follow-up change
   - Documented in proposal.md and design.md as intentional out-of-scope

5. **Duplicate `Monitoring.DetectPerson` OpenTelemetry span name** (WARNING #1, non-blocking)
   - Same span name emitted in DetectPersonExecutor.cs and FaceAiSharpPersonDetector.cs
   - Tags differ; harmless but cosmetic; renaming one occurrence (e.g., to `Monitoring.DetectPerson.Adapter`) is a future quality pass

6. **No appsettings.json operator defaults** (SUGGESTION #2, non-blocking)
   - `AccessDecisionOptions` and `WatchFolderOptions` lack sample configuration section
   - Code defaults exist; documentation deferred

---

## Archive Checklist

- [x] **Task Completion Gate**: All 46 tasks marked complete; spot-checked against on-disk implementation (PR4 tasks independently verified)
- [x] **Specs synced**: 6 domain specs copied mechanically to `openspec/specs/`; diff-verified empty
- [x] **Folder moved**: `openspec/changes/monitoring-pipeline/` → `openspec/changes/archive/2026-09-14-monitoring-pipeline/` via git mv; no stale source remains
- [x] **Archive integrity**: All artifacts present in destination; archive contains proposal, design, specs, tasks, verify-report, exploration, research
- [x] **Verification passed**: No CRITICAL findings; PASS WITH WARNINGS verdict stands; 84/84 tests, clean build
- [x] **No regressions**: Module boundary clean, spec compliance verified, test count matches expected (84/84 = 82 prior + 2 new)
- [x] **Archive report written**: This document, recorded as final state per authority hierarchy

---

## Recommendations for Next Steps

1. **Delivery**: This change is ready for merge to main under ordinary repository policy (no SDD lifecycle blockers). The decision to push/PR is user-owned and not part of the archive phase.

2. **Follow-ups** (non-blocking, explicitly documented):
   - Rename one `Monitoring.DetectPerson` span to avoid duplicate in traces
   - Load-test FaceAiSharp concurrent safety under production load patterns
   - Add `appsettings.json` example section for operator configuration
   - Implement production BackgroundService/IHostedService host in a separate change

3. **Repository improvements**:
   - Decide git-tracking strategy for `openspec/changes/` planning artifacts (currently excluded)
   - Consider audit trail enhancement if planning documents should appear in git history

---

## SDD Cycle Complete

The monitoring-pipeline change has been fully **proposed → specified → designed → tasked → applied → verified → archived**.

- Proposal: `openspec/changes/archive/2026-09-14-monitoring-pipeline/proposal.md`
- Specs: `openspec/specs/{motion-image-ingestion, person-detection, identity-verification, access-decision-policy, access-notification, pending-verification-record}/spec.md` (now part of source of truth)
- Design: `openspec/changes/archive/2026-09-14-monitoring-pipeline/design.md`
- Tasks: `openspec/changes/archive/2026-09-14-monitoring-pipeline/tasks.md` (46/46 complete)
- Verify: `openspec/changes/archive/2026-09-14-monitoring-pipeline/verify-report.md` (PASS WITH WARNINGS)
- Archive: `openspec/changes/archive/2026-09-14-monitoring-pipeline/` (this location)

Ready for the next change.
