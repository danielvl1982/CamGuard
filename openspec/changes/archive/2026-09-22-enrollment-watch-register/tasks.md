# Tasks: enrollment-watch-register

**Remediation** (2026-09-18): closes verify FAILED 16/18 UNTESTED + fixes pause-during-prompt loss via `RescanAndEnqueue()` in `EnrollmentWatchIngestionSource.Resume()`; adds 31 persisted tests under `tests/Enrollment.UnitTests/` (FindByFullName, EnrollmentService, EnrollmentWatchIngestion, EnrollmentWatchArchiver) — size:exception inherits approval.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 310-360 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

3 new files (~165) + 5 modified (~125) + config (~10); no migration/packages. Buffer 40-90.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Watch + overwrite in one PR | PR 1 | `dotnet test --filter "FindByFullName|EnrollmentWatch"` | `dotnet run --project src/Enrollment.Console -- watch` + drop to `photos/enroll` | Revert `EnrollmentWatch*`, `FindByFullNameAsync`, `Program.cs`, `appsettings.json` |

## Phase 1: Foundation

- [x] 1.1 Create `photos/enroll/.gitignore` (`*`, `!processed/`, `!quarantine/`) [P0]
- [x] 1.2 Create `src/Enrollment.Console/EnrollmentWatchOptions.cs` (SectionName, Path, 200ms×5, cap64) [P0]
- [x] 1.3 Create `src/Enrollment.Console/EnrollmentWatchArchiver.cs` (File.Move to processed/quarantine) [P0] Depends: 1.2

## Phase 2: Domain & Persistence

- [x] 2.1 Add `FindByFullNameAsync` to `src/Enrollment.Domain/IMemberVectorRepository.cs` [P0]
- [x] 2.2 Implement scan+Normalize and `ToMember()` in `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` + `src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs` [P0] Depends: 2.1
- [x] 2.3 Create `src/Enrollment.Application/EnrollmentService.cs` (branch Find→Upsert vs Enroll, InvalidName); update `src/Enrollment.Application/RegisterMemberCommand.cs` [P0] Depends: 2.2

## Phase 3: Watch Ingestion

- [x] 3.1 Create `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` (FSW+Channel64, IsStableAsync FileShare.None 5×200ms, Pause/Resume) [P0] Depends: 1.2
- [x] 3.2 Register options/source/archiver in `src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs` and add `EnrollmentWatch:Path` to `src/Enrollment.Console/appsettings.json` [P0] Depends: 3.1

## Phase 4: Console Wiring

- [x] 4.1 Refactor `src/Enrollment.Console/Program.cs` to `while(true)` menu; watch `await foreach` stable→Pause→prompt `Member FullName for '{file}':`→`File.OpenRead`→handler→archiver→Resume [P0] Depends: 2.3,3.1

## Phase 5: Testing

- [x] 5.1 Test `FindByFullNameAsync` normalize + overwrite atomic (case, trimmed, unknown, same-Id replace, count) [P1] Depends: 2.2,2.3 — covers member-enroll case/trim/unknown/replace/atomic
- [x] 5.2 Test InvalidName + stability gate (empty→quarantine, locked 5×200ms→quarantine) [P1] Depends: 3.1 — covers watch Locked/never-stable + both Empty
- [x] 5.3 Test pause/isolation/archival (5 queued during prompt not lost, `photos/watch` ignored, success→processed, NoFace/Multi→quarantine) [P1] Depends: 4.1 — covers No-loss, Isolation, Success/NoFace/Multi
- [x] 5.4 Run `dotnet build && dotnet test` + manual watch drop [P1] Depends: all

## Spec Scenario Map

- watch New file enqueued → 3.1,5.3
- watch Monitoring ignored → 5.3
- watch Locked retried → 5.2
- watch Never stabilizes → 5.2
- watch Prompts FullName → 4.1
- watch No loss while blocked → 5.3
- watch Empty rejected → 5.2
- watch Success→processed → 5.3
- watch NoFace→quarantine → 5.3
- watch Multi→quarantine → 5.3
- enroll Case-insensitive same Id → 5.1
- enroll Trimmed → 5.1
- enroll Unknown creates → 5.1
- enroll Replaces embedding → 5.1
- enroll Empty rejected → 5.2
- enroll Successful enrollment → 5.1
- enroll Invalid persists nothing → 5.1,5.3
- enroll Overwrite atomic → 5.1

## Commit Plan

1. `feat: watch folder+options` — 1.1,1.2
2. `feat: FindByFullName+overwrite` — 2.1-2.3
3. `feat: ingestion source+archiver` — 3.1,3.2
4. `feat: watch loop with pause` — 4.1
5. `test: watch+re-enroll` — 5.1-5.4

Threat matrix: N/A
