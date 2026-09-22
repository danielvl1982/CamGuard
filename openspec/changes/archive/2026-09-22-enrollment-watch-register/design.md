# Design: enrollment-watch-register

## Technical Approach

Reuse Monitoring's `FileSystemWatcher`+`Channel`+`FileShare.None` 5x200ms for isolated `photos/enroll` (not `photos/watch`). `Program.cs` becomes `while(true)` menu (1:spike,2:register,3:watch,0:exit). `watch` yields stable paths via `await foreach`, pauses watcher during `ReadLine` (`EnableRaisingEvents=false`) to avoid `DropOldest`, prompts `Member FullName for '{file}':`, validates, then branches on `FindByFullNameAsync` (trim+OrdinalIgnoreCase): found->Upsert same `MemberId`, not found->`Enroll`. Archival to `processed/`/`quarantine/`. Covers `enrollment-watch-ingestion` and `member-enrollment` delta. net10.0, CPM `Directory.Packages.props`, no new packages.

## Architecture Decisions

### Decision: Watch folder isolation

| Option | Tradeoff | Decision |
|---|---|---|
| Reuse `photos/watch` | No new folder but violates spec isolation, mixes Monitoring | Rejected |
| `photos/enroll` + `processed/`/`quarantine/` | Clean isolation, `EnrollmentWatchOptions.Path` configurable | **Chosen** |

**Rationale**: Spec mandates isolation; `Monitoring.Host` keeps `photos/watch`.

### Decision: Watch mechanism

| Option | Tradeoff | Decision |
|---|---|---|
| Polling `GetFiles` | Simple, no buffer overflow | Rejected — diverges from proven pattern, same stability code |
| `FileSystemWatcher`+`Channel` 5x200ms | Proven, familiar to reviewer | **Chosen** |

**Rationale**: Reuse minimizes risk. Capacity 64 (vs 256 for Monitoring) fits interactive rate.

### Decision: Prompt blocking

| Option | Tradeoff | Decision |
|---|---|---|
| Keep `DropOldest` during `ReadLine` | Simple but drops burst files | Rejected |
| Pause watcher while blocked, resume after | No loss, bounded | **Chosen** |

**Rationale**: Required by spec scenario (5 files during prompt not lost).

### Decision: Embedding cardinality

| Option | Tradeoff | Decision |
|---|---|---|
| Multi-embedding (N rows) | Keeps history | Deferred — breaks key, migration, >400 lines |
| Single-embedding overwrite | Last wins, zero schema change | **Chosen** |

**Rationale**: Protects budget; additive is explicit follow-up.

### Decision: FindByFullNameAsync

| Option | Tradeoff | Decision |
|---|---|---|
| SQL `COLLATE NOCASE` index | Fast but leaks abstraction, no index exists | Rejected |
| Client-side scan + Normalize() | O(N), N small, no migration | **Chosen** |

**Rationale**: Simplest correct for family-scale N; shared helper at write+read.

## Data Flow

```
photos/enroll/ -> FSW -> Channel -> IsStableAsync (FileShare.None 5x200ms)
       -> await foreach stable path
       -> Pause watcher, prompt FullName, validate
          -> empty -> quarantine, InvalidName
          -> FindByFullNameAsync -> found? Upsert same Id : Enroll new Id
          -> ExtractAsync(File.OpenRead) -> 0/2+ faces -> quarantine else SaveAsync
       -> Archiver.MoveToProcessed/Quarantine (File.Move) -> Resume watcher
```

No agent workflow -> no sequence diagram.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/Enrollment.Console/EnrollmentWatchOptions.cs` | Create | Options: Path, 200ms, 5 attempts, cap 64 |
| `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` | Create | FSW+Channel source with Pause/Resume |
| `src/Enrollment.Console/EnrollmentWatchArchiver.cs` | Create | Move to `processed/`/`quarantine/` |
| `src/Enrollment.Domain/IMemberVectorRepository.cs` | Modify | Add `FindByFullNameAsync` |
| `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` | Modify | Scan + Normalize |
| `src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs` | Modify | Add `ToMember` |
| `src/Enrollment.Application/RegisterMemberCommand.cs` | Modify | Extract `EnrollmentService` |
| `src/Enrollment.Console/Program.cs` | Modify | One-shot -> loop menu |
| `src/Enrollment.Console/appsettings.json` | Modify | Add `EnrollmentWatch:Path` |
| `photos/enroll/.gitignore` | Create | `*` + `!processed/` + `!quarantine/` |

## Interfaces / Contracts

```csharp
public interface IMemberVectorRepository {
    Task SaveAsync(Member m, CancellationToken ct);
    Task<(MemberId,double)?> FindNearestAsync(FaceEmbedding q, CancellationToken ct);
    Task<Member?> FindByFullNameAsync(string fullName, CancellationToken ct);
}
public sealed class EnrollmentWatchOptions {
    public const string SectionName = "EnrollmentWatch";
    public string Path { get; set; } = "C:/Dev/CamGuard/photos/enroll";
    public int StabilityRetryDelayMilliseconds { get; set; } = 200;
    public int MaxStabilityRetryAttempts { get; set; } = 5;
    public int ChannelCapacity { get; set; } = 64;
}
public sealed class EnrollmentWatchIngestionSource : IAsyncDisposable {
    public IAsyncEnumerable<string> WatchAsync(CancellationToken ct);
    public void Pause(); public void Resume();
}
public sealed class EnrollmentService {
    public Task<Result<MemberId,RegisterMemberError>> EnrollAsync(string fullName, Stream photo, CancellationToken ct);
}
public enum RegisterMemberError { NoFaceDetected, MultipleFacesDetected, ExtractionFailed, InvalidMember, InvalidName }
```

DI: `AddEnrollment` binds `EnrollmentWatchOptions` via closure, registers source/archiver, activity `CamGuard.Enrollment.Watch`.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Normalize + scan case-insensitive | In-memory SqliteCollection |
| Unit | Overwrite same Id | Stub extractor+repo |
| Unit | Empty -> InvalidName, no persist | Mock archiver |
| Integration | Locked -> quarantine | Temp folder |
| Integration | Pause avoids DropOldest | Pause/Resume test |
| Manual | Case-variant overwrite | Drop file, type `ana torres` |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

No migration. Upsert reuses `MemberId` key. Rollback: delete `photos/enroll/`, revert `AddEnrollment`/new files/`FindByFullNameAsync`/`Program.cs`/`appsettings.json`.

## Open Questions

- [ ] Absolute ConnectionString portability — follow-up?
- [ ] Capacity 64 enough for burst sync?
- [ ] Multi-embedding key scheme for follow-up?
