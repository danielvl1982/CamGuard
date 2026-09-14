# Tasks: Monitoring Pipeline (Detect → Verify → Decide → Notify)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1400–1800 (4 new projects + 6 capabilities + observability + 9 test files) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (scaffold+Domain+Application) → PR 2 (Infrastructure+Observability) → PR 3 (Tests) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main (precedent from `ai-access-control`) |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Note: `ai-access-control`'s similarly-sized per-unit estimates still landed at 349–618 real lines per PR. Treat ~1400–1800 as a floor, not a ceiling.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | Scaffolding + Domain + Application ports/executors (no I/O) | PR 1 | `dotnet test --filter AccessDecisionResult\|PendingVerification` | N/A — no I/O boundary yet | Delete `src/Monitoring.Domain`, `src/Monitoring.Application` |
| 2 | Infrastructure adapters + composition root + Observability | PR 2 | `dotnet test --filter AccessDecisionPolicy\|NotifyExecutor` | `dotnet build` + manual drop-folder smoke test | Delete `src/Monitoring.Infrastructure`, remove `AddMonitoring(...)` host call |
| 3 | Tests (unit + folder-watch integration harness) | PR 3 | `dotnet test --filter Monitoring` | Folder-watch integration test itself is the harness | Delete `tests/Monitoring.UnitTests` new files |

## Phase 1: Solution Scaffolding

- [x] 1.1 Add `Microsoft.Agents.AI.Workflows` `PackageVersion` to `Directory.Packages.props` — not yet pinned despite design's assumption
- [x] 1.2 Create `src/Monitoring.Domain/Monitoring.Domain.csproj` (net10.0, CPM, `CSharpFunctionalExtensions` PackageReference)
- [x] 1.3 Create `src/Monitoring.Application/Monitoring.Application.csproj`, reference `Monitoring.Domain` + `Enrollment.PublicApi` only
- [x] 1.4 Create `src/Monitoring.Infrastructure/Monitoring.Infrastructure.csproj`, reference `Monitoring.Application`; add `Microsoft.Agents.AI.Workflows`, `FaceAiSharp`, `FaceAiSharp.Bundle`, `Microsoft.ML.OnnxRuntime`
- [x] 1.5 Create `tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj`, reference all `Monitoring.*` + AutoMocker/xunit
- [x] 1.6 Add all four new projects to `CamGuard.slnx`
- [x] 1.7 Verify `dotnet build` succeeds on the empty scaffold

## Phase 2: Domain (`src/Monitoring.Domain`)

- [x] 2.1 Create `AccessOutcome.cs` enum (`Allowed`/`Denied`/`HumanVerificationRequired`)
- [x] 2.2 Create `AccessDecisionResult.cs` sealed record (`Outcome`, `Distance`, `Reason`)
- [x] 2.3 Create `PendingVerificationId.cs` — local `sealed record : IComparable<PendingVerificationId>`, mirrors `MemberId`, never references `Enrollment.Domain`
- [x] 2.4 Create `PendingVerificationStatus.cs` enum (`Pending`)
- [x] 2.5 Create `PendingVerification.cs` : `CSharpFunctionalExtensions.Entity<PendingVerificationId>` directly (never `EntityBase<TId>`); `Create(...)` factory defaults `Status` to `Pending`

## Phase 3: Application Ports + Workflow Executors (`src/Monitoring.Application`)

- [x] 3.1 Create `IPersonDetector.cs` + `PersonDetectionResult` record; document single-person-per-image scoping in XML comment
- [x] 3.2 Create `IAccessDecisionPolicy.cs`
- [x] 3.3 Create `INotificationService.cs` + `AccessAlert.cs` record
- [x] 3.4 Create `IImageArchiver.cs`
- [x] 3.5 Create `IIngestionSource.cs`
- [x] 3.6 Create `IPendingVerificationRepository.cs` port (`SaveAsync`)
- [x] 3.7 Create `DetectPersonExecutor.cs` — conditional short-circuit edge to Notify on zero faces, synthesizes `HumanVerificationRequired`
- [x] 3.8 Create `VerifyIdentityExecutor.cs` — calls `IMemberDirectoryReader`, forwards distance unmodified
- [x] 3.9 Create `DecideAccessExecutor.cs` — calls `IAccessDecisionPolicy`
- [x] 3.10 Create `NotifyExecutor.cs` — try/catch isolates `NotifyAsync` failures (log, never rethrow); persists `PendingVerification` on `HumanVerificationRequired`; always proceeds to archival
- [x] 3.11 Create `MonitoringWorkflowHost.cs` — `WorkflowBuilder` graph wiring all four executors

## Phase 4: Infrastructure Adapters (`src/Monitoring.Infrastructure`)

- [x] 4.1 Create `AccessDecisionOptions.cs` (`AllowedMaxDistance`, `DeniedMinDistance`) + `WatchFolderOptions.cs`
- [x] 4.2 Create `AccessDecisionPolicy.cs` : `IAccessDecisionPolicy` — deterministic three-outcome boundary logic
- [x] 4.3 Create `LoggingNotificationService.cs` : `INotificationService` (`ILogger`-based)
- [x] 4.4 Create `FileSystemImageArchiver.cs` : `IImageArchiver` (moves to `processed/`/`quarantine/`)
- [x] 4.5 Create `FileSystemWatchIngestionSource.cs` : `IIngestionSource` — `FileSystemWatcher.Created` → bounded `Channel<string>` → debounce-retry consumer
- [x] 4.6 Create `FaceAiSharpPersonDetector.cs` : `IPersonDetector` — single-consumer `Channel<PersonDetectionRequest>` serializing SCRFD detect+embed calls
- [x] 4.7 Create `InMemoryPendingVerificationRepository.cs` : `IPendingVerificationRepository` (design's default; upgradeable later)
- [x] 4.8 Create `MonitoringServiceCollectionExtensions.cs` — `AddMonitoring(...)`, options validation (`AllowedMaxDistance < DeniedMinDistance`), all DI registrations

## Phase 5: Observability

- [x] 5.1 Add `"CamGuard.Monitoring"` `ActivitySourceName` const + `AddNamedObservabilitySource` call in `AddMonitoring(...)`
- [x] 5.2 Instrument executors with `Monitoring.Ingest`/`DetectPerson`/`VerifyIdentity`/`Decide`/`Notify` spans + tags per design's Observability table

## Phase 6: Tests (`tests/Monitoring.UnitTests`)

- [x] 6.1 `FileSystemWatchIngestionSourceTests.cs` — new file triggers ingestion; locked file retried with backoff; one workflow run per file
- [x] 6.2 `FileSystemImageArchiverTests.cs` — success archives to `processed/`; failure archives to `quarantine/`
- [x] 6.3 `DetectPersonExecutorTests.cs` — faces proceed to verify; zero faces routes to `HumanVerificationRequired`, verify never invoked, result never silently ignored
- [x] 6.4 `VerifyIdentityExecutorTests.cs` — queries `IMemberDirectoryReader`; forwards `Distance` never as similarity; bit-for-bit passthrough at threshold; uses synthetic mocked embeddings of assumed `FaceEmbedding.ExpectedDimension` (still unconfirmed — do not depend on it being correct)
- [x] 6.5 `AccessDecisionPolicyTests.cs` — independent thresholds; `<=AllowedMaxDistance`→`Allowed`; `>=DeniedMinDistance`→`Denied`; strictly-between→`HumanVerificationRequired`; startup validation fails fast when `AllowedMaxDistance >= DeniedMinDistance`
- [x] 6.6 `NotifyExecutorTests.cs` — alert fired for all three outcomes including `Allowed`; alert excludes raw image bytes; notification-failure-isolation: `NotifyAsync` throws → logged, run completes, archival still invoked
- [x] 6.7 `PendingVerificationTests.cs` — persisted once per `HumanVerificationRequired` path (zero-face and mid-threshold); nullable fields reflect triggering path; initial status `Pending`; no automatic transition
- [x] 6.8 `MonitoringWorkflowIntegrationTests.cs` — temp watch root, real `FileSystemWatchIngestionSource`/`FileSystemImageArchiver`, mocked `IPersonDetector`/`IMemberDirectoryReader`; reuses `src/Enrollment.Console/testdata/blank-image.png` (read-only) fixture; asserts file moved and `NotifyAsync` invoked once
- [x] 6.9 Verify `dotnet test --filter Monitoring` passes

## Phase 7: Verification

- [x] 7.1 Verify `Monitoring.*` project references resolve to `Enrollment.PublicApi` only — no `Enrollment.Domain`/`Enrollment.Application`/`Enrollment.Infrastructure` reference exists in any `Monitoring.*.csproj`
- [x] 7.2 Run full `dotnet build` + `dotnet test` across the solution

## PR4: Verify Warning Follow-up

Closes 2 non-blocking WARNINGs from the `pass_with_warnings` verify report (`evidence_revision: sha256:2d2ea525f75025093bba28d6709639b6722f91afb57784a81a47c64c95a43b84`). Verify had already passed; this is a voluntary quality follow-up before archiving.

- [x] 8.1 Add `DecideAccessExecutorTests.cs` covering the no-match (`Distance == null`) → `Denied` branch, asserting `IAccessDecisionPolicy.Decide(...)` is `Times.Never` called
- [ ] 8.2 Document the missing production ingestion host as an explicit, intentional out-of-scope decision in `proposal.md`'s Out of Scope list and `design.md`'s Open Questions
