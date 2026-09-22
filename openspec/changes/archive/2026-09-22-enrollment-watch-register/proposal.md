# Proposal: enrollment-watch-register

## Intent

`Enrollment.Console` is one-shot (`spike`/`register` then exit) — no folder watch, no loop. Add interactive loop plus watched folder separate from `photos/watch`, prompting `FullName` per photo to create or overwrite single embedding.

## Scope

### In Scope
- Interactive menu loop (`spike` / `register` manual / `watch` / `exit`) — multiple iterations
- Watch `C:/Dev/CamGuard/photos/enroll` via `FileSystemWatcher`+`Channel`+`FileShare.None` 5×200ms + `processed/`/`quarantine/`
- Normalized lookup `IMemberVectorRepository.FindByFullNameAsync` (trim + `OrdinalIgnoreCase`)
- Single-embedding overwrite: existing → Upsert same `MemberId`; new → `Member.Enroll`
- `EnrollmentWatchOptions` + `appsettings.json` + DI, scope `CamGuard.Enrollment.Watch`
- `photos/enroll/.gitignore` (allowlist `processed/`/`quarantine/`)

### Out of Scope
- Multi-embedding additive storage — deferred
- Vector/threshold duplicate warning (`AllowedMaxDistance`) — deferred
- Absolute `ConnectionString` portability fix — risk only
- Changes to `photos/watch` or `motion-image-ingestion`

## Capabilities

### New Capabilities
- `enrollment-watch-ingestion`: enrollment folder-watch, stability, queueing, archival, per-file prompt; isolated from `motion-image-ingestion`

### Modified Capabilities
- `member-enrollment`: re-enrollment overwrite — `FindByFullNameAsync`, case-insensitive, single-embedding replacement, atomic gate per photo

## Approach

Reuse Monitoring's `FileSystemWatchIngestionSource`/`FileSystemImageArchiver` as `EnrollmentWatchIngestionSource`+`Options` (~64). `Program.cs` → `while(true)` menu; `watch` starts watcher, `await foreach` stable paths, pauses watcher while `ReadLine` blocks to avoid `DropOldest`, validates name, runs handler with `File.OpenRead`, archives. Handler: `FindByFullNameAsync` → found → overwrite; not found → `Enroll`. Keep one-embedding invariant.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/Enrollment.Console/Program.cs` | Modified | One-shot → loop + `watch` sub-mode |
| `src/Enrollment.Application/IMemberVectorRepository.cs` | Modified | Add `FindByFullNameAsync` |
| `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` | Modified | Normalized scan of `members` |
| `src/Enrollment.Console/EnrollmentWatch*.cs` | New | Watcher/archiver/options (adapted) |
| `src/Enrollment.Console/appsettings.json` | Modified | `EnrollmentWatch:Path` = `.../photos/enroll` |
| `photos/enroll/` | New | Folder + `.gitignore` + subfolders |
| `openspec/specs/member-enrollment/spec.md` | Modified | Delta: re-enrollment overwrite |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Prompt blocks → `DropOldest` drops | Med | Pause watcher during `ReadLine` |
| Overwrite surprises (additive expected) | Med | Log `Replaced embedding`; defer multi-embedding explicitly |
| `FullName` duplicates (casing/typos) | Med | Normalize at write + read; spec scenario |
| Absolute path non-portable | Low | Document; keep configured path |

## Rollback Plan

Delete `photos/enroll/`; revert `AddEnrollment` + delete `EnrollmentWatch*.cs`; remove `FindByFullNameAsync` + impl; revert `Program.cs` to `args[0] switch` and `appsettings.json`; `dotnet build && dotnet test` — no DB migration (Upsert reuse).

## Dependencies

- `CommunityToolkit.VectorData.SqliteVec`, `FaceAiSharp` via `FaceAiSharpBundleFactory` — existing, no new packages

## Success Criteria

- [ ] Menu supports `spike`→`register`→`watch`→`exit` without restart
- [ ] Photo in `photos/enroll` prompts `FullName`; new creates, existing (case-insensitive) overwrites same `MemberId`
- [ ] Invalid photo (0/2+ faces) quarantines, persists nothing
- [ ] `photos/watch` and Monitoring unchanged
- [ ] Diff <400 lines; `FindByFullNameAsync` and overwrite tested
