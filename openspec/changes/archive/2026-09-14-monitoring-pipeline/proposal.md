# Proposal: Monitoring Pipeline (Detect → Verify → Decide → Notify)

## Intent

CamGuard's Enrollment module (archived `ai-access-control` change) can register members and answer "who is this embedding closest to," but nothing yet consumes that capability against live camera/folder events. This change builds the Monitoring module: watch a drop folder for images, detect a person, verify identity against `IMemberDirectoryReader`, apply a deterministic access decision, and notify — the second and final vertical slice needed for a working access-control loop. It builds strictly on top of `ai-access-control`'s already-shipped decisions; it does not re-decide module boundaries, embedding strategy, or vector store.

## Scope

### In Scope
- Folder-watch ingestion: `FileSystemWatcher` → debounced channel → one independent workflow run per validated file, completion signaled by archiving to `processed/`/`quarantine/`.
- Person-detection reusing FaceAiSharp's SCRFD face detector (no new ML dependency).
- Identity verification via `IMemberDirectoryReader`, correctly treating `MemberMatch.Score` as cosine **distance** (lower = closer), never similarity.
- Deterministic `IAccessDecisionPolicy` (Allowed / Denied / HumanVerificationRequired), two named distance thresholds.
- `INotificationService` / `LoggingNotificationService` firing an `AccessAlert` for every outcome.
- Terminal `PendingVerification` record for `HumanVerificationRequired` (not a suspended workflow).

### Out of Scope
- Any real notification channel (email/SMS).
- UI/API to resolve/close `PendingVerification` records.
- GPU/DirectML execution providers (CPU only; DirectML documented as unsafe for concurrent `Run()`).
- General object detection (`YoloDotNet`/YOLO-family) — unverified license/provenance, not adopted.
- Durable/suspended-workflow checkpointing for a literal future human-approval resume.
- A production hosting entrypoint (`BackgroundService`/`IHostedService`) wiring ingestion → workflow → archival together — this change delivers the components and proves their wiring via an integration test; a runnable host is a follow-up.

**Single-change decision**: unlike `ai-access-control` (explicitly phased), this proposal stays ONE change. Monitoring is one cohesive vertical slice consuming an already-stable Enrollment contract — there is no equivalent "risky greenfield scaffolding vs. module logic" split to make. The review-workload guard (Section E) still applies at `sdd-tasks`; expect a chained/stacked-PR recommendation given six new capabilities.

## Capabilities

### New Capabilities
- `motion-image-ingestion`: folder-watch → debounce → one workflow run per file, archived on completion.
- `person-detection`: SCRFD-based person-presence proxy; zero faces routes to `HumanVerificationRequired`, never ignored.
- `identity-verification`: query `IMemberDirectoryReader`, interpret `Score` as cosine distance.
- `access-decision-policy`: single deterministic class, two distance thresholds, three outcomes.
- `access-notification`: `AccessAlert` DTO fired for every decision.
- `pending-verification-record`: terminal record + notification for `HumanVerificationRequired`.

### Modified Capabilities
None — Monitoring only reads `IMemberDirectoryReader` unchanged; no Enrollment requirement changes.

## Approach

Modular monolith addition: `Monitoring.Domain/Application/Infrastructure`, project-referencing only `Enrollment.PublicApi`. **Architecture decisions recorded**: (1) FaceAiSharp SCRFD reused as person-detection proxy, no new model; (2) FaceAiSharp/OnnxRuntime concurrent `Run()` is medium-confidence safe on CPU per research — adopt the conservative fallback anyway: serialize detector/embedder access via `SemaphoreSlim(1,1)` or a single-consumer queue; (3) `AccessDecisionPolicy` is one deterministic class, not a Strategy — no repeated-branch growth exists; (4) `IMemberDirectoryReader` consumed in-process via DI, host composes `AddEnrollment(...)` + `AddMonitoring(...)`; (5) all naming (`Distance`, never `Score`/`Similarity`) enforced from Domain through Options/thresholds; (6) `Microsoft.Agents.AI.Workflows` orchestrates detect→verify→decide→notify per executor, following the reference-repo patterns already used for observability naming (`"CamGuard.Monitoring"`); (7) `HumanVerificationRequired` is a terminal `PendingVerification` record, not workflow suspension.

## Affected Areas

| Area | Impact | Description |
|------|--------|--------------|
| `src/Enrollment.PublicApi` | Referenced, unchanged | Monitoring project-references this only |
| `src/Monitoring.Domain` | New | `AccessOutcome`, `AccessDecisionResult`, `PendingVerification` |
| `src/Monitoring.Application` | New | `IAccessDecisionPolicy`, `INotificationService`, `IImageArchiver`, workflow executors |
| `src/Monitoring.Infrastructure` | New | `LoggingNotificationService`, `FileSystemWatcher` consumer, semaphore-guarded FaceAiSharp adapter, `AddMonitoring(...)`, Options classes |
| `src/Common` | Reused, unchanged | `AddNamedObservabilitySource` pattern reused for `"CamGuard.Monitoring"` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| FaceAiSharp/OnnxRuntime concurrent `Run()` safety is medium-confidence (Discussions, not official docs) | Medium | Serialize via semaphore/single-consumer queue per research.md; revisit only after a load test |
| Cosine-distance-vs-similarity naming/logic inversion | High | `Distance` naming everywhere; startup validation `AllowedMaxDistance < DeniedMinDistance`; explicit spec wording |
| No durable-workflow story for a future literal human-approval resume | Low | Explicitly deferred; `PendingVerification` is a terminal record, not a checkpoint |
| `FileSystemWatcher` reliability (missed events, locked files) | Medium | Reuse `ai-access-control`'s already-researched debounce/retry pattern |
| Six new capabilities may approach the 400-line PR review budget | Medium | `sdd-tasks` MUST apply the review-workload guard; recommend chained PR slices if forecast is high |

## Rollback Plan

Monitoring is isolated behind its own composition root; nothing outside it exists yet except the host calling `AddMonitoring(...)`. To revert: remove that call and delete `src/Monitoring.*` — `Enrollment`/`IMemberDirectoryReader` are read-only to Monitoring and remain untouched. No data migration exists to reverse; `PendingVerification` records are additive-only.

## Dependencies

- `Enrollment.PublicApi.IMemberDirectoryReader` (archived `ai-access-control` change).
- FaceAiSharp (already an Enrollment dependency, reused, no new package).
- `Microsoft.Agents.AI.Workflows` (already a standing project choice, first applied here).

## Success Criteria

- [ ] A dropped image with a known member's face resolves to `Allowed` and fires a notification.
- [ ] A dropped image with no detected face resolves to `HumanVerificationRequired` and persists a `PendingVerification` record.
- [ ] A dropped image with an unrecognized face resolves to `Denied`.
- [ ] Concurrent image drops never crash or corrupt the FaceAiSharp detector/embedder (serialized access holds under load).
- [ ] `Monitoring.*` references only `Enrollment.PublicApi`, never Enrollment's internal projects.
