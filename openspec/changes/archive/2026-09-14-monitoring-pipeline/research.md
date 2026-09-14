# Research: FaceAiSharp singleton thread-safety under concurrent calls

Change: monitoring-pipeline
Date: 2026-09-14

## Question
Is it safe to register FaceAiSharp's `IFaceDetectorWithLandmarks` / `IFaceEmbeddingsGenerator`
(created via `FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks()` /
`CreateFaceEmbeddingsGenerator()`) as a DI singleton and call it concurrently from multiple
threads/tasks — or does it need external synchronization?

## Findings

### 1. FaceAiSharp itself is silent on thread safety
- Repo: https://github.com/georg-jung/FaceAiSharp (MIT, v0.6.35, .NET 8.0+)
  - README shows the bundle-factory usage pattern (`FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks()`)
    but contains no statement about thread safety, singleton lifetime, concurrency, or disposal semantics.
  - Open issues (as of this research: #37, #43, #45, #46, #56, #62, #66, #71) — none mention
    thread safety, concurrency, singleton DI, or multi-threaded usage.
  - Conclusion: no explicit guarantee or explicit warning exists in the project's own documentation/issues.

### 2. The underlying Microsoft.ML.OnnxRuntime.InferenceSession — official/maintainer guidance
- ONNX Runtime GitHub Discussion #10107, "ONNX Runtime Thread Safety":
  https://github.com/microsoft/onnxruntime/discussions/10107
  - Maintainer statement: "sessions themselves are thread safe after construction" and
    "Multiple threads can invoke the Run() method on the same inference session object... You
    should create one session and use it in multiple threads. ORT is designed for this."
  - Caveat noted by the same maintainer: uncertainty around "more complicated bits of the API
    around custom allocators" — i.e., the guarantee is for standard usage, not necessarily every
    advanced allocator/IOBinding configuration.
- ONNX Runtime GitHub Discussion #9441, DirectML thread safety:
  https://github.com/microsoft/onnxruntime/discussions/9441
  - Explicit exception: "the DirectML execution provider does not support parallel execution...
    it does not support multi-threaded calls to Run on the same inference session." Only one
    thread may call `Run()` at a time per session when DirectML EP is used; multiple sessions
    (separate objects) may run in parallel instead.
- ONNX Runtime GitHub Discussion #15300, "Parallelism of ONNX session objects with multiple threads":
  https://github.com/microsoft/onnxruntime/discussions/15300
  - Maintainer (Pranav Sharma, ORT core team) reiterates: sessions are thread-safe and designed
    for concurrent `Run()` calls; creating one session per thread is called "a huge waste of
    resources" and explicitly discouraged. Notes a performance caveat: shared allocators can incur
    "more lock contention between threads of different sessions" — a performance tradeoff, not a
    correctness issue.
- ONNX Runtime official performance docs, "Thread management":
  https://onnxruntime.ai/docs/performance/tune-performance/threading.html
  - Describes intra-op/inter-op thread pools configured per session, consistent with (but not an
    explicit restatement of) multi-threaded `Run()` being the intended usage pattern. Does not
    itself state the thread-safety guarantee in writing.
- Original GitHub issue #114, "Is InferenceSession.Run thread-safe?":
  https://github.com/microsoft/onnxruntime/issues/114
  - Confirms this has been an open community question since 2018; the page content fetched did
    not surface a maintainer answer directly on that issue (answer is documented instead in the
    Discussions above).

### 3. Conflicting secondary source (lowers confidence)
- Third-party tutorial, theneuralbase.com "Multi-model serving":
  https://theneuralbase.com/onnx/learn/advanced/multi-model-serving/
  - States: "The most common mistake is assuming InferenceSession objects are thread-safe for
    concurrent calls. They are not," and recommends a `threading.Lock` per session or one session
    per worker thread.
  - This is a third-party educational blog, not official Microsoft documentation, and cites no
    sources for the claim. It directly contradicts the ORT maintainer statements in (2). No
    official onnxruntime.ai page was found asserting non-thread-safety.

### 4. GPU-specific crash reports (execution-provider caveat, not core-contradiction)
- https://github.com/microsoft/onnxruntime/issues/26610 — crash instantiating multiple
  `InferenceSession`s on the same GPU from different threads (session *construction*
  contention/GPU driver issue, not `Run()` call safety on one already-constructed session).
- https://github.com/microsoft/onnxruntime/issues/22147 — feature request to make the DirectML
  EP support concurrent `Run()`, confirming DirectML is the known, currently-unsupported case.
- Relevance to CamGuard: these reports concern GPU/DirectML-specific behavior. CamGuard's
  Enrollment module already uses FaceAiSharp with `Microsoft.ML.OnnxRuntime` (not the DirectML
  or GPU-specific NuGet packages per the ai-access-control change background), so the CPU
  execution provider is the relevant one, and no crash reports were found for CPU EP concurrent
  `Run()`.

## Conclusion
- **CPU execution provider (CamGuard's presumed deployment target):** official-adjacent evidence
  (core maintainer statements in GitHub Discussions, corroborated by ORT's own per-session
  thread-pool design docs) supports that a single `InferenceSession` — and by extension a
  FaceAiSharp detector/embedder wrapping one — can be registered as a DI singleton and have its
  inference method called concurrently from multiple threads/tasks, without external locking,
  for correctness. Performance-wise, concurrent calls on one session will contend for the
  session's internal thread pool, which is a throughput consideration, not a correctness bug.
- **DirectML/GPU execution provider:** documented as NOT safe for concurrent `Run()` on the same
  session — requires one thread at a time, or separate session instances.
- **Confidence: MEDIUM.** No single official onnxruntime.ai documentation page states the
  guarantee in prose; the strongest evidence is maintainer statements in GitHub Discussions
  (semi-official, not a docs page), and one third-party source asserts the opposite without
  citation. FaceAiSharp adds no guidance of its own, so it inherits ONNX Runtime's ambiguity
  rather than resolving it.

## Recommendation for the design phase
Given the medium confidence and the presence of a contradicting (if lower-authority) source,
**do not rely on unsynchronized concurrent calls into a singleton FaceAiSharp detector/embedder
as the initial design.** Adopt the conservative default:
- Serialize access to the shared detector/embedder singleton via a `SemaphoreSlim(1,1)` guard
  around detect/embed calls, or
- Route motion-triggered images through a single-consumer channel/queue so only one inference
  call is in flight at a time.

This trades a small amount of throughput for guaranteed correctness and removes dependency on
an unwritten, maintainer-discussion-only guarantee. If CamGuard later needs higher throughput,
revisit with an explicit load test against the actual deployed execution provider (CPU) before
removing the synchronization, since the affirmative evidence, while reasonably strong, is not
formally documented.

## Sources
1. https://github.com/georg-jung/FaceAiSharp — README/issues, no thread-safety statement (accessed 2026-09-14)
2. https://github.com/microsoft/onnxruntime/discussions/10107 — maintainer: sessions thread-safe, Run() concurrent by design (accessed 2026-09-14)
3. https://github.com/microsoft/onnxruntime/discussions/9441 — DirectML EP exception: single-thread-at-a-time per session (accessed 2026-09-14)
4. https://github.com/microsoft/onnxruntime/discussions/15300 — maintainer Pranav Sharma: one session shared across threads is correct and recommended; lock-contention performance caveat (accessed 2026-09-14)
5. https://onnxruntime.ai/docs/performance/tune-performance/threading.html — per-session thread pool config, no explicit thread-safety prose (accessed 2026-09-14)
6. https://github.com/microsoft/onnxruntime/issues/114 — original 2018 community question (accessed 2026-09-14)
7. https://theneuralbase.com/onnx/learn/advanced/multi-model-serving/ — third-party blog contradicting maintainer guidance, no citations (accessed 2026-09-14)
8. https://github.com/microsoft/onnxruntime/issues/26610, https://github.com/microsoft/onnxruntime/issues/22147 — GPU/DirectML-specific crash and feature-request reports (accessed 2026-09-14)
