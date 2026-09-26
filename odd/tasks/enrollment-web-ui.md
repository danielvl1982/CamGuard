# Feature: Enrollment Web UI (Blazor + Tailwind)

> ODD feature document — single source of truth for this feature's life. Every phase/task
> outcome, verification evidence, and decision is logged here automatically as work progresses.

## Objective

Move member registration out of the console-only UX into a web SPA: a Blazor (Interactive
Server) host serving a registration form (multi-photo upload with preview and inline
face-gate feedback) plus a read-only member directory, styled with a dark theme whose zones
are separated by surface-contrast and with consistent `:focus-visible` behavior.

## Problem / Why

Registration today runs through `Enrollment.Console` interactive menu (name + photo paths).
The user wants a step up to a web interface for the registration flow. Study conclusion
(session 2026-09-25): **Blazor Web App (.NET 10, Interactive Server) + Tailwind CSS 4** over
React + Minimal API, because 100% of the registration logic already lives in C# and
`AddEnrollment(IConfiguration)` is a host-agnostic composition root — components inject
`RegisterMemberCommandHandler` directly, no intermediate API layer, single `dotnet`
toolchain. FaceAiSharp/ONNX are native .NET binaries, so the pipeline must stay
server-side regardless of stack (this forced a server host in every option).

## Scope

### In scope
- New host project `src/Enrollment.Web` (`Microsoft.NET.Sdk.Web`, net10.0) using the
  Blazor Web App model with interactive server rendering.
- Register page: `FullName` + multi-photo `InputFile` with previews, client-side checks,
  server call to `RegisterMemberCommandHandler`, inline mapping of `RegisterMemberError`
  (`NoFaceDetected`, `MultipleFacesDetected`, `ExtractionFailed`, `InvalidName`,
  `InvalidMember`).
- Members list page: read-only directory (requires new `ListAllAsync` port method —
  the repository today only supports nearest-match and normalized-name lookup).
- Dark design system: Tailwind 4 `@theme` tokens (surface ladder `base → surface →
  elevated` for zone separation, borders, text, accent, focus ring) + global
  `:focus-visible` ring. UI copy in English.
- Unit tests for the new repository method; existing test suite stays green.

### Out of scope (v1)
- `watch` / `spike` console modes as UI, authentication/authorization, embedding
  similarity search UI, member editing/deletion, React, mobile-first polish.
- The existing `Enrollment.Console` stays untouched as the CLI tool.

## Constraints

- .NET 10, central package management (`Directory.Packages.props`), hexagonal boundaries:
  Domain stays free of ASP.NET/Tailwind dependencies (web project references
  Application + Infrastructure only, like Console does).
- Native ONNX/FaceAiSharp packages must be referenced by the consuming app project
  (same pattern as `Enrollment.Console.csproj`).
- SQLite DB path and `appsettings.json` follow the existing `VectorStoreOptions` wiring.
- Delivery via feature branch + PR (repo convention, see PR history #2–#4).

## TDD & checks

- **TDD mode: OFF** (source: no strict-TDD configuration in project or session; frameworks
  present do not enable it). Ordinary functional checks apply.
- Test runner: `dotnet test`; build: `dotnet build`.
- Per-task verification commands are listed in each task.

## Delivery strategy

- **Strategy: `ask-on-risk`** (default). **Forecast: ~800 authored lines (>400 budget).**
- **Chain strategy: `stacked-to-main`** (user decision, 2026-09-25). Each PR merges to
  main in order; slices mapped per task, keeping each PR under ~400 lines:
  - **PR1** = T1 + T2 (host scaffold + list port) — branch `feat/enrollment-web-ui-pr1`… or
    sequential slices on one branch; exact branch naming decided at first push.
  - **PR2** = T3 (theme + layout)
  - **PR3** = T4 + T5 + T6 (pages + verification)

## Tasks

- [x] **T1 — Scaffold `src/Enrollment.Web` host**
  csproj (Web SDK, project refs Application+Infrastructure, FaceAiSharp/ONNX packages),
  `Program.cs` (WebApplicationBuilder + `AddEnrollment(configuration)` + Blazor interactive
  server wiring), minimal App/Routes/Layout/Home, launchSettings.
  *Acceptance:* `dotnet build` green; `dotnet run` serves the home page (HTTP 200).
  *Route:* delegated writer (writer trigger: 4+ new non-trivial files).
  *Evidence:* commit `27f8d24`. Verified 2026-09-26: `dotnet build src/Enrollment.Web` →
  `Compilación correcta, 0 warnings / 0 errors`; server responds `HTTP 200` on
  `http://localhost:5203` (profile `http`), serves `Home.razor` = "Hello, world!".

- [x] **T2 — `ListAllAsync` repository method**
  Add `Task<IReadOnlyList<Member>> ListAllAsync(CancellationToken)` to
  `IMemberVectorRepository`, implement in `SqliteVecMemberRepository`
  (verify SqliteVec preview enumeration support; fallback: raw SQL via Microsoft.Data.Sqlite
  on the same table), tests in `tests/Enrollment.UnitTests` following
  `FindByFullNameTests` patterns.
  *Acceptance:* `dotnet test` green including new list tests.
  *Route:* delegated writer (writer trigger: 3 non-trivial files: port + impl + tests).
  *Evidence:* 2026-09-26. 3 files: `src/Enrollment.Domain/IMemberVectorRepository.cs`,
  `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs`,
  `tests/Enrollment.UnitTests/ListAllAsyncTests.cs` (new, 5 tests).
  **Raw-SQL fallback NOT needed** — preview `GetAsync(r => true, int.MaxValue, …)` +
  `EnsureCollectionExistsAsync` enumerates fine (probed empirically against
  `CommunityToolkit.VectorData.SqliteVec` 1.0.1-preview).
  **`IncludeVectors: true` is forced**: probe showed `false` yields `Embedding = null` and
  `Member.Rehydrate` requires a validated `FaceEmbedding`, so the port's `IReadOnlyList<Member>`
  return type mandates vector materialization (documented in code; identity-only DTO would be
  a T5+ signature change). Order is storage order → tests assert set membership, not order.
  `dotnet test CamGuard.slnx` → **131 passed / 0 failed** (Enrollment 68 = 63 prior + 5 new;
  Monitoring 63). `dotnet build CamGuard.slnx` → 0 errors (9 pre-existing warnings: NU1902
  OpenTelemetry advisory ×7, CS9057 Moq.AutoMocker analyzer, NU1902 test project).
  *Note:* solution build only goes green after stopping the T1 smoke server (PID 11632) —
  it locks `Enrollment.Web\bin` output (MSB3021/MSB3027). A/B stash check proved the lock was
  environmental, not code.

- [x] **T3 — Tailwind 4 + dark token system + layout zones**
  Dev-only `package.json` in `Enrollment.Web` (`@tailwindcss/cli` build/watch →
  `wwwroot/app.css`), `@theme` tokens (surface ladder, borders, text, accent, focus ring),
  `@source` for `*.razor`, base layout with zones separated by surface contrast, global
  `:focus-visible` ring (never `outline: none` without replacement).
  *Acceptance:* CSS builds; `dotnet build` green; layout renders zone contrast + visible
  focus ring on tab navigation.
  *Route:* delegated writer (writer trigger: 3+ files: package.json, CSS, layout razor).
  *Evidence:* 2026-09-26. 6 paths: `.gitignore`, `src/Enrollment.Web/package.json` +
  `package-lock.json` (dev only, `@tailwindcss/cli ^4.1.0` → v4.3.3),
  `src/Enrollment.Web/Styles/app.css` (input), `src/Enrollment.Web/wwwroot/app.css`
  (committed output, 10,371 bytes), `Components/Layout/MainLayout.razor`.
  Ladder `--color-base #0b0f16` → `--color-surface #151d2a` → `--color-elevated #212c3d`;
  zones header/nav/main/footer = surface/elevated/base/surface (adjacent zones differ by one
  ladder step → contrast-only separation). Global `:focus-visible { outline: 2px solid
  var(--color-focus-ring); outline-offset: 2px }` in `@layer base`; built CSS has
  **0 `outline: none`**. `@import "tailwindcss" source(none)` + `@source "../Components/**/*.razor"`.
  Verified: `npm run build` → `tailwindcss v4.3.3`, `Done in 30ms`, exit 0; output mtime
  12:54:29 ≥ input 12:54:21 (not stale); ladder classes + `:focus-visible` present in output;
  `node_modules` ignored; `dotnet build CamGuard.slnx` → 0 errors / 8 pre-existing warnings.
  *Gotchas:* (a) **Tailwind 4 prunes unused `@theme` vars** — `--color-accent*` /
  `--color-border-strong` are absent from `:root` until a utility uses them, so downstream
  pages must use utility classes, never `var(--color-*)` from `.razor.css` scoped files.
  (b) `#blazor-error-ui` lives in `MainLayout.razor.css` (scoped bundle), not in the
  overwritten `wwwroot/app.css`. (c) `npm run watch` (`--watch=always`) NEVER returns —
  it blocked the writer's foreground shell for 2h16m until the processes were killed; never
  use a watcher as a verification command.

- [ ] **T4 — Register page**
  `Register.razor`: FullName input + multi `InputFile` with per-photo previews and remove,
  client-side validation (non-empty name, ≥1 photo), submit → scope → streams →
  `RegisterMemberCommandHandler.HandleAsync`, map each `RegisterMemberError` to an inline
  message, success confirmation state.
  *Acceptance:* `dotnet build` green; manual smoke registers a member using
  `src/Enrollment.Console/testdata/blank-image.png` (expected: gate error path) and a real
  face photo (expected: success + persisted row).
  *Route:* delegated writer (writer trigger: 2+ non-trivial files).
  *Evidence:* pending.

- [ ] **T5 — Members list page**
  `Members.razor`: read-only table/list from `ListAllAsync` (FullName + MemberId), empty
  state, linked navigation from layout.
  *Acceptance:* `dotnet build` green; smoke shows members registered via T4.
  *Route:* delegated writer (writer trigger: 2 files: page + nav link).
  *Evidence:* pending.

- [ ] **T6 — Full verification & closure**
  `dotnet build` + full `dotnet test`, manual end-to-end smoke (register → list),
  RDD candidate assessment per commit if enabled, final work-unit commit,
  feature-doc evidence refresh.
  *Acceptance:* all checks green or failures recorded honestly.
  *Route:* inline (bounded verification).
  *Evidence:* pending.

## Acceptance criteria (feature-level)

1. `dotnet run --project src/Enrollment.Web` serves a dark-themed UI with visibly distinct
   surface zones and a visible focus ring on every interactive element.
2. Registering via the web form persists the member exactly as the console path does
   (same handler), and gate errors appear inline without losing form state.
3. The members page lists persisted members.
4. `dotnet test` green; Console app behavior unchanged.
5. Each task landed as one work-unit commit on the feature branch (Conventional Commit,
   no AI attribution).

## Route declarations

| Task | Route | Trigger evidence |
|---|---|---|
| T1 | delegated | 4+ new non-trivial files (writer trigger) |
| T2 | delegated | 3 non-trivial files (writer trigger) |
| T3 | delegated | 3+ files (writer trigger) |
| T4 | delegated | 2+ non-trivial files (writer trigger) |
| T5 | delegated | 2 files (writer trigger) |
| T6 | inline | bounded verification (1–3 actions) |

Delegation fallback: if the subagent provider rejects launches (observed once this session:
"OpenCode's free tier can only be used from within OpenCode"), the task continues inline
with disclosure — a client-runtime limitation, not a scope change.

## Progress log

- 2026-09-25 — Feature doc created after stack study (Blazor+Tailwind) and scope decision
  (register + members list). Route: ODD, user preference: document each phase in this file
  automatically. Chain strategy question pending before first commit.
- 2026-09-25 — Chain strategy: `stacked-to-main` chosen by user; delivery `ask-on-risk`
  (forecast ~800 lines > 400 budget). PR slicing per task group above.
- 2026-09-26 — T1 landed as `27f8d24` (host scaffold). Startup smoke: server on :5203,
  HTTP 200. Orphan instance from prior session held the port (`AddressInUseException` on
  relaunch) — kill it before a solution build or a `dotnet run`.
- 2026-09-26 — T2 landed (ListAllAsync): 3 files, 5 new tests, solution suite 131 green.
  Writer returned `partial` only for the bin-dir file lock; stashed A/B proved 0 CS errors.
  RDD is **on (global)** — per-commit assessment runs after each work-unit commit.
- 2026-09-26 — RDD consent for the T1+T2 slice (22 files / 848 lines, `slice_budget_reached`,
  medium): **user chose "Skip this time"** → `declined_this_candidate`, no review record,
  candidate-scoped (not the kill switch). Boundary stays at branch point `f5ba6de`.
- 2026-09-26 — T3 landed (Tailwind toolchain + dark ladder + 4-zone layout), 6 paths.
  **Incident:** the writer ran `npm run watch` (`--watch=always`) as a foreground
  verification command → it never returns, so the sub-agent hung 2h16m (files frozen at
  10:35:47, PIDs 21100/11060 idle since 10:36:23). Fixed by killing both watchers, which
  released the shell. Rule: one-shot `npm run build` only for verification.
  Smoke photo `photos/DSC_9665.jpg` (13 MB) supplied by user; `photos/.gitignore` has its
  patterns **commented out**, so the photo was untracked-but-visible — excluded locally via
  `.git/info/exclude`. Fix `photos/.gitignore` at T6.
