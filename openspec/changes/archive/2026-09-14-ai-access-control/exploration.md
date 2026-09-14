# Exploration: ai-access-control (CamGuard AI-managed physical access control)

## Current State

Greenfield .NET 10 project (`C:\Users\danielvl\Desktop\Exercices\CamGuard` is empty, no `.git`). Planned stack per `sdd-init/camguard`: net10.0, SDK 10.0.103, Central Package Management, `Microsoft.Agents.AI.{OpenAI,AzureAI,Workflows,DevUI}`, modeled on reference course repo `C:\Users\danielvl\Desktop\Exercices\getting-started-microsoft-agent-framework-in-dotnet-master`.

Spot-checked the reference repo directly (`Directory.Packages.props`, `Chapter04.Console/Demos.cs`, `Chapter05.Console/Demos.cs`): **no vector-store or embedding packages exist there** — vector storage and face embeddings are net-new decisions for CamGuard, not something to copy from the course. Confirmed reusable patterns:

- Agent factory: `client.GetChatClient("gpt-4o-mini").AsIChatClient().AsAIAgent(instructions, name, tools)`.
- Tools: `AIFunctionFactory.Create(...)`, human-in-the-loop via `ApprovalRequiredAIFunction` + `ToolApprovalRequestContent` (approve/deny round-trip through `RequestInfoEvent`/`SendResponseAsync`).
- Structured output: `agent.RunAsync<T>()` returning a `record`/`enum` shape, validated with `System.ComponentModel.DataAnnotations` + manual retry loop.
- Workflows (`Microsoft.Agents.AI.Workflows`): `WorkflowBuilder`, custom `Executor<TIn,TOut>`, conditional edges via `Func<object?, bool>` (exactly the shape needed for detect→verify branching), `WithOutputFrom`, shared state via `IWorkflowContext.QueueStateUpdateAsync`/`ReadStateAsync`, custom `WorkflowEvent`s.

## Affected Areas

Nothing exists yet to affect — this is scaffolding, not modification. Areas to be *created*:

- `src/Enrollment.*` — member card + reference photo ingestion + face embedding generation + vector store writes.
- `src/Monitoring.*` — folder watcher, detect/identify/decide/notify workflow.
- `src/Common`/`SharedKernel` — vector store config, observability wiring, notification abstraction (only if genuinely cross-module technical, not business, shared code).
- `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `.slnx` — new, mirroring reference repo conventions (net10.0, SDK 10.0.103, CPM).

## Approaches

1. **Local-first ONNX face embeddings (FaceAiSharp/ArcFace) + SQLite vector store** — extract face embeddings locally via FaceAiSharp (ArcFace-family ONNX model, e.g. InsightFace buffalo_s/MobileFaceNet, ~13MB, runs on `Microsoft.ML.OnnxRuntime` CPU), persist via `Microsoft.Extensions.VectorData` SQLite connector.
   - Pros: fully offline, no per-call cost, no external approval process, works with `Microsoft.Extensions.VectorData` abstractions directly, keeps face-recognition cleanly decoupled from LLM/chat embeddings.
   - Cons: extra native dependency (ONNX Runtime), model licensing/size to manage, accuracy is "good enough open-source" not enterprise-SLA.
   - Effort: Medium.

2. **Azure AI Vision Face API for identification/verification** — use Microsoft's managed Face service for both detection and identity matching.
   - Pros: managed service, no local model/ONNX Runtime to maintain, stays inside the Azure/.NET stack already used for chat.
   - Cons: **Face identification/verification are Limited Access gated features** requiring registration and Microsoft vetting (S0/E0 tier only, restricted use cases) — not realistically obtainable for a fast greenfield first phase. Face *detection* alone is open, but that alone doesn't solve identity matching.
   - Effort: Low (if access were granted) / **Blocked** in practice for phase 1.

3. **Delegate identity matching to an LLM/vision chat call** — reuse `agent.RunAsync<T>()` for the whole detect+identify+decide pipeline in one or two chat calls.
   - Pros: minimal new infra, fully reuses course patterns.
   - Cons: no LLM produces calibrated face-recognition embeddings comparable to ArcFace-family models; using an LLM as the identity arbiter makes the security-critical decision non-deterministic, unauditable, and hard to unit test — conflicts with `dotnet-testable-code-design` and `dotnet-clean-architecture` (business invariants must live in Domain/Application, not a probabilistic external dependency).
   - Effort: Low effort, but wrong shape for a security system.

## Recommendation

**Approach 1** (FaceAiSharp/ArcFace-family ONNX embeddings + SQLite-backed `Microsoft.Extensions.VectorData` store), combined with a clear split of responsibilities:

- **Face-embedding extraction** behind its own port (e.g. `IFaceEmbeddingExtractor`) — explicitly NOT the same abstraction as `IChatClient`/`Microsoft.Extensions.AI` text embeddings, because face-recognition embeddings (ArcFace-style, margin-loss trained) and LLM text/multimodal embeddings solve different problems and are not interchangeable.
- **Detection pipeline**: two separate steps, not one call — (a) a cheap deterministic local detector (can reuse the SCRFD face detector FaceAiSharp already bundles) gates most false triggers (animal/object/motion noise) cheaply; an LLM `RunAsync<T>()` call MAY optionally add richer person-vs-animal scene reasoning on top, but (b) identity verification is ALWAYS a deterministic vector-similarity search against the enrolled member store in plain code — never delegated to an LLM.
- **Three-way decision**: `record AccessDecisionResult(AccessOutcome Outcome, double Confidence, string Reason)` / `enum AccessOutcome { Allowed, Denied, HumanVerificationRequired }` (mirrors the course's `ReviewAnalysisV2` + enum pattern). Threshold logic (e.g. similarity ≥ 0.65 → Allowed, 0.45–0.65 → HumanVerification, < 0.45 → Denied) lives in a **deterministic C# policy** (`IAccessDecisionPolicy.Decide(score, profile)`), not inside model structured output — auditable, versionable, unit-testable. `RunAsync<T>()`/LLM stays reserved for judgment-based enrichment (narrative "Reason" text, optional scene classification), never the sole arbiter of Allowed/Denied.
- **Folder watching**: `FileSystemWatcher` → bounded `Channel<string>` → background consumer debounces (poll file-size stability or retry `FileStream` open with `FileShare.None`) before treating a file as ready → hand the validated path to the workflow as input. This queueing/debounce layer is plain infrastructure code, sitting outside `Microsoft.Agents.AI.Workflows`.
- **Pipeline orchestration**: `Microsoft.Agents.AI.Workflows` `WorkflowBuilder` with custom `Executor<TIn,TOut>` steps (DetectPerson → IdentifyMember → Decide → Notify), conditional edges (`Func<object?, bool>`, same shape as the course's `SpamDetectionExecutor`), human-in-the-loop approval for `HumanVerificationRequired` reusing the course's `RequestInfoEvent`/`ToolApprovalRequestContent` approve/deny mechanism.
- **Notifications**: `INotificationService.NotifyAsync(AccessAlert alert, CancellationToken ct)`, channel-agnostic `AccessAlert` DTO (no email-specific fields baked in). Phase 1 ships `LoggingNotificationService` (ILogger + OpenTelemetry trace/span correlation per `dotnet-observability`). Real channel deferred; `dotnet-email-delivery` skill is the right one to load later, not now.
- **Module boundaries**: modular monolith, one solution, two modules — `Enrollment` (owns member card + photo ingestion + embedding generation + vector store writes) and `Monitoring` (owns watcher + pipeline, read-only access to member vectors via a small `IMemberDirectoryReader` port or `MemberEnrolled`/`MemberRevoked` integration events, never touching Enrollment's internals or DbContext directly). Small `Common`/`SharedKernel` only for cross-cutting technical concerns (vector store config, observability), never shared business models — per `dotnet-modular-monolith`.

## Risks

- Azure Face API identify/verify is Limited-Access gated (Microsoft registration/vetting required, S0/E0 tier only) — do not assume it is available; treat local ONNX (FaceAiSharp) as the only realistic phase-1 path for actual face-recognition embeddings.
- `FileSystemWatcher` can silently drop events under burst load (internal buffer overflow) and fires before a writer finishes (partial-write reads) — needs an explicit debounce/queue layer, not naive direct wiring into the workflow.
- Face-recognition accuracy/bias and false-accept/false-reject risk on a physical access-control system is a genuine product risk; the `HumanVerificationRequired` outcome is the correct mitigating design already in scope, but threshold tuning needs to be revisited per deployment/lighting conditions.
- No test/CI infrastructure exists yet (greenfield); `dotnet-testable-code-design` (interface injection, AutoMocker) should be established from the very first module rather than retrofitted later.

## Ready for Proposal

Yes. All 6 requested investigation areas (vector storage, folder-watch pipeline, detect-vs-identify, structured decision, notification abstraction, module boundaries) have concrete, source-checked recommendations. `sdd-propose` can proceed to scope Enrollment vs. Monitoring as the first change(s).
