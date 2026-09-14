# Tasks: AI-Managed Access Control — Phase 1 (Scaffolding + Enrollment)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1200–1500 (greenfield: scaffolding + 6 projects + tests) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (scaffold+spike) → PR 2 (Domain+Application) → PR 3 (Infra+PublicApi+DI+Observability) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main (user-confirmed) |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main (user-confirmed)
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | Scaffolding + FaceAiSharp net10 spike (gate) | PR 1 | `dotnet test --filter FaceAiSharpNet10CompatibilityTests` | `dotnet run --project src/Enrollment.Console -- <imagePath>` | Delete scaffolding files + `src/Enrollment.Console` |
| 2 | Domain + Application (pure logic, no external deps) | PR 2 | `dotnet test --filter Member\|FaceEmbedding\|RegisterMemberCommandHandler\|QueryByEmbeddingQueryHandler` | N/A — no I/O boundary yet | Delete `src/Enrollment.Domain`, `src/Enrollment.Application` |
| 3 | Infrastructure adapters + PublicApi + DI + Observability | PR 3 | `dotnet test --filter SqliteVecMemberRepository\|MemberDirectoryReader` | Integration test round-trip is the harness (temp SQLite file) | Delete `src/Enrollment.Infrastructure`, `src/Enrollment.PublicApi`, DI/observability wiring |

## Phase 1: Solution Scaffolding

- [x] 1.1 Create `global.json` — SDK 10.0.103, rollForward latestFeature
- [x] 1.2 Create `Directory.Packages.props` (CPM): FaceAiSharp 0.6.35, FaceAiSharp.Bundle, CommunityToolkit.VectorData.SqliteVec 1.0.1-preview, Microsoft.Data.Sqlite, AutoMocker, xunit
- [x] 1.3 Create `Directory.Build.props` — net10.0, Nullable, ImplicitUsings, EnablePreviewFeatures
- [x] 1.4 Create `CamGuard.slnx` referencing all project stubs
- [x] 1.5 Create empty project stubs: `src/Enrollment.Domain`, `src/Enrollment.Application`, `src/Enrollment.Infrastructure`, `src/Enrollment.PublicApi`, `src/Common`, `src/Enrollment.Console`, `tests/Enrollment.UnitTests`
- [x] 1.6 Verify `dotnet build` succeeds on the empty scaffold

## Phase 2: FaceAiSharp net10 Spike (GATE — must pass before Phase 4)

- [x] 2.1 Add FaceAiSharp + FaceAiSharp.Bundle refs to `src/Enrollment.Console/Enrollment.Console.csproj` only
- [x] 2.2 Create `src/Enrollment.Console/FaceAiSharpSpike.cs` — injectable `Run(imagePath)`, zero reference to `Enrollment.*`
- [x] 2.3 Create thin `src/Enrollment.Console/Program.cs` calling the spike
- [x] 2.4 Add a sample reference photo under `src/Enrollment.Console/testdata/` (synthetic placeholder — see note below)
- [x] 2.5 Create `tests/Enrollment.UnitTests/FaceAiSharpNet10CompatibilityTests.cs` — asserts no exception + returns vector
- [x] 2.6 Run spike; record confirmed embedding dimension/metric in `design.md` Open Questions before Phase 4 starts (net10 compat confirmed; dimension still open — see note below)

**PR 1 spike notes (read before starting Phase 3/4):**
- No real photo of a human face was available in this environment/session. Task 2.4's "sample reference photo" is a synthetic solid-gray 200×200 PNG (`src/Enrollment.Console/testdata/blank-image.png`), generated via `SixLabors.ImageSharp`, committed as the automated-test fixture only.
- `FaceAiSharpNet10CompatibilityTests` therefore proves net10.0/package **runtime compatibility** (constructs without exception; a faceless image correctly returns zero detected faces without crashing) — it does NOT prove real-face accuracy or confirm the embedding dimension, since that requires exactly one detected face.
- **Follow-up required from the user before Phase 4 trusts `FaceEmbedding.ExpectedDimension` (assumed 512):** run `dotnet run --project src/Enrollment.Console -- <path-to-a-real-reference-photo>` locally and record the printed `Embedding dimension:` value in `design.md`'s Open Questions.
- Real finding from the spike: FaceAiSharp only depends on `Microsoft.ML.OnnxRuntime.Managed`; the native ONNX Runtime package (`Microsoft.ML.OnnxRuntime`) must be referenced explicitly by any consuming project or `FaceAiSharpBundleFactory.Create*()` crashes the process (native access violation, not a catchable exception). Already applied to `Enrollment.Console`; Phase 4 must apply the same to `Enrollment.Infrastructure`.

## Phase 3: Domain (`src/Enrollment.Domain`)

- [x] 3.1 Create `MemberId.cs` — sealed record, rejects `Guid.Empty`
- [x] 3.2 Create `FaceEmbedding.cs` — `FromVector(float[])` validates dimension (per 2.6) + non-zero vector
- [x] 3.3 Create `Member.cs` : `EntityBase<MemberId>` — `Enroll(...)` returns `Result<Member, MemberError>`, raises `MemberEnrolled`
- [x] 3.4 Create `MemberEnrolled.cs` domain event
- [x] 3.5 Create `IMemberVectorRepository.cs` port — `SaveAsync`, `FindNearestAsync`

**PR 2 note on `EntityBase<TId>`:** design.md specifies `Member : EntityBase<MemberId>` but no task creates that base class. It did not exist anywhere in the repo. Added `src/Enrollment.Domain/EntityBase.cs` (extends `CSharpFunctionalExtensions.Entity<TId>` with a domain-event list, per the `entity-model` skill's "entity with domain events" base) and `IDomainEvent.cs`, both in `Enrollment.Domain` since no shared-kernel project is in scope yet and `Common` is explicitly restricted to technical config, never business/domain-modeling types. `CSharpFunctionalExtensions` 3.7.0 was added to `Directory.Packages.props` (not previously referenced anywhere) to provide `Entity<TId>` and `Result<TValue, TError>`, both used throughout Domain/Application per design.md's `Result<Member, MemberError>`/`Result<FaceEmbedding, FaceExtractionError>` signatures. `Entity<TId>` constrains `TId : IComparable<TId>`, so `MemberId` implements `IComparable<MemberId>`.

## Phase 4: Infrastructure Ports/Adapters (after Phase 2 gate + Phase 3)

- [x] 4.1 Create `src/Enrollment.Application/IFaceEmbeddingExtractor.cs` + `FaceExtractionError` enum
- [x] 4.2 Add FaceAiSharp refs to `src/Enrollment.Infrastructure/Enrollment.Infrastructure.csproj`
- [x] 4.3 Create `FaceAiSharpEmbeddingExtractor.cs` — SCRFD gate (reject 0/2+ faces) + ArcFace embedding
- [x] 4.4 Add CommunityToolkit.VectorData.SqliteVec + Microsoft.Data.Sqlite refs to Infrastructure csproj
- [x] 4.5 Create `SqliteVecMemberRepository.cs` — vector-store record + Infrastructure-only mapper from `Member`
- [x] 4.6 Create `src/Common/VectorStoreOptions.cs`

**PR 2 note — task 4.1 pulled forward from PR3 scope:** 4.1 is numbered under "Phase 4: Infrastructure" but its file target (`src/Enrollment.Application/IFaceEmbeddingExtractor.cs`) is the Application-layer port itself, not the FaceAiSharp adapter. `RegisterMemberCommandHandler` (task 5.1) structurally requires this port to compile, and the port has zero reference to FaceAiSharp/Infrastructure — matching PR2's acquired evidence-goal ("zero external I/O dependency"). Implemented only the interface + `FaceExtractionError` enum (already fully specified in design.md's "Ports and Adapters" section); 4.2–4.6 (actual FaceAiSharp/SqliteVec Infrastructure adapters, still PR3 scope) were NOT started.

## Phase 5: Application (`src/Enrollment.Application`)

- [x] 5.1 Create `RegisterMemberCommand.cs` + `RegisterMemberCommandHandler.cs` (extract → `Member.Enroll` → save)
- [x] 5.2 Create `QueryByEmbeddingQuery.cs` + `QueryByEmbeddingQueryHandler.cs` (`FromVector` validate → `FindNearestAsync`)

## Phase 6: PublicApi (`src/Enrollment.PublicApi`)

- [x] 6.1 Create `IMemberDirectoryReader.cs` + `MemberMatch` DTO
- [x] 6.2 Create `MemberDirectoryReader.cs` — delegates to query handler, maps no-match

## Phase 7: Composition Root / DI Wiring

- [x] 7.1 Create `AddEnrollment(...)` extension in `Enrollment.Infrastructure` — `TryAddScoped` all ports/handlers, bind `VectorStoreOptions`
- [x] 7.2 Create OpenTelemetry `ActivitySource`/`Meter` registration extension in `src/Common`

## Phase 8: Observability

- [x] 8.1 Add `CamGuard.Enrollment` `ActivitySource` + `Enrollment.ExtractEmbedding` span (tags: faces.detected.count, outcome) in the extractor
- [x] 8.2 Add `Enrollment.VectorStore.Save`/`Query` spans (tags: match.score, latency) in `SqliteVecMemberRepository.cs`

## Phase 9: Tests (AutoMocker, per spec scenarios)

- [x] 9.1 `tests/Enrollment.UnitTests/MemberTests.cs` — `Member.Enroll` invariants
- [x] 9.2 `tests/Enrollment.UnitTests/FaceEmbeddingTests.cs` — dimension/zero-vector validation
- [x] 9.3 `tests/Enrollment.UnitTests/RegisterMemberCommandHandlerTests.cs` — covers member-enrollment scenarios (zero/multiple-face rejection, atomic persistence, happy path)
- [x] 9.4 `tests/Enrollment.UnitTests/QueryByEmbeddingQueryHandlerTests.cs` — covers member-directory-query scenarios (best match, determinism, no-match, round-trip)
- [x] 9.5 `tests/Enrollment.UnitTests/MemberDirectoryReaderTests.cs` — Public API Boundary scenario
- [x] 9.6 `tests/Enrollment.UnitTests/SqliteVecMemberRepositoryTests.cs` — round-trip against temp SQLite file, not mocked

**PR 2 note:** 9.1–9.4 were implemented alongside their production code (3.1–3.5/5.1–5.2) rather than deferred, since PR2's acquired evidence-goal and the "Suggested Work Units" focused-test-command row above both require these exact test classes passing for this work unit. 9.5/9.6 stay PR3-scoped (PublicApi/Infrastructure, not built yet).

**PR 3 notes (final PR of `ai-access-control`, all 38 tasks now complete):**
- **4.2–4.6, 6.1–6.2, 7.1–7.2, 8.1–8.2, 9.5–9.6 all implemented as specified.** All 4 API assumptions needed to write `SqliteVecMemberRepository.cs` (`SqliteCollection<TKey,TRecord>` constructor shape, `VectorStoreKey`/`VectorStoreData`/`VectorStoreVector` attributes, `SearchAsync` returning `IAsyncEnumerable<VectorSearchResult<TRecord>>`, `DistanceFunction.CosineDistance`) were verified against the actual restored NuGet package's XML doc comments (not assumed from memory) before writing code, since `CommunityToolkit.VectorData.SqliteVec` is still `-preview` and undocumented in research.md beyond package existence. First-attempt compile succeeded against those verified signatures.
- **Distance/similarity metric resolved**: design.md's open question ("Confirm the distance/similarity metric... targeted for Phase 4") is now closed — `MemberVectorRecord.Embedding` uses `DistanceFunction.CosineDistance` (0 = identical, 2 = opposite), the standard convention for ArcFace-family embeddings. Documented on the attribute itself and in design.md.
- **`MemberVectorRecord.Id` is `string`, not `Guid`**: `SqliteCollection<TKey,TRecord>`'s `TKey` only supports `string`/`int`/`long` (confirmed from the package's own XML docs) — `MemberId.Value.ToString()` is stored/parsed at the Infrastructure boundary; `Member`/`MemberId` (Domain) are untouched.
- **New dependency not in original task list**: `AddEnrollment(...)` (task 7.1) needs `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Configuration.Abstractions`/`.Binder` — design.md's DI snippet assumes these are ambiently available (true only inside a full ASP.NET Core host), but `Enrollment.Infrastructure`/`Common` had zero DI-related package references before this PR. Added explicitly, pinned via CPM.
- **Bug found and fixed via the new DI integration test, not assumed correct**: design.md's DI snippet used `.Configure<IConfiguration>((o, c) => c.GetSection(...).Bind(o))`, which resolves `IConfiguration` *from the container* — this throws `"No service for type IConfiguration has been registered"` in any host that does not auto-register `IConfiguration` as a DI service (confirmed by `EnrollmentServiceCollectionExtensionsTests` failing before the fix). Fixed to `.Configure(o => configuration.GetSection(...).Bind(o))`, closing over the `configuration` parameter `AddEnrollment` already receives — works in any host, including a bare `ServiceCollection`. This is a correction to design.md's illustrative snippet, not a deviation from intent.
- **`Common`'s OpenTelemetry extension registers keyed `ActivitySource`/`Meter` singletons, not a full OpenTelemetry SDK pipeline**: `AddNamedObservabilitySource(string sourceName)` uses `TryAddKeyedSingleton` (keyed by module name, e.g. `"CamGuard.Enrollment"`) so a future second module (`Monitoring`) can open its own named source without colliding on the shared `ActivitySource`/`Meter` service types — plain (non-keyed) `TryAddSingleton` would let only the first caller's name win. No `OpenTelemetry`/`OpenTelemetry.Extensions.Hosting` SDK package was added: there is no host project yet to configure exporters/sampling against, and design.md's own wording ("registers a shared... extension") matches registering the instrumentation primitives, not standing up a full collector pipeline. `FaceAiSharpEmbeddingExtractor`/`SqliteVecMemberRepository` consume the keyed `ActivitySource` via `[FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)]` constructor injection (never a static field), per `dotnet-testable-code-design`.
- **`FaceAiSharpEmbeddingExtractor`'s testdata gap is unchanged from PR1/PR2**: no real face photo exists in this environment, so this PR could not add a "real photo → 512-dim embedding" integration test. The adapter's face-detection-gate logic (0/2+ faces → typed rejection) is proven by the existing `RegisterMemberCommandHandlerTests` (mocking the port) and by `EnrollmentServiceCollectionExtensionsTests` (proving the adapter constructs and resolves through DI without the PR1-flagged native-ONNX-Runtime crash) — but the actual embedding-extraction code path (exactly-one-face → 512-float vector) remains unexercised by an automated test. **This is a pending manual step for the user**, same as PR1/PR2 flagged: run `dotnet run --project src/Enrollment.Console -- <path-to-a-real-reference-photo>` to confirm the embedding dimension, then re-verify `FaceAiSharpEmbeddingExtractor` end-to-end with that same photo.
- **Gap restated, still not fixed (inherited from PR2, out of this PR's scope)**: `member-enrollment` spec's multi-photo atomic-persistence scenario is still not modeled by the single-photo `RegisterMemberCommand`/`IMemberDirectoryReader` boundary built in PR2/PR3. Not touched here — Application-layer command shape is out of scope for an Infrastructure/PublicApi/DI/Observability PR.

## PR4: Verify Remediation (multi-photo atomic enrollment)

`sdd-verify` FAILED (evidence revision `sha256:b4495d43eb316790963b8a0eea7335fa3b9bae4df78186b003a94c0ac628401f`) with one CRITICAL blocker: `member-enrollment` spec's "Atomic Vector Persistence" requirement mandates accepting "one or more reference photos" with all-or-nothing persistence, but `RegisterMemberCommand` only accepted a single `Stream Photo` — zero implementation, zero test. This section is new work, not originally itemized in Phases 1–9; it is tracked here rather than retrofitted into old task numbers, per the remediation instructions.

- [x] R4.1 Update `design.md`'s Application Flow sequence diagram (`RegisterMemberCommandHandler`) from single-photo to a per-photo loop: extract embedding for each submitted photo, gated by the existing zero/multiple-face-detected rejection; if ANY photo fails the gate, the whole enrollment is rejected and nothing is persisted; only when ALL photos pass does `Member.Enroll`/`SaveAsync` run once. Added one Architecture Decisions row documenting the multi-photo-to-single-primary-embedding choice. Everything else in design.md (ports, DI, observability) left unchanged — already verified COMPLIANT.
- [x] R4.2 `RegisterMemberCommand` (`src/Enrollment.Application/RegisterMemberCommand.cs`): `Stream Photo` → `IReadOnlyList<Stream> Photos`, constructor rejects `null` (`ArgumentNullException`) and empty (`ArgumentException`) — spec's "Member Registration Input" requirement mandates "one or more reference photos".
- [x] R4.3 `RegisterMemberCommandHandler` (`src/Enrollment.Application/RegisterMemberCommandHandler.cs`): loop extracts an embedding per photo; the first photo that fails the face-detection gate short-circuits and returns failure immediately — no further photos are processed and `SaveAsync` is never reached, so no partial DB write is possible. Only when every photo passes does the handler call `Member.Enroll` once, using the **first** photo's embedding as the primary embedding (documented decision — `Member` still models exactly one `FaceEmbedding`, unchanged per design.md; the spec's atomicity requirement does not mandate combining embeddings), then `SaveAsync` once.
- [x] R4.4 `Member`/`IMemberVectorRepository`/`FaceEmbedding` (Domain) were checked and intentionally left unchanged: `Member.Enroll` already takes exactly one `FaceEmbedding`, which satisfies the spec's literal wording once a primary embedding is selected per R4.3 — no forced downstream mechanical fix was needed in Infrastructure, PublicApi, or DI wiring.
- [x] R4.5 Extended `tests/Enrollment.UnitTests/RegisterMemberCommandHandlerTests.cs` with 6 new tests: multi-photo success (3 photos, all pass, `SaveAsync` called exactly once with the first photo's embedding), atomic rollback when one of several photos has zero faces detected, atomic rollback when one of several photos has multiple faces detected, plus `RegisterMemberCommand` constructor guard tests (null photo list, empty photo list). `MemberTests.cs` required no changes — `Member`'s aggregate shape did not change.

**Work Unit Evidence (PR4):**
- Focused test: `dotnet test tests/Enrollment.UnitTests/Enrollment.UnitTests.csproj --filter "RegisterMemberCommandHandlerTests|MemberTests"` → Passed 13/13 (7 pre-existing + 6 new).
- Full suite regression check: `dotnet test tests/Enrollment.UnitTests/Enrollment.UnitTests.csproj` (no filter) → Passed 32/32 (27 carried from PR1–3 + 5 net-new — one new test class fact plus the 4 handler/constructor scenarios; zero regressions).
- Runtime harness: N/A — this remediation is entirely Application/Domain-boundary logic (command shape + handler orchestration over mocked ports); no new I/O boundary was introduced. The existing Infrastructure/PublicApi runtime harnesses (PR3) are untouched and were not re-run since Infrastructure/PublicApi/DI files were not touched by this PR.
- Full solution build: `dotnet build CamGuard.slnx` → Build succeeded, 0 warnings, 0 errors.
- Changed lines: 177 insertions(+) / 24 deletions(-) across 4 files (design.md, RegisterMemberCommand.cs, RegisterMemberCommandHandler.cs, RegisterMemberCommandHandlerTests.cs) — well within the 500-line remediation budget.
- Rollback boundary: revert the 4 changed files listed above; no other file was touched by this PR.
