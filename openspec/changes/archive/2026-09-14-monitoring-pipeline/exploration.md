# Exploration: monitoring-pipeline (CamGuard Monitoring module)

## Current State

CamGuard's first change, `ai-access-control` (archived at `openspec/changes/archive/2026-09-14-ai-access-control/`), delivered a modular monolith with `src/Enrollment.{Domain,Application,Infrastructure,PublicApi}` + `src/Common`, all net10.0/CPM. The only surface Monitoring may reference is `Enrollment.PublicApi.IMemberDirectoryReader`:

```csharp
public interface IMemberDirectoryReader
{
    Task<MemberMatch?> FindBestMatchAsync(float[] embeddingVector, CancellationToken cancellationToken);
}
public sealed record MemberMatch(Guid MemberId, double Score);
```

**Critical, previously-unsurfaced correction**: `MemberMatch.Score` is **cosine distance**, not similarity. Confirmed in `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` (`[VectorStoreVector(..., DistanceFunction = DistanceFunction.CosineDistance)]`) and in design.md's resolved Open Question: "`FindNearestAsync`'s Score is therefore a cosine-distance value (lower is more similar), passed through unchanged — `monitoring-pipeline` must interpret it accordingly (not as a 0–1 similarity score)." Every threshold/naming decision below reflects this.

Conventions to mirror (confirmed from source, not re-derived): one composition-root extension per module (`AddEnrollment(...)` in `EnrollmentServiceCollectionExtensions.cs`), Options pattern (`VectorStoreOptions` bound via `services.AddOptions<T>().Configure(o => configuration.GetSection(...).Bind(o))` — closing over the `configuration` parameter directly, not resolving `IConfiguration` from the container, per a PR3 DI-test finding), keyed `ActivitySource`/`Meter` per module via `Common.ObservabilityServiceCollectionExtensions.AddNamedObservabilitySource(name)` + `[FromKeyedServices(name)]` injection (never a static field). Course repo confirms `Microsoft.Agents.AI.Workflows` patterns: `WorkflowBuilder`/`AgentWorkflowBuilder`, custom `Executor<TIn,TOut>`, conditional edges via `Func<object?,bool>`, `context.QueueStateUpdateAsync`/`ReadStateAsync`/`AddEventAsync`/`YieldOutputAsync`, and `RequestInfoEvent`/`ToolApprovalRequestContent` for console-blocking human approval (Chapter 5); OpenTelemetry `ActivitySource`+`TracerProvider.AddSource(name)` pattern (Chapter 6) matches Enrollment's `"CamGuard.Enrollment"` naming — Monitoring should register `"CamGuard.Monitoring"` the same way.

## Affected Areas

- `src/Enrollment.PublicApi/IMemberDirectoryReader.cs` — the only Enrollment surface Monitoring may reference; new Monitoring projects must project-reference `Enrollment.PublicApi.csproj` only.
- `src/Common/ObservabilityServiceCollectionExtensions.cs`, `VectorStoreOptions.cs` — reuse pattern for Monitoring's own named ActivitySource/Meter and new Options classes (e.g. `AccessDecisionOptions`, watch-folder settings).
- New: `src/Monitoring.Domain` (`AccessOutcome`, `AccessDecisionResult`, `PendingVerification`), `src/Monitoring.Application` (`IAccessDecisionPolicy`, `INotificationService`, workflow executors, folder-watch consumer contract), `src/Monitoring.Infrastructure` (`LoggingNotificationService`, `FileSystemWatcher`-based watcher, `IImageArchiver`, `AddMonitoring(...)` composition root), consuming `Microsoft.Agents.AI.Workflows`.
- `openspec/changes/archive/2026-09-14-ai-access-control/research.md` — already covers `FileSystemWatcher` reliability caveats; do not re-research.

## Approaches

### 1. Folder-watch → workflow shape

1. **Channel-debounced watcher + one independent workflow run per file** — `FileSystemWatcher.Created` → bounded `Channel<string>` → background consumer debounces (retry `FileStream.Open(FileShare.None)` with backoff) → `InProcessExecution.RunStreamingAsync(workflow, ...)` once per validated path; completion signaled by moving the file to `processed/`/`quarantine/` via an injected `IImageArchiver` port.
   - Pros: matches events' causal independence; auditable (file preserved, not deleted); testable (`IImageArchiver` mockable per dotnet-testable-code-design); reuses research already done for `FileSystemWatcher`.
   - Cons: needs an explicit concurrency-bound decision (Enrollment's ONNX singleton detector/embedder thread-safety under concurrent `Run()` calls is unconfirmed).
   - Effort: Medium.
2. **Log-only "done" signal, no file move** — same watcher/channel, but only log completion, leave file in place.
   - Pros: simpler, no new port.
   - Cons: watched folder grows unbounded; a process restart risks reprocessing already-handled files since `FileSystemWatcher` has no persistent processed-state; worse audit story for a security system.
   - Effort: Low, but not recommended.

### 2. Person/object detection

1. **Reuse FaceAiSharp's SCRFD face detector as the person-detection proxy** — if ≥1 face found, treat as person-present (and reuse the same detection for the ArcFace crop downstream); if 0 faces, route to a distinct `NoFaceDetected` outcome (never silently "not a person").
   - Pros: zero new ML dependency; one inference call serves both "person present" and "identity crop"; keeps Enrollment/Monitoring's stack uniform.
   - Cons: misses masked/back-turned/very-small-in-frame people — mitigated, not solved, by never collapsing "no face" into "ignore," instead defaulting that case to `HumanVerificationRequired`.
   - Effort: Low.
2. **Dedicated general object detector (YOLO-family ONNX, e.g. `YoloDotNet` — confirmed to exist on NuGet, actively versioned, ONNX-Runtime-based)** via `Microsoft.ML.OnnxRuntime` directly.
   - Pros: genuine person/animal/object discrimination independent of face visibility.
   - Cons: new ML dependency, new `IPersonPresenceDetector` port, unverified license/maintenance/model provenance (real research gap, not yet checked), added inference cost per event.
   - Effort: Medium-High.
   - **Recommendation: Option 1 for this change**; revisit Option 2 only if real deployment data shows unacceptable false-negative rates from masked/back-turned events.

### 3. Cross-module `IMemberDirectoryReader` consumption

1. **In-process DI reference (as already decided in ai-access-control's design)** — Monitoring's composition root depends on `Enrollment.PublicApi.IMemberDirectoryReader`, registered by the host calling both `AddEnrollment(...)` and `AddMonitoring(...)`.
   - Pros: matches `dotnet-modular-monolith`'s decision gate ("immediate answer required → PublicApi + boundary DTOs"); identity verification needs a synchronous per-event answer, not eventual consistency; zero new infra.
   - Cons: none material at current scale (small member roster, already established).
   - Effort: Low. **Recommended — just detail, don't relitigate.**
2. Integration-event/message decoupling — rejected for this concern; correct only for propagating `MemberEnrolled`/`MemberRevoked` state changes to a local cache, which isn't needed yet at this scale.

### 4. `IAccessDecisionPolicy` shape

1. **Single deterministic class implementing a small interface** — `AccessDecisionPolicy : IAccessDecisionPolicy`, input = raw cosine distance only, output = `AccessDecisionResult(AccessOutcome, double Distance, string Reason)`; two thresholds (`AllowedMaxDistance`, `DeniedMinDistance`, named for distance not naive "score") via `AccessDecisionOptions` bound like `Common.VectorStoreOptions`, with startup validation `AllowedMaxDistance < DeniedMinDistance`.
   - Pros: matches `solid-ocp-strategy-refactor`'s own decision gate ("single one-off conditional, unlikely to grow → Strategy is overkill"); interface still allows DI substitution/testing without Strategy ceremony.
   - Cons: none identified for a 2-threshold, 3-branch decision.
   - Effort: Low. **Recommended.**
2. Strategy + Null Object per solid-ocp-strategy-refactor's general pattern — rejected: no repeated type/behavior branch exists here, and no forecasted growth in outcome variants; would be over-engineering per the skill's own guidance.

### 5. `INotificationService`/`LoggingNotificationService`

- Shape confirmed unchanged: `NotifyAsync(AccessAlert alert, CancellationToken ct)`.
- `AccessAlert` should carry: `MemberId? MemberId` (null = unidentified), `double Distance` (raw cosine distance, correctly named), `AccessOutcome Outcome`, `string ImageReference` (archived path, never raw bytes), `DateTimeOffset OccurredAtUtc` (via injected `TimeProvider`, never `DateTime.UtcNow` directly), optional `string? Reason`.
- `dotnet-email-delivery` stays out of scope — confirmed by proposal.md's existing "Any real notification channel (email/SMS)... deferred" line; only `LoggingNotificationService` ships now.

### 6. Human-in-the-loop wiring

1. **Terminal pending-review outcome, not a suspended workflow** — `HumanVerificationRequired` completes the run, persists a `PendingVerification` record (`ImageReference`, `Distance`, candidate `MemberId?`, `OccurredAtUtc`, `Status = Pending`), and fires `INotificationService`; a future UI/API resolves it via a separate write path.
   - Pros: keeps each workflow run bounded/independent (consistent with topic 1's answer); avoids needing durable/suspended-workflow checkpointing that hasn't been designed; mirrors the existing "placeholder now, real channel later" precedent from `INotificationService`.
   - Cons: not literal human-in-the-loop workflow suspension; a future "resume this exact run on approval" requirement would need new research into `Microsoft.Agents.AI.Workflows` checkpointing.
   - Effort: Low. **Recommended.**
2. Literal `RequestInfoEvent`/`ToolApprovalRequestContent` console-blocking pattern from the course — rejected for real deployment: designed for one interactive operator resuming an in-memory `StreamingRun`, doesn't fit unattended motion events or asynchronous human review.

## Recommendation

Proceed to `sdd-propose` with: FaceAiSharp SCRFD reused as the person-detection proxy (3-state detection outcome, "no face" routed to `HumanVerificationRequired`, never ignored); a single deterministic `AccessDecisionPolicy` class (no Strategy pattern) operating on cosine-distance thresholds via Options; direct in-process `IMemberDirectoryReader` consumption restricted to `Enrollment.PublicApi` project references; `INotificationService`/`LoggingNotificationService` as already scoped with the detailed `AccessAlert` DTO above; `HumanVerificationRequired` as a terminal pending-review outcome + notification, not a blocking workflow suspension; one independent workflow run per validated file via a Channel-debounced `FileSystemWatcher` consumer, with move-to-`processed/` as the completion signal via an `IImageArchiver` port.

## Risks

- Distance-vs-similarity score direction is the highest-risk naming/logic bug surface for this change — must be made explicit in spec/design wording, not left implicit as in the prior change's early exploration.
- `YoloDotNet` (or any general object detector) is unverified for license/maintenance/model provenance — not adopted now, but flag for a future change if needed.
- FaceAiSharp's singleton `IFaceDetectorWithLandmarks`/`IFaceEmbeddingsGenerator` thread-safety under concurrent workflow runs is unconfirmed — verify before enabling parallel image processing.
- No durable/suspended-workflow story exists for a hypothetical future "resume exact run on human approval" requirement — current recommendation is a simpler pending-record placeholder, explicitly not equivalent.

## Ready for Proposal

Yes — all 6 requested topics covered with concrete, source-grounded recommendations and tradeoffs; no blocking unknowns for `sdd-propose`. Flagged research gaps (FaceAiSharp singleton thread-safety) are non-blocking and addressable in `sdd-research` if selected.
