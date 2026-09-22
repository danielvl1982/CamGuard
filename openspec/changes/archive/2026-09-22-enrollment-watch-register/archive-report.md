# Archive Report: enrollment-watch-register

**Date**: 2026-09-22
**Change**: enrollment-watch-register
**Archive Location**: `openspec/changes/archive/2026-09-22-enrollment-watch-register/`
**Mode**: hybrid (filesystem + Engram)
**Store**: hybrid (openspec files + Engram mirror)
**Status**: ARCHIVED

## Status

**ARCHIVED**: Change fully complete, verified (PASS), and archived. Implementation: 13/13 tasks complete with remediation (RescanAndEnqueue + 31 persisted tests). Verification: PASS 8/8 requirements, 18/18 scenarios, 0 blockers, 0 critical — per remediation re-verify 2026-09-22.

## Summary

The `enrollment-watch-register` SDD change extends `Enrollment.Console` from a one-shot `spike`/`register` CLI to an interactive loop plus isolated watched-folder ingestion at `photos/enroll` (separate from `photos/watch`), prompting `FullName` per photo and branching on normalized `FindByFullNameAsync` (trim + OrdinalIgnoreCase) to overwrite a single embedding (Upsert same `MemberId`) or create a new member. All 13 tasks are complete, including remediation that fixes the pause-during-prompt no-loss defect via `RescanAndEnqueue()` in `EnrollmentWatchIngestionSource.Resume()` and adds 31 persisted covering tests. Re-verification (2026-09-22) confirms `dotnet build` 0 errors (8× NU1902 pre-existing) and `dotnet test` 126/126 passed (63 Enrollment + 63 Monitoring).

Size overage is cohesive and inherits **size:exception** approval for a single-PR delivery (watch + overwrite + loop + tests must ship atomically). The prior verify FAIL (16 blockers, 16 UNTESTED) is now superseded — remediation closes all unverified scenarios.

## Change Scope

### New Capability
- `enrollment-watch-ingestion`: enrollment folder-watch, stability gate (5×200ms FileShare.None), per-file `FullName` prompt with watcher pause+rescan, and `processed/`/`quarantine/` archival — isolated from `motion-image-ingestion`.

### Modified Capability
- `member-enrollment`: re-enrollment overwrite — `FindByFullNameAsync` (case-insensitive, trimmed), single-embedding replacement via Upsert reusing same `MemberId`, atomic per-photo and batch atomicity preserved.

### Artifacts Archived
- `proposal.md` (70 lines, 3915 bytes): Intent (one-shot → loop+watch), scope (photos/enroll, FileSystemWatcher+Channel, FindByFullNameAsync, single-embedding overwrite), approach, risks, rollback plan.
- `specs/enrollment-watch-ingestion/spec.md` (92 lines, 3751 bytes): 5 Requirements / 10 Scenarios — folder isolation, debounce stability gate, per-file prompt+pause, FullName validation, completion archival.
- `specs/member-enrollment/spec.md` (71 lines delta, 3567 bytes): ADDED 2 Requirements (Normalized FullName Lookup/Single-Embedding Re-enrollment 4 scenarios, Enrollment Name Validation 1 scenario) + MODIFIED 1 Requirement (Atomic Vector Persistence + Re-enrollment overwrite atomic).
- `design.md` (133 lines, 6233 bytes): Technical approach (FSW+Channel+FileShare.None 5×200ms, while(true) menu, Pause/Resume, Find→Upsert vs Enroll), 4 architecture decisions, data flow, file changes, interfaces, testing strategy.
- `tasks.md` (86 lines, 4449 bytes): 13/13 tasks complete (Phase 1 Foundation 1.1-1.3, Phase 2 Domain/Persistence 2.1-2.3, Phase 3 Watch Ingestion 3.1-3.2, Phase 4 Console Wiring 4.1, Phase 5 Testing 5.1-5.4) plus remediation banner and Spec Scenario Map 18/18. Size:exception approved, single-pr pending.
- `apply-progress.md` (139 lines, 20318 bytes): All tasks checked, Files Changed table (24 entries), deviations (DI split, fixed 200ms, source quarantine, Rehydrate, RescanAndEnqueue), issues found including pause no-loss fix, remaining none, workload single-pr size:exception, work-unit evidence (dotnet test 126, dotnet build 0 errors), remediation details + Spec Scenario Map 18/18 with covering tests.
- `verify-report.md` (192 lines, 32583 bytes): **PASS** — evidence_revision sha256:c9e7edf54ed6924d74c4f04779aa290de5f37b0df38c42767a7f5eccd5e13aaf (git diff HEAD), combined sha256:7482f54fd886e966c491b1d69115a7a92054048da2d08bc225166256b79156ce, build sha256:144c72cf57662d842d54120651307375136113b5c9dee91efd98213a79137674 + test sha256:dee64d3d399bddc644f3e4847b426a17de24c7704dfebfe27664e3ce8c68ef81, build 0 errors, test 126 passed (63+63), requirements 8/8, scenarios 18/18, 0 blockers, 0 critical, 2 WARNING (pre-existing NU1902 + size:exception), pause rescan proven by burst/sequential tests.
- `exploration.md` (21230 bytes): Current-state audit (one-shot CLI, Member single embedding, SqliteVec, Monitoring photos/watch pipeline, etc.).

## Spec Merge Details

### Main Spec Created
**File**: `openspec/specs/enrollment-watch-ingestion/spec.md`
**Source**: `openspec/changes/enrollment-watch-register/specs/enrollment-watch-ingestion/spec.md`
**Method**: Mechanical shell copy (cp to temp + diff -r + mv + diff -r verification)
**Changes Applied**: Full spec creation — 5 Requirements, 10 Scenarios as above. This domain is new; no prior canonical existed.
**Verification**: `diff -r source temp` empty PASS; `diff -r source target` empty PASS (byte-identity preserved).

### Main Spec Updated
**File**: `openspec/specs/member-enrollment/spec.md`
**Source**: `openspec/changes/enrollment-watch-register/specs/member-enrollment/spec.md`
**Method**: `gentle-ai sdd-archive-compose` (native, preserves unrelated requirements byte-for-byte, applies RENAMED→MODIFIED→REMOVED→ADDED)
**Command**: `gentle-ai sdd-archive-compose --canonical "openspec/specs/member-enrollment/spec.md" --delta "openspec/changes/enrollment-watch-register/specs/member-enrollment/spec.md" --output "openspec/specs/member-enrollment/spec.md.compose-tmp" && mv "openspec/specs/member-enrollment/spec.md.compose-tmp" "openspec/specs/member-enrollment/spec.md"`
**Exit code**: 0 (success)
**Changes Applied**:
- **ADDED**: `Normalized FullName Lookup and Single-Embedding Re-enrollment` — 4 scenarios (case-insensitive same Id, trimmed matches, unknown creates new, re-enrollment replaces embedding)
- **ADDED**: `Enrollment Name Validation` — 1 scenario (Empty FullName rejected → InvalidName, no persist)
- **MODIFIED**: `Atomic Vector Persistence` — extended to atomically Upsert same `MemberId` for existing normalized `FullName` (single embedding invariant, watch per-file atomic vs batch all-or-nothing), + new scenario `Re-enrollment overwrite is atomic` (Maria Lopez `  MARIA LOPEZ  ` → same Id, count stable)
- **Preserved**: `Member Registration Input`, `Face Detection Gate`, `Zero-Face Rejection`, `Multiple-Face Rejection`, `Embedding Extraction`, plus the two existing `Atomic Vector Persistence` scenarios (Successful end-to-end enrollment, Enrollment with one invalid photo persists nothing) and `Out of Scope` note
**Result**: Delta requirements merged; unrelated requirements preserved byte-for-byte.

**Delta Spec Disposition**: Both delta specs archived with the change folder; main specs (`openspec/specs/member-enrollment/spec.md` and `openspec/specs/enrollment-watch-ingestion/spec.md`) are the single source of truth going forward.

## Task Completion Gate

**Result**: PASS — All 13/13 tasks checked.

| Phase | Count | Status |
|-------|-------|--------|
| Phase 1: Foundation (1.1-1.3) | 3 | 3/3 complete |
| Phase 2: Domain & Persistence (2.1-2.3) | 3 | 3/3 complete |
| Phase 3: Watch Ingestion (3.1-3.2) | 2 | 2/2 complete |
| Phase 4: Console Wiring (4.1) | 1 | 1/1 complete |
| Phase 5: Testing (5.1-5.4) | 4 | 4/4 complete |
| **TOTAL** | **13** | **13/13 complete** |

No unchecked tasks remain in the archived `tasks.md`. Remediation adds 31 persisted tests (FindByFullNameTests 6, EnrollmentServiceTests 10, EnrollmentWatchIngestionTests 7, EnrollmentWatchArchiverTests 7 plus prompt-contract helper) and the RescanAndEnqueue fix — both counted as complete within tasks 5.1-5.3.

## Build and Test Verification (Final-State Authority)

**Build**: PASS
- Command: `dotnet build --nologo`
- Exit code: 0
- Errors: 0
- Warnings: 8 (all NU1902 OpenTelemetry.Api 1.13.1 moderate GHSA-g94r-2vxg-569j, pre-existing, unrelated to this change — present on every build including archived reports)
- Build output hash: sha256:144c72cf57662d842d54120651307375136113b5c9dee91efd98213a79137674 (Get-FileHash SHA256 of build_output.txt, lowercase)

**Tests**: PASS
- Command: `dotnet test --nologo`
- Exit code: 0
- Total: 126/126 passed
  - Enrollment.UnitTests: 63 (32 pre-existing + 31 remediation: FindByFullName 6, EnrollmentService 10, EnrollmentWatchIngestion 7, EnrollmentWatchArchiver 7 + helpers)
  - Monitoring.UnitTests: 63
- Skipped: 0
- Failed: 0
- Test output hash: sha256:dee64d3d399bddc644f3e4847b426a17de24c7704dfebfe27664e3ce8c68ef81 (Get-FileHash SHA256 of test_output.txt, lowercase)

Focused filter `dotnet test --filter "FindByFullName|EnrollmentWatch|EnrollmentService"` also passes (31 new covering tests), confirming coverage of all 16 previously UNTESTED scenarios.

## Verification Result (sdd-verify — Remediation Re-verify)

**Final Verdict**: PASS (Standard mode, strict_tdd: false)
**Evidence Revision**: sha256:c9e7edf54ed6924d74c4f04779aa290de5f37b0df38c42767a7f5eccd5e13aaf (SHA256 of `git diff HEAD` — tracked modifications: Program.cs 199 ins, IMemberVectorRepository, Member.Rehydrate, Mapper, Repo, DI, csproj, etc.)
**Combined Working-Tree Hash**: sha256:7482f54fd886e966c491b1d69115a7a92054048da2d08bc225166256b79156ce (diff + `git status --porcelain` for untracked change files)
**HEAD at verify**: 00ac266ba3e102ad6b897e19cd4654c41f2f7ff9 (dirty working tree — 10 tracked modifications + 19 untracked files per verify report)
**Date**: 2026-09-22 (remediation re-verify replaces prior FAIL 16 blockers at 00ac266-equivalent baseline with 95 tests)

**Requirements**: 8/8 compliant
**Scenarios**: 18/18 compliant (0 UNTESTED, 0 partial) — all have passing covering tests (hard rule satisfied)

| Requirement | Scenario | Covering Test | Result |
|-------------|----------|---------------|--------|
| Enrollment Watch Folder Isolation | New enrollment file enqueued | EnrollmentWatchIngestionTests.New_enrollment_file_in_photos_enroll_is_enqueued | ✅ COMPLIANT |
| Enrollment Watch Folder Isolation | Monitoring folder ignored | File_created_in_photos_watch_is_not_enqueued | ✅ COMPLIANT |
| Debounce and File Stability Gate | Locked retried until stable | Locked_file_is_retried_until_stable_then_enqueued | ✅ COMPLIANT |
| Debounce and File Stability Gate | Never stabilizes → quarantine | File_never_stabilizes_after_retries_is_quarantined_and_not_enqueued | ✅ COMPLIANT |
| Per-File Prompt and Watcher Pause | Stable prompts FullName | Watch_prompt_formats_member_fullName_correctly + Program.cs prompt string | ✅ COMPLIANT |
| Per-File Prompt and Watcher Pause | No loss while blocked (5 burst) | Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan | ✅ COMPLIANT |
| FullName Validation (watch) | Empty rejected → InvalidName → quarantine | MoveToQuarantineAsync_moves_file_on_InvalidName + EnrollmentServiceTests | ✅ COMPLIANT |
| Completion Archival | Success → processed | MoveToProcessedAsync_moves_successful_enrollment_to_processed_folder | ✅ COMPLIANT |
| Completion Archival | NoFace → quarantine | MoveToQuarantineAsync_moves_file_on_NoFaceDetected | ✅ COMPLIANT |
| Completion Archival | MultipleFaces → quarantine | MoveToQuarantineAsync_moves_file_on_MultipleFacesDetected | ✅ COMPLIANT |
| Normalized Lookup / Re-enrollment | Case-insensitive same Id | FindByFullNameTests ana torres + EnrollmentService case reuse | ✅ COMPLIANT |
| Normalized Lookup / Re-enrollment | Trimmed matches | Surrounding whitespace + trim reuse | ✅ COMPLIANT |
| Normalized Lookup / Re-enrollment | Unknown creates new | Returns null + creates new MemberId | ✅ COMPLIANT |
| Normalized Lookup / Re-enrollment | Replaces embedding | Same MemberId atomic + FindNearest photo B | ✅ COMPLIANT |
| Enrollment Name Validation | Empty rejected | Rejects empty/whitespace InvalidName | ✅ COMPLIANT |
| Atomic Vector Persistence (MODIFIED) | Successful enrollment | Enroll success + FindNearest | ✅ COMPLIANT |
| Atomic Vector Persistence (MODIFIED) | One invalid persists nothing | Atomic rejection, nothing persisted | ✅ COMPLIANT |
| Atomic Vector Persistence (MODIFIED) | Re-enrollment overwrite atomic | Same Id, count stable, photo B query | ✅ COMPLIANT |

**Critical Issues**: 0
**Warnings**: 2 (non-blocking: 1 pre-existing NU1902 advisory, 2 size:exception line-count overage — approved single-pr cohesive slice ~1150-1200 total vs 400 budget, remediation inherits exception)
**Suggestions**: 2 (optional: O(N) scan via GetAsync(r=>true) reliance on provider, fixed 200ms vs Monitoring scaled delay — both documented, within spec, covered by tests)

**Key Facts for Final State — Reconciliation per Final-State Authority**:
1. **Persisted tasks artifact** (highest): 13/13 complete, remediation closes 16 UNTESTED + pause no-loss.
2. **Orchestrator-provided final-state facts** (outrank snapshots):
   - verifyReport PASS 8/8 18/18 0 blockers replaces prior FAIL 16 blockers — outranks stale snapshot claim
   - apply-progress.md remediated (RescanAndEnqueue + 31 persisted tests, build 0 errors, tests 126)
   - HEAD 00ac266, working tree dirty with tracked mods + untracked change/test files, evidence_revision c9e7ed..., combined 7482f54f..., build/test hashes as above
   - size:exception already approved for ~1150-1200 single PR (cohesive watch+overwrite+loop+tests)
   - RescanAndEnqueue closes no-loss; 31 tests persisted; 126 tests green — all outrank prior UNTESTED snapshot
3. **verify-report and apply-progress snapshots**: intermediate PASS now authoritative; prior FAIL is stale history only.

Unrankable contradictions are recorded explicitly; stale FAIL claims are attributed to their snapshot time and not restated as current facts. A future reader consults this archive report as the terminal record.

## Implementation Details

### Files Changed (from apply-progress.md)

| File | Action | What Was Done |
|------|--------|---------------|
| `photos/enroll/.gitignore` | Created | Allowlist `*` + `!processed/` + `!quarantine/` |
| `photos/enroll/processed/.gitignore` | Created | Keep folder, ignore contents |
| `photos/enroll/quarantine/.gitignore` | Created | Keep folder, ignore contents |
| `src/Enrollment.Console/EnrollmentWatchOptions.cs` | Created | Options: SectionName EnrollmentWatch, Path C:/Dev/CamGuard/photos/enroll, 200ms×5, cap 64 |
| `src/Enrollment.Console/EnrollmentWatchArchiver.cs` | Created | Move to processed/quarantine via File.Move |
| `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` | Created + Remediation Fix | FSW+Channel64, IsStableAsync FileShare.None 5×200ms, Pause/Resume, quarantine unstable; Remediation: added RescanAndEnqueue() called from Resume() enumerating photos/enroll top-level files after EnableRaisingEvents=true |
| `src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs` | Created | Console-layer helper to bind EnrollmentWatchOptions and register archiver/source |
| `src/Enrollment.Domain/IMemberVectorRepository.cs` | Modified | Added FindByFullNameAsync(string, CancellationToken) → Member? |
| `src/Enrollment.Domain/Member.cs` | Modified | Added Rehydrate(MemberId, string, FaceEmbedding) |
| `src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs` | Modified | Added ToMember() and Normalize(string) (Trim) |
| `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` | Modified | Implemented FindByFullNameAsync via GetAsync(r=>true) scan + OrdinalIgnoreCase + IncludeVectors, activity Enrollment.VectorStore.FindByFullName |
| `src/Enrollment.Application/RegisterMemberCommand.cs` | Modified | Added RegisterMemberError.InvalidName |
| `src/Enrollment.Application/EnrollmentService.cs` | Created | Single-photo service: validates FullName, branches FindByFullNameAsync → reuse Id : NewId, extracts, Member.Enroll, SaveAsync |
| `src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs` | Modified | Added WatchActivitySourceName CamGuard.Enrollment.Watch, watch ActivitySource, EnrollmentService |
| `src/Enrollment.Console/Enrollment.Console.csproj` | Modified | Added Microsoft.Extensions.Logging, Logging.Abstractions, Options, Configuration.Binder/Abstractions |
| `src/Enrollment.Console/appsettings.json` | Modified | Added EnrollmentWatch:Path = C:/Dev/CamGuard/photos/enroll |
| `src/Enrollment.Console/Program.cs` | Modified | One-shot args[0] switch → hybrid one-shot + while(true) menu (spike/register/watch/exit), watch await foreach stable → Pause → prompt Member FullName for '{file}': → File.OpenRead → EnrollmentService → archiver → Resume, Ctrl+C |
| `Directory.Packages.props` | Modified | Added Microsoft.Extensions.Logging 10.0.12 |
| `tests/Enrollment.UnitTests/FindByFullNameTests.cs` | Created (Remediation) | 6 integration tests: case-insensitive, trimmed, unknown null, empty/whitespace null, trim applied, overwrite same Id atomic |
| `tests/Enrollment.UnitTests/EnrollmentServiceTests.cs` | Created (Remediation) | 10 tests: InvalidName without persist/extractor, NoFace/Multiple mapping, unknown creates, case/trim reuse same Id, overwrite atomic, trim before persist, failed extraction does not corrupt |
| `tests/Enrollment.UnitTests/EnrollmentWatchIngestionTests.cs` | Created (Remediation) | 7 tests: new file enqueued, photos/watch ignored, locked retried until stable, never stabilizes → quarantine, pause burst 5 no-loss via rescan, sequential after resume, prompt contract |
| `tests/Enrollment.UnitTests/EnrollmentWatchArchiverTests.cs` | Created (Remediation) | 7 tests: success→processed, InvalidName/NoFace/Multi→quarantine, no file left in drop, folder creation, Program routing integration |

### Deviations from Design (documented, no behavioral regression)
- EnrollmentWatch registration split to avoid Infrastructure→Console cycle (Infrastructure registers Watch ActivitySource + EnrollmentService; Console helper binds EnrollmentWatchOptions + archiver/source).
- IsStable fixed 200ms per attempt (spec literal) vs Monitoring scaled delay×attempt — within spec.
- Unstable quarantine in source itself (spec requires never-stable → quarantine before enrollment handler).
- Member.Rehydrate added for ToMember mapping.
- Pause no-loss rescan: Resume() enumerates photos/enroll top-level files and TryWrites to bounded channel (cap 64), preserving Pause semantics while guaranteeing burst 5 not lost.

### Architecture Decisions Confirmed
- Watch isolation via photos/enroll + processed/quarantine + EnrollmentWatchOptions.Path configurable
- FileSystemWatcher+Channel 5×200ms (proven Monitoring pattern, cap 64 vs 256 fits interactive rate)
- Pause watcher while blocked + RescanAndEnqueue (no DropOldest during prompt, no loss)
- Single-embedding overwrite (last wins Upsert, zero schema change, multi-embedding deferred)
- Client-side scan + Normalize (O(N) for family-scale N, no migration, no COLLATE NOCASE index)

## Workload / PR Boundary

- Mode: single-pr with size:exception approved (parent-confirmed, delivery strategy auto-chain but chain strategy pending — archiving as single cohesive change)
- Current work unit: Watch + overwrite in one PR (all phases) + remediation (pause rescan + 31 persisted tests)
- Boundary: photos/enroll folder through Program.cs watch loop with pause+rescan, FindByFullNameAsync + overwrite + archival + 31 persisted tests
- Changed lines: ~540-550 original + ~600 test + 20 rescan ≈ 1150-1200 total (forecast 310-360 underestimated new EnrollmentService + console helper + verbose stability/quarantine; size:exception already approved; slice cohesive — watch isolation + single-embedding overwrite + interactive loop + tests must ship together for 18 scenarios atomic)
- Recommendation: single PR (chained PRs not recommended); splitting would break atomicity.

## Final-State Facts (Explicit)

Per the preflight and verify-report PASS (re-verify 2026-09-22) — these facts are final-state and outrank any stale snapshots:

- **Remediation RescanAndEnqueue closes no-loss**: EnrollmentWatchIngestionSource.Resume() now enumerates photos/enroll top-level files and TryWrites to bounded channel after EnableRaisingEvents=true; proven by Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan (5 burst while paused all yielded after Resume) + sequential test. Fixes prior verify FAIL's lossy-pause finding.
- **31 tests persisted**: 4 new files (FindByFullNameTests 6, EnrollmentServiceTests 10, EnrollmentWatchIngestionTests 7, EnrollmentWatchArchiverTests 7 + helpers) close all 16 previously UNTESTED scenarios; committed as untracked + dirt working-tree evidence, not ad-hoc temp.
- **126 tests green**: dotnet test 126 passed (63 Enrollment + 63 Monitoring) 0 fail, 0 skip — Enrollment grew 32→63, all pre-existing + Monitoring 63 remain green (no regressions).
- **Build 0 errors**: dotnet build 0 errors, 8× NU1902 pre-existing warnings only (test/build hashes captured above).
- **8/8 requirements, 18/18 scenarios COMPLIANT**: includes enrollment-watch-ingestion 5/10 + member-enrollment 3/8 delta.
- **Evidence hashes are captured from the PASS re-verify** (not invented): evidence_revision sha256:c9e7edf54..., combined 7482f54..., build 144c72cf..., test dee64d3d..., HEAD 00ac266 with dirty working-tree diff.
- **Previous verify FAIL (16 blockers, 2/18 COMPLIANT) is superseded**: that snapshot remains valid history of what was true at its time but is no longer the state at close.

## Archive Operations

### Spec Composition
- **Member-enrollment**: `gentle-ai sdd-archive-compose --canonical openspec/specs/member-enrollment/spec.md --delta openspec/changes/enrollment-watch-register/specs/member-enrollment/spec.md --output openspec/specs/member-enrollment/spec.md.compose-tmp && mv compose-tmp canonical`
  - Exit code: 0 (success)
  - Result: 2 ADDED + 1 MODIFIED merged; unrelated requirements preserved byte-for-byte; compose-tmp atomically moved.
- **Enrollment-watch-ingestion**: mechanical copy (mkdir -p target_dir; mktemp; cp source temp; diff -r source temp PASS; mv temp target; diff -r source target PASS)
  - Result: New spec byte-identical to delta; no merge needed.

### Mechanical Archive Move
- **Source**: `openspec/changes/enrollment-watch-register/`
- **Destination**: `openspec/changes/archive/2026-09-22-enrollment-watch-register/`
- **Method**: snapshot cp -R to TEMP, attempted git mv (untracked → fatal: source directory is empty), fallback to plain mv (after fallback source diff empty PASS), then source-gone check, then diff -r snapshot vs destination.
- **Snapshot Verification**: `diff -r snapshot/source destination` — empty (no differences) PASS — byte-identity confirmed; fallback_source_diff also empty.
- **Date prefix**: 2026-09-22 (ISO YYYY-MM-DD) per rule.
- **Result**: Source removed from active changes; archive folder populated with all artifacts including new archive-report.md (additive, excluded from source/destination comparison).

### Diff Readback (Archive Verification)

**Spec sync (enrollment-watch-ingestion)**: `diff -r source temp` — empty PASS; `diff -r source target` — empty PASS.

**Spec sync (member-enrollment)**: `diff -u canonical before compose-tmp` shows expected ADDED/MODIFIED diff (not byte-identity, but native composition proof); `gentle-ai sdd-archive-compose` exit 0 is the composition evidence + mv atomic.

**Archive Move**: `diff -r snapshot/source destination` — empty PASS (full output below is empty; any non-empty would FAIL the phase). The archive-report file is additive-only and excluded from source/destination comparison because it did not exist in the source snapshot.

```
(diff -r snapshot/source destination — empty, no output)
(diff -r source temp enrollment-watch-ingestion — empty, no output)
```

Empty diff is the only passing evidence; verbatim output is included as empty (no differences).

## Traceability (Hybrid Mode)

This SDD change has been persisted to both backends:

**Filesystem** (openspec mode):
- Merged spec: `openspec/specs/member-enrollment/spec.md` (now 6604 bytes, 2 ADDED + 1 MODIFIED)
- New spec: `openspec/specs/enrollment-watch-ingestion/spec.md` (3751 bytes, new domain)
- Archived folder: `openspec/changes/archive/2026-09-22-enrollment-watch-register/` (proposal, design, tasks, apply-progress, verify-report PASS, exploration, specs deltas, archive-report.md)
- Active changes directory `openspec/changes/enrollment-watch-register/` no longer exists

**Engram** (persistent memory):
- Project: `camguard`
- Artifact store: hybrid — topics `sdd/enrollment-watch-register/{proposal,spec,design,tasks,apply-progress,verify-report,archive-report}`
- Engram IDs actually read before archive:
  - proposal: #224 (architecture) — `sdd/enrollment-watch-register/proposal`
  - design: #225 (architecture) — design enrollment-watch-register
  - tasks: #226 (architecture) — `sdd/enrollment-watch-register/tasks`
  - verify-report prior PASS #227 (decision) — verify PASS 18/18 (superseded by full verify-report mirror below, but retained as read evidence)
  - explore: #223 (architecture) — `sdd/enrollment-watch-register/explore`
- Engram IDs mirrored on this archive (upserts on same topic_key or new):
  - `sdd/enrollment-watch-register/spec` → spec delta (enrollment-watch-ingestion + member-enrollment) mirror
  - `sdd/enrollment-watch-register/apply-progress` → apply-progress mirror (13/13 + remediation)
  - `sdd/enrollment-watch-register/verify-report` → verify-report PASS mirror (full 8/8 18/18 0 blockers, hashes c9e7ed..., 144c72..., dee64d3d..., HEAD 00ac266)
  - `sdd/enrollment-watch-register/archive-report` → this report (topics synced below)

The Engram verify-report mirror reflects the PASS actual (2026-09-22, 126 tests, 0 blockers); the prior FAIL snapshot (16 blockers) remains history but is no longer the current verdict per final-state authority.

## Risks and Decisions

### No Blocking Risks
- All CRITICAL issues closed (pause no-loss fix + 31 persisted tests close 16 UNTESTED)
- All 13/13 tasks complete, no unchecked implementation work remains
- Build green (0 errors), tests green (126/126)
- Spec merges successful (native compose + mechanical copy both verified)
- Archive move successful with mechanical diff readback empty PASS
- size:exception approved — cohesive single-PR delivery; splitting would break correctness

### Pre-existing Advisories (Not New, Documented)
- OpenTelemetry.Api NU1902 moderate vulnerability persists across solution (8 warnings on every build, unrelated to this change; no new packages introduced beyond Microsoft.Extensions.Logging 10.0.12)
- O(N) scan via GetAsync(r=>true) relies on provider translating constant-true to no-filter — verified against CommunityToolkit.VectorData.SqliteVec 1.0.1-preview in 6 integration tests; fallback documented in apply-progress Issues Found
- IsStable fixed 200ms vs Monitoring scaled delay×attempt — spec literal, slightly more lenient on later attempts, within spec, documented deviation

### Rollback Readiness (after archive, before merge)
- Delete `photos/enroll/` (+ processed/quarantine gitignores)
- Delete `src/Enrollment.Console/EnrollmentWatch*.cs` + `EnrollmentWatchServiceCollectionExtensions.cs` + `src/Enrollment.Application/EnrollmentService.cs`
- Revert `IMemberVectorRepository` (remove FindByFullNameAsync), `Member.cs` (Rehydrate), `MemberVectorRecordMapper`/`SqliteVecMemberRepository` (scan+ToMember/FindByFullName), `EnrollmentServiceCollectionExtensions` (WatchActivitySource+EnrollmentService), `Program.cs` to args[0] switch one-shot, `appsettings.json` (remove EnrollmentWatch:Path), `Directory.Packages.props` (remove Logging version), plus 4 remediation test files
- `dotnet build && dotnet test` returns to 95 tests at 00ac266
- After spec merge + archive, rollback reverts specs: `git checkout main -- openspec/specs/member-enrollment/spec.md; git clean -fd openspec/specs/enrollment-watch-ingestion/`

## Source of Truth Updated

The following specs now reflect the new behavior (hybrid persistence complete):
- `openspec/specs/member-enrollment/spec.md` — ADDED/MODIFIED requirements now canonical (member-enrollment + enrollment overwrite)
- `openspec/specs/enrollment-watch-ingestion/spec.md` — new canonical spec for enrollment watch ingestion (5 req, 10 scenarios)

These are the source of truth going forward; archived delta specs are audit copies only.

## Preservation Notes

- **Bytes historical preserved**: Archived tasks retain original checked bytes (13/13); verify PASS bytes preserved; no checkboxes repaired or historical reports rewritten during archive. Prior FAIL snapshot history remains accessible in git log but is not the current verdict.
- **Missing artifacts**: none observed — proposal, specs (2 domains), design, tasks, apply-progress, verify-report all present in archive snapshot. apply-progress and verify-report were read directly from `C:\Dev\CamGuard\openspec\changes\enrollment-watch-register\`.
- **Active changes directory**: `openspec/changes/enrollment-watch-register/` no longer exists post-archive (source gone, destination populated).
- **Verbatim diff readback**: included above; empty diff is the only passing evidence per Mechanical Copy Contract; agent self-report never sufficient.
- **Engram mirror**: archive-report saved to Engram topic `sdd/enrollment-watch-register/archive-report` (type architecture) with observation IDs for traceability above; verify-report mirror updated to PASS actual with hashes per hybrid contract.

## SDD Cycle Complete

The change is archived. Implementation: **complete (13/13 + remediation RescanAndEnqueue + 31 persisted tests)**. Verification: **PASS (126 tests, 8/8 requirements, 18/18 scenarios, 0 blockers)**. Unfinished tasks and unresolved findings: **none observed** — all specs implemented, all tests green, all scenarios compliant, deviatons documented, rollout/rollback defined. Pre-existing NU1902 advisory and size advisory remain but are not blocking.

**Archive Location**: `openspec/changes/archive/2026-09-22-enrollment-watch-register/`
**Preservation**: All artifacts that existed in the source change folder are preserved in the archive; missing artifacts are reported as none.
**Next steps**: Merge single PR (size:exception approved) — no further code or test work required for this change per verify PASS nextRecommended archive.

---

**Archived by**: sdd-archive (SDD Archive Phase)
**Archive Date**: 2026-09-22
**Report Generated**: 2026-09-22
**HEAD at archive**: 00ac266ba3e102ad6b897e19cd4654c41f2f7ff9 (dirty working-tree changes preserved + specs synced + archive move)
**Evidence revision reviewed**: sha256:c9e7edf54ed6924d74c4f04779aa290de5f37b0df38c42767a7f5eccd5e13aaf (+ combined 7482f54f..., build 144c72cf..., test dee64d3d...)
**Mode**: hybrid (both -> openspec files + Engram mirror)
**Workload**: single-pr with size:exception (~1150-1200 lines, cohesive slice)

