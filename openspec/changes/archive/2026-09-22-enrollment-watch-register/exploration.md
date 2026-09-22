# Exploration: enrollment-watch-register

## Current State

`Enrollment.Console/Program.cs` is a one-shot CLI: `args[0] switch { spike, register }` with no loop — it parses `register "<Full Name>" <photo...>` into a `RegisterMemberCommand(FullName, IReadOnlyList<Stream>)` and exits. Handler `RegisterMemberCommandHandler` gates every submitted photo through `IFaceEmbeddingExtractor.ExtractAsync` (SCRFD: reject 0 or 2+ faces), and only when ALL pass does it call `Member.Enroll(id, fullName, embeddings[0])` and `SaveAsync` once (atomic persistence, `member-enrollment` spec). `Member` (`Enrollment.Domain`) models **exactly one `FaceEmbedding`** (`ExpectedDimension=512`, `Member.Enroll` raises `MemberEnrolled`).

Persistence is `SqliteVecMemberRepository` over `CommunityToolkit.VectorData.SqliteVec` — `MemberVectorRecord { [VectorStoreKey] string Id, [VectorStoreData] string FullName, [VectorStoreVector(512, CosineDistance)] float[] Embedding }` keyed by `MemberId.Value.ToString()`. `FindNearestAsync` searches cosine distance; `IMemberVectorRepository` has only `SaveAsync`/`FindNearestAsync`, no lookup by name. No query by `FullName` exists anywhere (`Enrollment.Application`, `PublicApi`).

`Monitoring.Host` already watches `photos/watch` via `FileSystemWatchIngestionSource` (FileSystemWatcher -> bounded `Channel<string>` (capacity 256, DropOldest) + debounce stability check `File.Open(..., FileShare.None)` with 5 retries x 200ms backoff -> `IImageArchiver` moves to `processed/`/`quarantine/`). `photos/` today: `photos/.gitignore` (allowlisted), `photos/watch/.gitignore` (`*` + `!processed/`), `photos/watch/processed/.gitignore`. `VectorStore` ConnectionString is `Data Source=C:/Dev/CamGuard/camguard-enrollment.db` (absolute Windows path, shared by both modules) / `MembersCollectionName=members`. `Monitoring.Host/appsettings.json:WatchFolder.Path=C:/Dev/CamGuard/photos/watch`.

Tests: `Enrollment.UnitTests` 32 / `Monitoring.UnitTests` 62 passing. `MemberDirectoryReader` (`Enrollment.PublicApi`) is the only public surface for Monitoring — it delegates to `QueryByEmbeddingQueryHandler` -> `FindNearestAsync`. `EnrollmentServiceCollectionExtensions.AddEnrollment` registers `IFaceDetectorWithLandmarks`/`IFaceEmbeddingsGenerator` as singletons via `FaceAiSharpBundleFactory`, plus scoped extractor/repo/handlers.

## Affected Areas

- `src/Enrollment.Console/Program.cs` — one-shot switch must become interactive menu loop with multiple iterations; add `watch` command/mode that prompts per photo
- `src/Enrollment.Application/RegisterMemberCommand.cs` — currently `SaveAsync(Member)` once per enrollment; extended to handle add-photo-to-existing-member (upsert vs. second row), needs branching on member existence
- `src/Enrollment.Domain/Member.cs` — single `FaceEmbedding` invariant must be revisited for "add photo to existing member"; either keep invariant and overwrite or remodel to multiple embeddings per member
- `src/Enrollment.Domain/IMemberVectorRepository.cs` — needs `FindByFullNameAsync` or `ExistsAsync` (or at least read-before-write) for name-based lookup; key semantics collide with multi-embedding
- `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` + `MemberVectorRecord.cs` + `MemberVectorRecordMapper.cs` — single-row-per-member with `Id == MemberId` cannot hold N embeddings; multi-embedding requires new record keying (e.g., `MemberId#ordinal`) or separate `Embedding` collection and nearest-search deduplication
- `src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs` — DI extension to register new watch service / options if watch runs as hosted loop inside Console
- `src/Enrollment.Console/appsettings.json` + `src/Monitoring.Host/appsettings.json` — new `EnrollmentWatch`/`Enrollment.WatchFolder` section must point to a folder **separate** from `photos/watch` (the user's brief explicitly requires this separation)
- `photos/` layout — new folder `photos/enroll` (or `photos/inbox`/`photos/drop`) with its own `.gitignore`, `processed/`, `quarantine/` subfolders; `photos/watch` remains Monitoring's drop zone
- `openspec/specs/member-enrollment/spec.md` — Out of Scope note "re-enrollment ... NOT covered" will need delta; `member-directory-query` may gain a by-name query scenario
- `tests/Enrollment.UnitTests/*` — `MemberTests`, `RegisterMemberCommandHandlerTests`, `SqliteVecMemberRepositoryTests` must cover new paths (name lookup, overwrite vs. additive, watch stability)

## Approaches

### A — Watch Mechanism: Reuse FileSystemWatcher+Channel (Monitoring pattern) vs. Polling

1. **Reuse `FileSystemWatcher` + bounded `Channel<string>` + debounce retry (Monitoring's `FileSystemWatchIngestionSource` / `FileSystemImageArchiver` pattern)** — Extract a shared abstraction or duplicate it scoped to Enrollment.Console; watcher on `photos/enroll` yields stability-checked paths, console prompts for name per file, then moves to `processed/` or `quarantine/`.
   - Pros: Proven, already tested against `FileSystemWatcher` buffer-overflow and partial-write races; preserves reviewer familiarity; bounded channel protects memory; matches `motion-image-ingestion` spec language exactly.
   - Cons: Prompt is a **blocking** `Console.ReadLine` — while the user is typing, `Channel` fills (DropOldest loses events); `FileSystemWatcher` `Error` event (buffer overflow) needs surfacing; interactive console cannot be a `BackgroundService` like `MonitoringHostedService`.
   - Effort: Medium — copy/adapt 2 classes + options, wire menu integration, handle channel-full feedback.

2. **Polling scan (`Directory.GetFiles` every N seconds)** — Loop enumerates `photos/enroll/*.{jpg,png}`, filters out `processed/`/`quarantine/`, processes new files deterministically.
   - Pros: Trivially testable (no `FileSystemWatcher` mocking), no buffer-overflow or `Created` vs `Changed` subtlety, behaves identically on Windows/Linux/macOS file systems.
   - Cons: Latency (poll interval trade-off), continuous IO even when idle, races with files still being copied (still needs same stability `FileShare.None` retry — not actually simpler), diverges from Monitoring's established pattern for no principled reason.
   - Effort: Low for polling itself; same prompting/archival complexity remains.

### B — Embedding Cardinality: Keep Single Embedding Per Member (overwrite) vs. Multiple Embeddings Per Member

1. **Single embedding per Member (keep invariant, last photo wins)** — Existing member looked up by `FullName` -> `Member.Enroll`-like update path mutates/replaces stored embedding with `embeddings[0]` of new photo (or overwrites via `UpsertAsync` with same `Id`). No schema change.
   - Pros: Zero migration — `Member`, `MemberVectorRecord` (`Id == MemberId`), and `SaveAsync`/`UpsertAsync` stay unchanged; atomic gate logic already correct; stays well under 400-line budget; `FindNearest` unchanged.
   - Cons: Loses historical pose diversity — ArcFace accuracy benefits from multiple reference vectors per identity; user intent "adicion de esa photo para ese miembro" reads as *accumulating* photos, not replacing; last-write-wins surprises users who expected additive enrollment.
   - Effort: Low.

2. **Multiple embeddings per Member (additive rows)** — One `Member` concept maps to N `MemberVectorRecord` rows keyed as `{MemberId}#{ordinal}` or a new `MemberPhotoRecord { PhotoId, MemberId, Embedding }` collection; `FindNearestAsync` searches top-K and deduplicates to nearest per `MemberId`; name lookup returns the logical Member with all embeddings.
   - Pros: Faithful to request (every photo is kept), materially improves recognition (diverse angles/lighting); cleanly supports "add photo for existing family member"; future-proofs directory.
   - Cons: Breaks current `Id == MemberId` uniqueness contract (every existing test assumes one row per member); requires mapper rewrite, new deduplication logic, migration for existing DB files; search cost scales with total photos (top-K vs top-1); still needs `VectorStoreVector` dimension/metrics unchanged.
   - Effort: High — schema, mapping, search, tests, migration; likely pushes one PR past 400 lines.

3. **Hybrid: store member row + separate embedding rows (normalized)** — Keep `MemberVectorRecord` for the primary embedding and add a second collection `MemberEmbeddingRecord { Id=Guid, MemberId, Embedding }`; directory query searches embedding collection, joins to member.
   - Pros: Preserves original member row without mutation; isolates concerns; supports eventual SQL-backed metadata beyond vectors.
   - Cons: Most complex; two collections to ensure-collection-exists, two Upserts per enrollment; double implementation effort for same user-visible behavior as B2; strongest budget risk.
   - Effort: High.

### C — Existing-Member Lookup: Exact FullName vs. Vector-Nearest Suggestion

1. **Lookup by normalized `FullName` (case-insensitive trimmed equality)** — New `IMemberVectorRepository.FindByFullNameAsync(string)` (or `IMemberReader` by name) scans the `members` collection filtering `FullName == normalized`. Prompt flow: user types name -> exact match => existing Member -> add photo path; no match => new Member.
   - Pros: Deterministic, matches user's "se nos va a solicitar el nombre y si ya es un miembro existente" specification directly; simple string compare; no threshold ambiguity.
   - Cons: Fragile to typos/diacritics (`Jose` vs `José`, `Ana Maria` vs `Ana-MarIa`), no help for duplicate accidental variants; requires linear scan today (no `FullName` index in `SqliteVec`); `FullName` uniqueness not enforced anywhere — opening duplicates.
   - Effort: Low (add one repository method, normalize helper).

2. **Vector-based duplicate suggestion** — Extract embedding first, search `FindNearestAsync`; if `distance <= AllowedMaxDistance` (0.35 today), propose the matched member ("Is this X? [Y/n]") before falling back to name.
   - Pros: Catches near-duplicates despite name typos; reuses existing deterministic vector comparison; aligns with Monitoring's threshold semantics.
   - Cons: Adds latency (must run detection/extraction before prompting); false positives near threshold; UX becomes two-step (name + confirmation) — more branching; threshold coupling between Enrollment and Monitoring (`AllowedMaxDistance` lives under Monitoring options, not Enrollment).
   - Effort: Medium — extractor must run before repository branch, handler needs policy or threshold injection.

3. **Composite: name-primary, vector-confirmation** — Exact `FullName` lookup is authoritative; if no exact match but nearest distance is very close, warn "A very similar member X already exists — create new anyway?" Requires both code paths.
   - Pros: Best accuracy/UX, reduces duplicates without surprising the user.
   - Cons: Most logic; configuration of warning threshold; hardest to keep under line budget.
   - Effort: Medium-High.

### D — Console Interaction: Full Interactive Loop + Blocking Watch Sub-Mode vs. Alternatives

1. **Interactive menu loop + `watch` sub-mode (recommended hybrid)** — `Program.cs` becomes `while(true) { printMenu(); choice = ReadLine(); switch }` with entries `1: spike`, `2: register (manual: prompt FullName + photo paths)`, `3: watch (start watcher, per-file prompt name, loop until user types 'q' or Ctrl+C)`, `0: exit`. `watch` reuses `FileSystemWatcher+Channel` (or polling) internally but prompts via `Console` synchronously.
   - Pros: Directly satisfies "permita multiples iteraciones" and "por cada photo agregada se nos va a solicitar el nombre"; keeps spike/register/watch in one process without spawning `Monitoring.Host`; familiar console UX; `watch` can be cancelled cleanly (CancellationTokenSource linked to 'q').
   - Cons: Mixed concerns (menu IO + watcher) in one project; console `ReadLine` lacks testability — needs extraction of `IConsole`/`IEnrollmentWatcherService` for coverage; blocking prompt + watcher channel sizing must be reasoned about together.
   - Effort: Medium.

2. **Watch-only daemon (like `MonitoringHostedService` but for enrollment)** — `Enrollment.Console` becomes a `Host.CreateApplicationBuilder` hosted service that watches forever without any menu.
   - Pros: Mirrors `MonitoringHostedService` structurally; natural for non-interactive enrollment in production.
   - Cons: Contradicts "mejorar el menu" requirement (no menu); cannot interactively prompt for a name per photo without breaking the hosted-service model; forces name to be encoded in filename or subfolder, which the request does not state.
   - Effort: Low structurally, but mismatched to spec.

3. **Loop without watch (manual-only multi-iteration)** — Keep only `register` but allow repeating it (`register` prompts for name and photos in a loop, no file-system watch at all).
   - Pros: Simplest — no `FileSystemWatcher` questions, no processed/quarantine archival, stays under budget.
   - Cons: Ignores "monitorizar una carpeta" requirement — half the request is dropped; users must type paths by hand even when photos are batch-dropped via camera sync.
   - Effort: Low, but incomplete.

## Recommendation

**Narrow scope to a single-PRsized slice that respects the 400-line review budget and the explicit separation from `photos/watch`:**

- **A -> Option A1 (FileSystemWatcher reuse):** copy-adapt `FileSystemWatchIngestionSource`/`FileSystemImageArchiver` into an Enrollment-scoped watcher (`EnrollmentWatchIngestionSource` + `EnrollmentWatchOptions`) configured to `photos/enroll` (new folder, NOT `photos/watch` nor `photos/inbox`). Carry over the exact stability-retry, bounded `Channel`, and `processed`/`quarantine` archival (both under `photos/enroll/`) — reviewer familiarity outweighs the novelty of polling. Use a simpler channel size (e.g., 64) and surface channel-full warning at prompt time.

- **B -> Option B1 (single embedding, overwrite) for this change:** keep `Member`'s "exactly one `FaceEmbedding`" invariant and `MemberVectorRecord`'s single-row-per-member contract. Adding a photo for an existing member **replaces** the stored embedding (Upsert with same `Id`). Document explicitly in proposal/spec delta that additive multi-embedding is a *follow-up* change — this is the single most important scoping decision to protect the budget. Handler logic: `FindByFullNameAsync(normalized)` -> if found, extract new embedding then `SaveAsync(updatedMember with same MemberId)` (Upsert); if not found, `Member.Enroll(NewId, ...)` as today.

- **C -> Option C1 (normalized FullName lookup) for this change:** introduce `IMemberVectorRepository.FindByFullNameAsync(string) -> Member?` (case-insensitive trimmed equality, trim + `StringComparison.OrdinalIgnoreCase`). This is sufficient and deterministic. Vector-warn (C2/C3) can be a follow-up after multi-embedding lands — do not couple enrollment to `AccessDecisionOptions` thresholds yet (that leaks Monitoring's domain into Enrollment).

- **D -> Option D1 (interactive loop + blocking watch):** refactor `Program.cs` to a `while(true)` menu (`spike`, `register` (manual), `watch` (enrollment), `exit`). Extract console IO behind `IConsole` or at least a `EnrollmentConsoleMenu` class so `Program.Main` stays thin per existing comment ("Main never holds business logic"). `watch` sub-mode: ensure folder exists, start watcher, `await foreach` stable paths, `Console.Write($"Member FullName for '{fileName}': ")`, read name, validate non-empty, run `RegisterMemberCommandHandler` with `File.OpenRead(path)`, log outcome, delegate archival to the Enrollment archiver (processed on success, quarantine on failure). Graceful exit on `q`/`exit` input or `Ctrl+C` (`Console.CancelKeyPress`).

**Folder naming:** `photos/enroll` is preferred over `photos/inbox` or `photos/drop` because (a) `watch` is already taken, (b) `enroll` is self-documenting and matches `member-enrollment`, (c) it avoids the ambiguity noted in the task ("likely photos/inbox or photos/enroll or photos/drop"). Create `photos/enroll/.gitignore` mirroring `photos/watch/.gitignore` but with `*` + `!processed/` + `!quarantine/` + `!.gitignore`.

**Why this minimizes risk:** no schema migration, no `Id` keying change, no deduplication-search change, no cross-module threshold import. One repository method added, two watcher/archiver classes adapted from proven code, one console loop — comfortably under 400 lines; a multi-embedding follow-up becomes its own change after the watch+menu slice is archived.

## Risks

- **Single-embedding overwrite surprises users:** additive intent ("adicion de esa photo") loses history; user may re-enroll a family member expecting to improve recognition and instead discard the better pose. Mitigation: log `Replaced embedding for {FullName} ({MemberId})` explicitly and call out follow-up multi-embedding in proposal.
- **Prompt blocks watcher consumer:** while user is typing a name, the watcher `Channel` (DropOldest) may evict paths; burst of 10 photos + slow typing can silently drop files. Mitigation: use small bounded channel but pause watcher (`EnableRaisingEvents=false`) while prompt blocks, or buffer to an unbounded queue for the watch sub-mode only.
- **File locking on Windows:** exclusive `FileShare.None` stability check correctly prevents partial writes, but some editors/cameras hold locks longer than 5x200ms = 1s; enrollment watch retries same config as Monitoring — may need slightly longer retry (e.g., 8 attempts) for large photos.
- **`FullName` duplication without uniqueness constraint:** `SqliteVec` has no unique constraint on `FullName`; case-only differences (`ana` vs `Ana`) create distinct members unless normalization is strictly applied at every write and read path. Mitigation: normalization helper used by both `SaveAsync` and `FindByFullNameAsync`, and a spec scenario asserting case-insensitive idempotence.
- **Absolute `ConnectionString` (`C:/Dev/CamGuard/...`):** non-portable, breaks on any other checkout path; shared DB between Enrollment and Monitoring means enrollment Upserts are immediately visible to Monitoring — correct but couples dev machines. Mitigation: move to relative `Data Source=camguard-enrollment.db` under `AppContext.BaseDirectory` or respect `appsettings.json` `WatchFolder.Path` pattern, but out-of-scope for this slice — document as Open Question.
- **FaceAiSharp singleton detector concurrency:** `IFaceDetectorWithLandmarks` is singleton (expensive ONNX model) — if `Monitoring.Host` and `Enrollment.Console` ever run in the same process (not today, but modular monolith allows `AddEnrollment` + `AddMonitoring` composition), concurrent `DetectFaces` calls need the same serialization guard as `FaceAiSharpPersonDetector`'s channel. Current proposal keeps processes separate, so safe; future co-hosting needs the guard.
- **`photos/watch` vs. `photos/enroll` confusion:** reviewer or operator may drop enrollment photos into `photos/watch`, causing Monitoring to process them as access attempts (identity verification + notification). Mitigation: distinct folder names, distinct log scopes (`CamGuard.Enrollment.Watch` vs `CamGuard.Monitoring`), and documentation in `photos/README.md` or archiver logs.
- **Testability of console loop:** `Console.ReadLine`/`Write` are static — unit tests for menu flow need `IConsole` abstraction or `StringReader`/`StringWriter` seams; skipping that makes `Program.cs` untestable. Recommend extracting `EnrollmentConsoleRunner` / `EnrollmentWatchService` as testable units.
- **Scope creep to multi-embedding:** temptation to "fix" B1 into B2 mid-PR easily pushes diff past 400 lines. Gate via `sdd-tasks` budget forecast (`Decision needed before apply`, `Chained PRs recommended`, `400-line budget risk`) — pin B1 and defer B2 explicitly in tasks.

## Ready for Proposal

Yes. Scope is narrow and decisions are explicit enough to produce delta specs: add `EnrollmentWatchFolder` configuration capability, extend `member-enrollment` with re-enrollment (overwrite) scenario(s) (`FullName` case-insensitive lookup, single embedding replacement semantics), and a console interaction capability (interactive menu with multiple iterations and `watch` sub-mode prompting per new file, with `processed/`/`quarantine/` archival separate from `photos/watch`). Multi-embedding, vector-based duplicate warning, and cross-module threshold coupling remain explicit follow-ups — not part of this proposal.

## Key Learnings

1. Enrollment Member currently holds exactly one FaceEmbedding and the vector record key is the MemberId string so multi photo persistence would require new keying.
2. Monitoring photos watch at C:/Dev/CamGuard/photos/watch must remain separate from the new enrollment watch folder proposed as photos/enroll.
3. FileSystemWatcher debounce with FileShare None stability retry is already proven in Monitoring and should be reused for enrollment watch.
4. Normalized FullName lookup is the minimal deterministic way to detect existing family members without coupling to Monitoring thresholds.
5. Interactive console menu must pause the watcher channel while prompting to avoid DropOldest loss during slow name entry.

