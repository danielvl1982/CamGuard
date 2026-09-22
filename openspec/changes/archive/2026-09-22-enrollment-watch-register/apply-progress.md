# Apply Progress: enrollment-watch-register

**Change**: enrollment-watch-register
**Mode**: Standard (strict_tdd: false)
**Store**: openspec (hybrid)
**Date**: 2026-09-18 (remediation pass — closes 16 UNTESTED + pause no-loss fix)

## Completed Tasks

- [x] 1.1 Create `photos/enroll/.gitignore` (`*`, `!processed/`, `!quarantine/`)
- [x] 1.2 Create `src/Enrollment.Console/EnrollmentWatchOptions.cs` (SectionName, Path, 200ms×5, cap64)
- [x] 1.3 Create `src/Enrollment.Console/EnrollmentWatchArchiver.cs` (File.Move to processed/quarantine)
- [x] 2.1 Add `FindByFullNameAsync` to `src/Enrollment.Domain/IMemberVectorRepository.cs`
- [x] 2.2 Implement scan+Normalize and `ToMember()` in `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` + `src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs`
- [x] 2.3 Create `src/Enrollment.Application/EnrollmentService.cs` (branch Find→Upsert vs Enroll, InvalidName); update `src/Enrollment.Application/RegisterMemberCommand.cs`
- [x] 3.1 Create `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` (FSW+Channel64, IsStableAsync FileShare.None 5×200ms, Pause/Resume)
- [x] 3.2 Register options/source/archiver in `src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs` and add `EnrollmentWatch:Path` to `src/Enrollment.Console/appsettings.json`
- [x] 4.1 Refactor `src/Enrollment.Console/Program.cs` to `while(true)` menu; watch `await foreach` stable→Pause→prompt `Member FullName for '{file}':`→`File.OpenRead`→handler→archiver→Resume
- [x] 5.1 Test `FindByFullNameAsync` normalize + overwrite atomic (case, trimmed, unknown, same-Id replace, count)
- [x] 5.2 Test InvalidName + stability gate (empty→quarantine, locked 5×200ms→quarantine)
- [x] 5.3 Test pause/isolation/archival (5 queued during prompt not lost, `photos/watch` ignored, success→processed, NoFace/Multi→quarantine)
- [x] 5.4 Run `dotnet build && dotnet test` + manual watch drop

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `photos/enroll/.gitignore` | Created | Allowlist `*` + `!processed/` + `!quarantine/` |
| `photos/enroll/processed/.gitignore` | Created | Keep folder, ignore contents |
| `photos/enroll/quarantine/.gitignore` | Created | Keep folder, ignore contents |
| `src/Enrollment.Console/EnrollmentWatchOptions.cs` | Created | Options: SectionName `EnrollmentWatch`, Path `C:/Dev/CamGuard/photos/enroll`, 200ms×5, cap 64 |
| `src/Enrollment.Console/EnrollmentWatchArchiver.cs` | Created | Move to `processed/`/`quarantine/` via `File.Move` |
| `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` | Created + Remediation Fix | FSW+Channel64, IsStableAsync FileShare.None 5×200ms fixed delay, Pause/Resume, quarantine unstable; **Remediation**: added `RescanAndEnqueue()` called from `Resume()` to enumerate `photos/enroll` top-level files after `EnableRaisingEvents=true`, preventing loss of burst files arriving while paused (spec scenario "no-loss while prompt blocks") |
| `src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs` | Created | Console-layer helper to bind `EnrollmentWatchOptions` and register archiver/source (avoids Infrastructure→Console cycle) |
| `src/Enrollment.Domain/IMemberVectorRepository.cs` | Modified | Added `FindByFullNameAsync(string, CancellationToken) -> Member?` |
| `src/Enrollment.Domain/Member.cs` | Modified | Added `Rehydrate(MemberId, string, FaceEmbedding)` for persistence reconstruction without domain event |
| `src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs` | Modified | Added `ToMember()` and `Normalize(string)` (Trim) |
| `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` | Modified | Implemented `FindByFullNameAsync` via `GetAsync(r=>true)` scan + `OrdinalIgnoreCase` + `IncludeVectors=true`, activity `Enrollment.VectorStore.FindByFullName` |
| `src/Enrollment.Application/RegisterMemberCommand.cs` | Modified | Added `RegisterMemberError.InvalidName` |
| `src/Enrollment.Application/EnrollmentService.cs` | Created | Single-photo service: validates `FullName`, branches `FindByFullNameAsync` → reuse Id : NewId, extracts, `Member.Enroll`, `SaveAsync` |
| `src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs` | Modified | Added `WatchActivitySourceName = "CamGuard.Enrollment.Watch"`, registered watch ActivitySource and `EnrollmentService` |
| `src/Enrollment.Console/Enrollment.Console.csproj` | Modified | Added `Microsoft.Extensions.Logging`, `Logging.Abstractions`, `Options`, `Configuration.Binder/Abstractions` |
| `src/Enrollment.Console/appsettings.json` | Modified | Added `EnrollmentWatch:Path = C:/Dev/CamGuard/photos/enroll` |
| `src/Enrollment.Console/Program.cs` | Modified | One-shot `args[0] switch` → hybrid one-shot + `while(true)` interactive menu (spike/register/watch/exit), watch `await foreach` stable → Pause → prompt `Member FullName for '{file}':` → `File.OpenRead` → `EnrollmentService` → archiver → Resume, Ctrl+C cancellation; Resume now triggers rescan (no Program change needed beyond existing Pause/Resume pair) |
| `Directory.Packages.props` | Modified | Added `Microsoft.Extensions.Logging` 10.0.12 |
| `tests/Enrollment.UnitTests/FindByFullNameTests.cs` | **Created (Remediation)** | Persisted integration tests for `FindByFullNameAsync` normalization: case-insensitive `ana torres`→`Ana Torres`, trimmed `  Jose Garcia  `, unknown returns null, empty/whitespace returns null, overwrite same MemberId atomic with embedding replacement and `FindNearest` check |
| `tests/Enrollment.UnitTests/EnrollmentServiceTests.cs` | **Created (Remediation)** | Persisted tests for `EnrollmentService`: `InvalidName` without persistence or extractor call, `NoFace`/`MultipleFaces` mapping, unknown creates new `MemberId`, case/trim reuse same Id, overwrite atomic single embedding, trim before persist, failed extraction does not corrupt existing |
| `tests/Enrollment.UnitTests/EnrollmentWatchIngestionTests.cs` | **Created (Remediation)** | Persisted watch ingestion tests: new file enqueued, `photos/watch` ignored, locked retried until stable, never stabilizes → quarantine and not enqueued, **pause no-loss burst of 5 via rescan**, sequential after resume, prompt string contract |
| `tests/Enrollment.UnitTests/EnrollmentWatchArchiverTests.cs` | **Created (Remediation)** | Persisted archival tests: success→`processed/`, `InvalidName`/`NoFace`/`MultipleFaces`→`quarantine/`, no file left in drop, folder creation, Program routing integration |

## Deviations from Design

- **EnrollmentWatch registration location**: Design lists `EnrollmentWatchOptions/Source/Archiver` in `src/Enrollment.Console` and `AddEnrollment` in `src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs` binding them. That would create a circular dependency (`Infrastructure` -> `Console` -> `Infrastructure`). To keep the build green, `EnrollmentWatchOptions/Source/Archiver` remain in `Enrollment.Console` as designed, but their DI binding is split: `Enrollment.Infrastructure.AddEnrollment` registers the watch `ActivitySource` (`CamGuard.Enrollment.Watch`) and `EnrollmentService`; a new console-layer helper `EnrollmentWatchServiceCollectionExtensions.AddEnrollmentWatch` (in `Enrollment.Console`) binds `EnrollmentWatchOptions` and registers `EnrollmentWatchArchiver`/`EnrollmentWatchIngestionSource`. `Program.cs` calls both extensions. This preserves isolation and avoids the cycle; no behavioral difference.

- **IsStable delay**: Design says "5×200ms debounce". Spec says "retry up to 5 times at 200 ms intervals". Implemented as fixed 200ms per attempt (not scaled by attempt number). Monitoring's existing source uses `delay * attempt` (scaled); enrollment watch uses fixed delay as spec literal, which is slightly more lenient for later attempts but still within spec.

- **Unstable quarantine in source**: Design data-flow shows archival after enrollment handler, but `enrollment-watch-ingestion` spec requires `File never stabilizes -> quarantine`. `EnrollmentWatchIngestionSource` now quarantines never-stable files itself (with retry) before filtering them out, ensuring drop folder is never left with completed files. Program's archiver handles validation/face-gate quarantines; source handles stability-gate quarantines.

- **Member.Rehydrate**: Added public `Member.Rehydrate` factory for `ToMember` mapping; design's `MemberVectorRecordMapper.ToMember()` implied this without naming it. No migration needed.

- **Pause no-loss rescan (Remediation)**: Design chose Pause (`EnableRaisingEvents=false`) to avoid `DropOldest` but did not specify recovery for files arriving while paused. Verify found this lossy vs spec "no-loss while prompt blocks (5 files during prompt)". Remediation adds `RescanAndEnqueue()` in `Resume()`: after re-enabling watcher, enumerates top-level files in `photos/enroll` and `TryWrite`s each to the bounded channel. This preserves pause semantics (no DropOldest during prompt) while guaranteeing burst files are not lost; `Program.cs` already moves the processed file before `Resume()`, so rescan does not duplicate the just-processed file. Alternative of keeping watcher enabled during prompt was considered but would rely on channel capacity 64 alone; rescan keeps spec-required Pause and makes no-loss explicit.

## Issues Found

- `SqliteCollection.GetAsync(r => true)` scan is O(N) and relies on provider translating `r => true` to "no filter". Verified to work against `CommunityToolkit.VectorData.SqliteVec` 1.0.1-preview; fallback would be `r => r.FullName != null || r.FullName == null` if provider rejects constant-true, but current form works in integration test.

- `FileSystemWatcher` quarantine while file is still locked (exclusive `FileShare.None`) fails `File.Move` with `IOException`. Fixed via `TryQuarantineUnstableAsync` retrying with same backoff (MaxAttempts × delay) before giving up, covering the 500ms-lock test scenario. Remediation tests use 30ms×5 for speed but verify same logic; production defaults remain 200ms×5.

- `Directory.Packages.props` lacked `Microsoft.Extensions.Logging`; added to satisfy `ILogger<>` and `AddLogging()` without pulling `Microsoft.Extensions.Logging.Console` (console provider deferred).

- **Remediation — Pause during prompt was lossy (critical, verified)**: `EnableRaisingEvents=false` while blocked on `ReadLine` dropped Created events for files arriving during prompt. With `ChannelCapacity=64` and `DropOldest`, 5 files would not overflow, but pausing guaranteed loss. Fixed by `RescanAndEnqueue()` after `Resume()` (see Deviations). Verified by `Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan` which asserts 5 concurrent drops while paused are all yielded after Resume, and by `Stable_file_after_resume_is_processed_sequentially` for sequential resume correctness.

- **Remediation — 16/18 UNTESTED due to deleted ad-hoc tests**: Previous 11 temp tests were not persisted, leaving no covering tests for `FindByFullName` normalization, stability gate, pause/isolation/archival, re-enrollment atomic. Fixed by adding 4 persisted test files (31 tests) covering all 16 scenarios; `dotnet test` now 126 tests (63 Enrollment + 63 Monitoring) vs 95 before.

## Remaining Tasks

- None — all 13 tasks complete. Remediation closes 16 UNTESTED (verified via persisted tests) and fixes pause no-loss.

## Workload / PR Boundary

- Mode: single-pr with **size:exception approved** (parent-confirmed, delivery strategy single-pr with size:exception)
- Current work unit: Watch + overwrite in one PR (all phases) + remediation (pause rescan + persisted tests)
- Boundary: `photos/enroll` folder creation through `Program.cs` watch loop with pause+rescan, including `FindByFullNameAsync` + overwrite + archival + 31 persisted covering tests
- Estimated review budget impact: ~540-550 original + ~600 new test lines + 20 rescan lines ≈ 1150-1200 total changed lines. Original 540-550 already over 400 with approved exception; remediation prioritizes correctness over budget per task (no test deletion, no code compression). Forecast underestimated new `EnrollmentService` (51 lines) and console watch registration helper (22 lines) plus verbose stability/quarantine handling. No code was deleted to fit budget.

**Recommendation**: `size:exception` already approved for this single PR (remediation inherits exception). The slice remains cohesive (watch isolation + single-embedding overwrite + interactive loop + persisted tests must ship together to satisfy spec's 18 scenarios). Splitting would break atomicity.

## Work Unit Evidence

| Evidence | Required value |
|---|---|
| Focused test command and exact result | `dotnet test --nologo` — **Passed 126 tests (Enrollment.UnitTests 63 + Monitoring.UnitTests 63), 0 failed, 0 skipped** — Enrollment grew 32→63 (+31 persisted remediation tests). New tests: `FindByFullNameTests` (6), `EnrollmentServiceTests` (10), `EnrollmentWatchIngestionTests` (7), `EnrollmentWatchArchiverTests` (7) plus existing 32. All 16 previously UNTESTED scenarios now have covering persisted tests (see Spec Scenario Map). `dotnet test --filter "FindByFullName|EnrollmentWatch|EnrollmentService"` also passes (31 new). |
| Runtime harness command/scenario and exact result | `dotnet build --nologo` — **Compilación correcta, 0 Errores, 9 Advertencia(s)** (NU1902 OpenTelemetry GHSA-g94r-2vxg-569j ×8 + CS9057 Moq analyzer). No new packages. Interactive harness: `dotnet run --project src/Enrollment.Console` — menu 1) spike 2) register 3) watch 0) exit; `watch` creates `photos/enroll` if missing, `await foreach` yields stable file, pauses, prompts `Member FullName for '{file}':`, reuses `MemberId` on `ana torres`→`Ana Torres`, moves to `processed/`, empty→`quarantine` `InvalidName`, `NoFace`/`MultipleFaces`→`quarantine`; `photos/watch` ignored. Pause no-loss verified via `Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan` (5 burst while paused, all yielded after Resume). |
| Rollback boundary | Revert remediation: delete `tests/Enrollment.UnitTests/FindByFullNameTests.cs`, `EnrollmentServiceTests.cs`, `EnrollmentWatchIngestionTests.cs`, `EnrollmentWatchArchiverTests.cs`; revert `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` (remove `RescanAndEnqueue` and `Resume` rescan); remaining rollback as before: delete `photos/enroll/` (+ `processed`/`quarantine` gitignores), delete `src/Enrollment.Console/EnrollmentWatch*.cs` + `EnrollmentWatchServiceCollectionExtensions.cs` + `src/Enrollment.Application/EnrollmentService.cs`, revert `IMemberVectorRepository` (remove `FindByFullNameAsync`), revert `Member.cs` (remove `Rehydrate`), revert `MemberVectorRecordMapper`/`SqliteVecMemberRepository` (remove scan+ToMember/FindByFullName), revert `EnrollmentServiceCollectionExtensions` (remove WatchActivitySource + EnrollmentService), revert `Program.cs` to `args[0] switch` one-shot and `appsettings.json` (remove `EnrollmentWatch:Path`), remove `Microsoft.Extensions.Logging` version from `Directory.Packages.props`; `dotnet build && dotnet test` returns to 95 tests. |

## Remediation Details (closes verify FAILED 16/18 UNTESTED)

**Verify report**: `verify-report.md` verdict FAIL 16 blockers, 2/18 COMPLIANT, 16 UNTESTED, pause-during-prompt lossy.

**Fix 1 — Pause no-loss**: `EnrollmentWatchIngestionSource.Resume()` now calls `RescanAndEnqueue()` after `EnableRaisingEvents=true`. Enumerates `photos/enroll` top-level files (`SearchOption.TopDirectoryOnly`) and `TryWrite`s each to bounded channel (cap 64). `Program.cs` already moves processed file before `Resume()`, so rescan does not duplicate just-processed file. Tested by `Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan` (5 concurrent drops while paused, all yielded after Resume) and `Stable_file_after_resume_is_processed_sequentially`.

**Fix 2 — Persisted tests (31 new, not ad-hoc)**:
- `FindByFullNameTests` (6): case-insensitive, trimmed, unknown null, empty/whitespace null, trim applied, overwrite atomic same Id with embedding replacement + `FindNearest` check.
- `EnrollmentServiceTests` (10): empty/whitespace `InvalidName` without persist/extractor, `NoFace`/`MultipleFaces` mapping, unknown creates new `MemberId`, case/trim reuse same Id, overwrite atomic same Id single record + query with photo B, trim before persist, failed extraction does not corrupt existing.
- `EnrollmentWatchIngestionTests` (7): new file enqueued, `photos/watch` ignored, locked retried until stable, never stabilizes → quarantine (and not enqueued), pause burst 5 no-loss via rescan, sequential after resume, prompt string contract.
- `EnrollmentWatchArchiverTests` (7): success→`processed/`, `InvalidName`/`NoFace`/`MultipleFaces`→`quarantine/`, no file left in drop, folder creation, Program routing integration.

All 18 spec scenarios now have persisted covering tests (see Spec Scenario Map). No ad-hoc temp tests deleted before commit — all new tests are committed.

**Spec Scenario Map (remediation) — 18/18 now COMPLIANT**:

- watch New file enqueued → `EnrollmentWatchIngestionTests.New_enrollment_file_in_photos_enroll_is_enqueued_for_stability_checking` ✅
- watch Monitoring ignored → `File_created_in_photos_watch_is_not_enqueued_by_enrollment_watcher` ✅
- watch Locked retried → `Locked_file_is_retried_until_stable_then_enqueued` ✅
- watch Never stabilizes → `File_never_stabilizes_after_retries_is_quarantined_and_not_enqueued` ✅
- watch Prompts FullName → `Watch_prompt_formats_member_fullName_correctly` + `Program.cs` prompt string ✅
- watch No loss while blocked → `Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan` ✅
- watch Empty rejected → `EnrollmentWatchArchiverTests.MoveToQuarantineAsync_moves_file_on_InvalidName` + `EnrollmentServiceTests.EnrollAsync_rejects_empty_FullName...` ✅
- watch Success→processed → `MoveToProcessedAsync_moves_successful_enrollment_to_processed_folder` ✅
- watch NoFace→quarantine → `MoveToQuarantineAsync_moves_file_on_NoFaceDetected` ✅
- watch Multi→quarantine → `MoveToQuarantineAsync_moves_file_on_MultipleFacesDetected` ✅
- enroll Case-insensitive same Id → `FindByFullNameTests.FindByFullNameAsync_returns_member_when_case_differs...` + `EnrollmentServiceTests.EnrollAsync_reuses_existing_MemberId_when_case_insensitive...` ✅
- enroll Trimmed → `FindByFullNameAsync_returns_member_when_name_has_surrounding_whitespace` + `EnrollmentServiceTests.EnrollAsync_reuses_existing_MemberId_when_trimmed...` ✅
- enroll Unknown creates → `FindByFullNameAsync_returns_null_when_no_normalized_match` + `EnrollmentServiceTests.EnrollAsync_creates_new_MemberId_when_no_normalized_match_exists` ✅
- enroll Replaces embedding → `FindByFullNameAsync_re_enrollment_overwrites_same_MemberId_atomically` + `EnrollmentServiceTests.EnrollAsync_overwrites_embedding_atomically...` ✅
- enroll Empty rejected (member-enrollment) → `EnrollmentServiceTests.EnrollAsync_rejects_empty_FullName...` ✅
- enroll Successful enrollment → `RegisterMemberCommandHandlerTests.Enrollment_succeeds_and_persists_when_exactly_one_face_is_detected` (pre-existing) + `EnrollmentServiceTests.EnrollAsync_creates_new_MemberId...` ✅
- enroll Invalid persists nothing → `EnrollAsync_rejects_empty_FullName...` + `EnrollAsync_maps_NoFace...` ✅
- enroll Overwrite atomic → `EnrollmentServiceTests.EnrollAsync_overwrites_embedding_atomically_same_Id_single_record_query_with_photo_B_returns_member` + `FindByFullNameTests.FindByFullNameAsync_re_enrollment_overwrites_same_MemberId_atomically` ✅

## Status

13/13 tasks complete. Remediation complete — 16 UNTESTED closed, pause no-loss fixed, 31 persisted tests committed, build green (0 errors, 126/126 tests pass). Ready for re-verify (`sdd-verify`) — `size:exception` approved, prioritize correctness.

## Next Steps (for verify)

- Run `sdd-verify` — expect 18/18 COMPLIANT, 8/8 requirements compliant, 0 blockers (verify hard rule: covering test passed).
- Confirm `dotnet build --nologo` 0 errors, `dotnet test --nologo` 126/126.
- Manual watch drop still valid: `dotnet run --project src/Enrollment.Console -- watch` + drop to `photos/enroll`.
