# Proposal: AI-Managed Physical Access Control — Phase 1 (Scaffolding + Enrollment)

## Intent

CamGuard needs a first working vertical slice of an AI-managed access-control system: register authorized members from reference photos into a local face-embedding vector store, so a later Monitoring pipeline can identify people against it. This is the first change in a greenfield repo — no code exists yet — so it must also establish project scaffolding and precedent-setting architecture decisions (module boundaries, embedding strategy, vector store) before any pipeline logic is built.

**Phasing decision**: this proposal is scoped to **Phase 1 only** (scaffolding + `Enrollment` module). The Monitoring pipeline (folder watch, detect/identify/decide/notify workflow) is deferred to a **separate follow-up change** (`monitoring-pipeline`), proposed after this one archives. Rationale: Enrollment and Monitoring are independently shippable and independently risky (embedding/vector-store correctness vs. `FileSystemWatcher`/workflow reliability); splitting keeps each change's spec/task/review surface small (per the 400-line review budget) and lets the FaceAiSharp/net10 and SQLite-preview risks get validated before the larger, higher-risk Monitoring workflow is designed.

## Scope

### In Scope
- Solution scaffolding: `.slnx`, `Directory.Packages.props` (CPM), `Directory.Build.props` (net10.0, Nullable, EnablePreviewFeatures), `global.json` (SDK 10.0.103), mirroring the reference course repo.
- `Enrollment` module: register a member (identity + reference photo) → extract face embedding → persist to vector store.
- `IFaceEmbeddingExtractor` port wrapping FaceAiSharp (ArcFace-family + SCRFD detector).
- SQLite-backed vector store via `CommunityToolkit.VectorData.SqliteVec` (1.0.1-preview).
- `IMemberDirectoryReader` public-API port: deterministic similarity query returning best-match member + score, for future Monitoring consumption.
- A local spike proving FaceAiSharp runs on net10.0.

### Out of Scope
- `Monitoring` module: folder watcher, detect/identify/decide/notify workflow, `Microsoft.Agents.AI.Workflows` pipeline, `IAccessDecisionPolicy`, `INotificationService`/`LoggingNotificationService` — all deferred to `monitoring-pipeline`.
- Any real notification channel (email/SMS).
- Azure AI Vision Face API integration (rejected — Limited Access, not obtainable for this project).
- UI/API surface for member management (console/test harness only for now).

## Capabilities

### New Capabilities
- `member-enrollment`: register a member from identity data + reference photo; extract embedding via `IFaceEmbeddingExtractor`; persist to the vector store; reject photos with zero or multiple detected faces.
- `member-directory-query`: given a face embedding, deterministically return the closest enrolled member and similarity score (or no-match), exposed as Enrollment's public API.

### Modified Capabilities
None — greenfield, nothing exists to modify.

## Approach

Modular monolith: `Enrollment` owns its Domain/Application/Infrastructure and the SQLite vector store; a small `Common` project holds only cross-cutting technical config (vector store settings, OpenTelemetry wiring) — never shared business models. DI registration follows one composition-root extension per module (`AddEnrollment(...)`), `TryAddScoped`, Options pattern for config.

**Architecture decisions recorded (precedent for this greenfield app)**:
1. Face embeddings via FaceAiSharp/ArcFace ONNX, local-only — not Azure Face API (Limited Access, Microsoft-account-team gated) and not LLM-arbitrated (non-deterministic, unauditable on a security path).
2. Vector store: `CommunityToolkit.VectorData.SqliteVec` (preview) — not `Microsoft.SemanticKernel.Connectors.*`, per research correction.
3. Identity matching is always deterministic vector similarity in plain C#; an `IAccessDecisionPolicy` (Allowed/Denied/HumanVerificationRequired) will consume similarity scores in `monitoring-pipeline` — never an LLM as sole arbiter.
4. Modular-monolith split: `Enrollment` and `Monitoring` as separate modules from day one, communicating only via `IMemberDirectoryReader`/integration events, never shared `DbContext`.
5. `Microsoft.Agents.AI.Workflows` is the standing choice for pipeline orchestration, applied starting in `monitoring-pipeline`.
6. `dotnet-testable-code-design` (constructor injection, `AutoMocker`) applies from Enrollment's first class.

## Affected Areas

| Area | Impact | Description |
|------|--------|--------------|
| `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `CamGuard.slnx` | New | CPM, net10.0, SDK 10.0.103 pin |
| `src/Enrollment.Domain` | New | Member aggregate, embedding value object |
| `src/Enrollment.Application` | New | Register-member command, query-by-embedding query |
| `src/Enrollment.Infrastructure` | New | FaceAiSharp extractor, SqliteVec repository |
| `src/Enrollment.PublicApi` | New | `IMemberDirectoryReader` port + DTOs |
| `src/Common` | New | Vector store config, OpenTelemetry wiring |
| `src/Enrollment.Console` (spike harness) | New | net10 FaceAiSharp compatibility spike |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `CommunityToolkit.VectorData.SqliteVec` is preview (1.0.1); API may break before GA | Medium | Pin exact version; isolate behind repository interface; re-check before release |
| FaceAiSharp net10 support is NuGet-"computed", not explicit | Medium | Run the spike harness first; fall back to FaceONNX (unverified alternative) only if the spike fails |
| False-accept/false-reject on a security system | High | Similarity-score contract designed now so `monitoring-pipeline` can apply `HumanVerificationRequired`; validate embedding quality with known-photo unit tests here |
| No test/CI infra yet (greenfield) | Medium | Establish `AutoMocker`-based unit tests from Enrollment's first class |
| Native ONNX Runtime dependency | Low | `FaceAiSharp.Bundle` ships model + runtime via NuGet; no separate install |

## Rollback Plan

Nothing outside this change depends on Enrollment yet. If the FaceAiSharp net10 spike fails or the SqliteVec preview package proves unusable: (1) swap the failing piece behind its existing port (`IFaceEmbeddingExtractor` or the vector-store repository) without touching Domain/Application, or (2) revert the branch/PR — deleting the newly-added `Enrollment.*`/scaffolding files fully removes the change since no other module references them yet.

## Dependencies

- FaceAiSharp (MIT, 0.6.35) + model packages (`FaceAiSharp.Bundle`).
- `CommunityToolkit.VectorData.SqliteVec` 1.0.1-preview + `Microsoft.Data.Sqlite`.
- .NET 10 SDK 10.0.103.

## Success Criteria

- [ ] Solution builds on net10.0/SDK 10.0.103 with CPM.
- [ ] FaceAiSharp net10 spike passes before Infrastructure depends on it.
- [ ] A reference photo enrolls end-to-end: identity + photo → embedding → persisted vector.
- [ ] Querying with a known photo's embedding returns that same member via `IMemberDirectoryReader`.
- [ ] Enrollment has zero incoming references from outside its own project boundary.
