# Research: ai-access-control

Verifies three load-bearing architectural claims from `exploration.md` (Engram obs #1306) against current, dated sources. All sources accessed 2026-09-14.

## Lane 1 — Azure AI Vision Face API access model (identify/verify gating)

**Conclusion: CONFIRMED, with an important refinement.** Face **identification** and **verification** are Limited Access, requiring registration via the Face Recognition intake form, and are restricted to customers/partners "working directly with Microsoft account teams" (i.e., not self-serve even after form approval) — plus Standard (S0)/Enterprise (E0) tier only, not Free (F0). **Face Detection is explicitly available without registration.** Liveness detection is separately gated (own intake requirement).

**Confidence: high.**

Citations:
- https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/computer-vision/limited-access-identity (ms.date 2025-10-15, updated 2026-06-05) — "Customers and partners who wish to use Limited Access features of the Face API, including Face identification and Face verification, are required to register... The Face Detection operation is available without registration." Also: "Face API is available only to customers managed by Microsoft, defined as those customers and partners who are working directly with Microsoft account teams."
- https://learn.microsoft.com/en-us/azure/ai-services/face/overview-identity (ms.date 2026-03-23, updated 2026-06-05) — top-of-page caution: "Face service access is limited based on eligibility and usage criteria... Use the Face Recognition intake form to apply for access."

**Impact on exploration's recommendation:** this fully validates choosing FaceAiSharp/local ONNX over Azure Face for identify/verify. It also surfaces a stronger reason than "gated behind a form": even an approved registration is scoped to Microsoft-managed accounts with a direct account-team relationship — a bar most greenfield/independent projects (like CamGuard) cannot clear at all, regardless of use-case legitimacy. This should be stated explicitly in the design doc as the rationale, not just "Limited Access."

## Lane 2 — FaceAiSharp

**Conclusion: CONFIRMED as viable**, with one version-support caveat to flag as a risk, not a blocker.

- Package: `FaceAiSharp` (core library), current version **0.6.35** (released 2026-05-29). Model packages ship separately, e.g. `FaceAiSharp.Models.ArcFace.LResNet100E-IR` (ArcFace embedding model from the ONNX Model Zoo) and `FaceAiSharp.Models.Scrfd.2dot5g_kps_640x640` (SCRFD face detector), or bundled via `FaceAiSharp.Bundle` 0.5.23.
- License: **MIT**.
- Target frameworks: net8.0 and **net10.0** listed as directly compatible; net9.0 shown as "computed" (compatible via net8.0 target, not an explicit dedicated build) — worth a quick local `dotnet restore` smoke test on net10 before committing, but no reported blocker.
- Key dependency: `Microsoft.ML.OnnxRuntime.Managed >= 1.26.0`, plus `SixLabors.ImageSharp >= 3.1.12`, `System.Numerics.Tensors >= 10.0.8`. No conflicting/pinned OnnxRuntime version found that would collide with other packages in this project (none yet declared).

**Confidence: high** on package/license/models; **medium** on net10 "first-class" support since NuGet's target-framework compatibility matrix shows net9 as computed rather than explicit — recommend a quick spike before relying on it.

Citations:
- https://www.nuget.org/packages/FaceAiSharp/ — version 0.6.35, MIT license, dependency list, target framework compatibility table.
- https://www.nuget.org/packages/FaceAiSharp.Bundle/ — "installs FaceAiSharp's managed code and multiple AI models in the ONNX format... all processing is done locally, with no reliance on cloud services."
- https://www.nuget.org/packages/FaceAiSharp.Models.ArcFace.LResNet100E-IR — confirms ArcFace model source is the official ONNX Model Zoo.

## Lane 3 — Microsoft.Extensions.VectorData (SQLite connector)

**Conclusion: CONFIRMED with a naming correction to the exploration's assumption.** A SQLite connector exists and is usable from a plain .NET console/worker app with no external server — but the package family has moved: it now ships as `CommunityToolkit.VectorData.SqliteVec` (**not** `Microsoft.SemanticKernel.Connectors.SqliteVec`, which is the older Semantic-Kernel-branded package and appears to be superseded/parallel for this purpose).

- `CommunityToolkit.VectorData.SqliteVec`, current version **1.0.1-preview** (still preview, not GA). Dependencies: `Microsoft.Data.Sqlite (>=10.0.9)`, `Microsoft.Extensions.VectorData.Abstractions (>=10.7.0)`, `SQLitePCLRaw.bundle_e_sqlite3`, and the `sqlite-vec` NuGet package (the native `sqlite-vec` extension, distributed as a NuGet-packaged native binary — no separate service/infra to install or run). Supports net8.0/net10.0/netstandard2.0/net462+.
- The official `Microsoft.Extensions.VectorData` docs page's current example code and provider-package guidance point to the `CommunityToolkit.VectorData.*` namespace (e.g., `CommunityToolkit.VectorData.InMemory`, `CommunityToolkit.VectorData.AzureAISearch`) as the current pattern, confirming the CommunityToolkit branding is the maintained one going forward, not just a rename in isolation.

**Confidence: high** that a usable, infra-free SQLite vector store exists; **medium-low** on production-readiness given it is still `-preview` versioned — flag as a risk (API surface could still change before GA).

Citations:
- https://learn.microsoft.com/en-us/dotnet/ai/vector-stores/overview (ms.date 2026-02-28, updated 2026-07-23) — current code sample uses `CommunityToolkit.VectorData.InMemory`; provider-package guidance references `CommunityToolkit.VectorData.AzureAISearch` as the pattern for provider packages.
- https://www.nuget.org/packages/CommunityToolkit.VectorData.SqliteVec — version 1.0.1-preview, "SQLite provider (with sqlite-vec extension) for Microsoft.Extensions.VectorData, by the .NET Community Toolkit," dependency list confirming self-contained NuGet-based native extension (no external infra).

**Correction to exploration.md:** the prior phase should not assume the package name is under the `Microsoft.SemanticKernel.Connectors.*` namespace for new work — use `CommunityToolkit.VectorData.SqliteVec`, and note it is preview-quality, not GA.

## Lane 4 — Alternative local face-embedding libraries

**Conclusion: a credible alternative exists** — **FaceONNX** (github.com/FaceONNX/FaceONNX), a NuGet-installable .NET library described as "face recognition and analytics library based on deep neural networks and ONNX runtime." It is a more direct ONNX Runtime wrapper without going through FaceAiSharp's model-bundle abstraction.

Trade-off: could **not** fully verify FaceONNX's current maintenance activity, license terms, or exact model provenance (ArcFace vs. a different embedding architecture) with a dated, authoritative source in the time available — this is a **gap**, flagged below rather than asserted. FaceAiSharp remains the better-verified choice given lane 2's confirmed MIT license, explicit ArcFace/ONNX-Model-Zoo provenance, and net10 listed compatibility.

**Confidence: low** on FaceONNX specifics (unverified); **medium** that it's a real, existing alternative worth a spike only if FaceAiSharp's net10 support spike (lane 2) fails.

Citations:
- https://github.com/FaceONNX/FaceONNX — repository exists and is described as ONNX-runtime-based face recognition/analytics for .NET (title/description only verified, not deep-dived).

## Lane 5 — FileSystemWatcher reliability

**Conclusion: CONFIRMED**, exactly as commonly known, and now cited to the official source instead of asserted from memory.

- The official remarks explicitly warn: "If there are many changes in a short time, the buffer can overflow. This causes the component to lose track of changes in the directory, and it will only provide blanket notification." Recommended mitigations: use `NotifyFilter`/`IncludeSubdirectories` to reduce noise, increase `InternalBufferSize` (costly — non-paged memory), avoid long file names, and keep event-handler code short. An `Error` event fires "when the internal buffer overflows," so subscribing to it is the supported way to detect (not prevent) loss.
- No explicit polling-fallback recommendation appears in the current API reference remarks (contrary to the summarized assumption) — the docs recommend *filtering/tuning*, not necessarily a polling fallback; a polling safety-net is still good practice but is a design choice, not a documented Microsoft recommendation. Flag this as a minor correction to how the design doc should phrase the citation.

**Confidence: high.**

Citations:
- https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher (net-10.0 moniker, api docs updated 2026-07-01) — Remarks section, "Windows operating system notifies your component of file changes in a buffer created by the FileSystemWatcher... the buffer can overflow," plus "Events and buffer sizes" section listing mitigation guidance and the `Error` event description ("Occurs when the instance of FileSystemWatcher is unable to continue monitoring changes or when the internal buffer overflows").

## Gaps (not verified — do not treat as fact)

1. FaceONNX's exact license, maintenance cadence, and underlying model architecture were not verified with a dated authoritative source (lane 4).
2. Whether FaceAiSharp's net10.0 listed compatibility is a fully tested first-class target or only computed/inherited from net8.0 was not resolved beyond NuGet's compatibility matrix — recommend a local spike, not further web research.
3. `CommunityToolkit.VectorData.SqliteVec`'s preview status means its API could change before this project ships; no GA date was found.
4. Did not verify whether `Microsoft.SemanticKernel.Connectors.SqliteVec` (the older package name found in initial search) is formally deprecated/superseded or simply parallel/legacy — only that current official docs point to the CommunityToolkit namespace as the maintained pattern.

## Risks

- Building on a `-preview` vector-store package (`CommunityToolkit.VectorData.SqliteVec` 1.0.1-preview) risks a breaking API change before GA; pin the exact version and re-check before release.
- FaceAiSharp's net10 support should be spiked early (small console harness) before deeper investment, given the "computed" (not explicit) compatibility signal for net9/net10 on NuGet.
- Azure Face Limited Access registration is gated not just by form approval but by a Microsoft account-team relationship — this is not a "wait for approval" risk, it's effectively a hard no for this project; do not budget time hoping for future access.
