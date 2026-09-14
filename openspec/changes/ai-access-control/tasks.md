# Tasks: AI-Managed Access Control — Phase 1 (Scaffolding + Enrollment)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1200–1500 (greenfield: scaffolding + 6 projects + tests) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (scaffold+spike) → PR 2 (Domain+Application) → PR 3 (Infra+PublicApi+DI+Observability) |
| Delivery strategy | auto-chain |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: pending
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

- [ ] 3.1 Create `MemberId.cs` — sealed record, rejects `Guid.Empty`
- [ ] 3.2 Create `FaceEmbedding.cs` — `FromVector(float[])` validates dimension (per 2.6) + non-zero vector
- [ ] 3.3 Create `Member.cs` : `EntityBase<MemberId>` — `Enroll(...)` returns `Result<Member, MemberError>`, raises `MemberEnrolled`
- [ ] 3.4 Create `MemberEnrolled.cs` domain event
- [ ] 3.5 Create `IMemberVectorRepository.cs` port — `SaveAsync`, `FindNearestAsync`

## Phase 4: Infrastructure Ports/Adapters (after Phase 2 gate + Phase 3)

- [ ] 4.1 Create `src/Enrollment.Application/IFaceEmbeddingExtractor.cs` + `FaceExtractionError` enum
- [ ] 4.2 Add FaceAiSharp refs to `src/Enrollment.Infrastructure/Enrollment.Infrastructure.csproj`
- [ ] 4.3 Create `FaceAiSharpEmbeddingExtractor.cs` — SCRFD gate (reject 0/2+ faces) + ArcFace embedding
- [ ] 4.4 Add CommunityToolkit.VectorData.SqliteVec + Microsoft.Data.Sqlite refs to Infrastructure csproj
- [ ] 4.5 Create `SqliteVecMemberRepository.cs` — vector-store record + Infrastructure-only mapper from `Member`
- [ ] 4.6 Create `src/Common/VectorStoreOptions.cs`

## Phase 5: Application (`src/Enrollment.Application`)

- [ ] 5.1 Create `RegisterMemberCommand.cs` + `RegisterMemberCommandHandler.cs` (extract → `Member.Enroll` → save)
- [ ] 5.2 Create `QueryByEmbeddingQuery.cs` + `QueryByEmbeddingQueryHandler.cs` (`FromVector` validate → `FindNearestAsync`)

## Phase 6: PublicApi (`src/Enrollment.PublicApi`)

- [ ] 6.1 Create `IMemberDirectoryReader.cs` + `MemberMatch` DTO
- [ ] 6.2 Create `MemberDirectoryReader.cs` — delegates to query handler, maps no-match

## Phase 7: Composition Root / DI Wiring

- [ ] 7.1 Create `AddEnrollment(...)` extension in `Enrollment.Infrastructure` — `TryAddScoped` all ports/handlers, bind `VectorStoreOptions`
- [ ] 7.2 Create OpenTelemetry `ActivitySource`/`Meter` registration extension in `src/Common`

## Phase 8: Observability

- [ ] 8.1 Add `CamGuard.Enrollment` `ActivitySource` + `Enrollment.ExtractEmbedding` span (tags: faces.detected.count, outcome) in the extractor
- [ ] 8.2 Add `Enrollment.VectorStore.Save`/`Query` spans (tags: match.score, latency) in `SqliteVecMemberRepository.cs`

## Phase 9: Tests (AutoMocker, per spec scenarios)

- [ ] 9.1 `tests/Enrollment.UnitTests/MemberTests.cs` — `Member.Enroll` invariants
- [ ] 9.2 `tests/Enrollment.UnitTests/FaceEmbeddingTests.cs` — dimension/zero-vector validation
- [ ] 9.3 `tests/Enrollment.UnitTests/RegisterMemberCommandHandlerTests.cs` — covers member-enrollment scenarios (zero/multiple-face rejection, atomic persistence, happy path)
- [ ] 9.4 `tests/Enrollment.UnitTests/QueryByEmbeddingQueryHandlerTests.cs` — covers member-directory-query scenarios (best match, determinism, no-match, round-trip)
- [ ] 9.5 `tests/Enrollment.UnitTests/MemberDirectoryReaderTests.cs` — Public API Boundary scenario
- [ ] 9.6 `tests/Enrollment.UnitTests/SqliteVecMemberRepositoryTests.cs` — round-trip against temp SQLite file, not mocked
