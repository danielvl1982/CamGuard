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

- [ ] **T1 — Scaffold `src/Enrollment.Web` host**
  csproj (Web SDK, project refs Application+Infrastructure, FaceAiSharp/ONNX packages),
  `Program.cs` (WebApplicationBuilder + `AddEnrollment(configuration)` + Blazor interactive
  server wiring), minimal App/Routes/Layout/Home, launchSettings.
  *Acceptance:* `dotnet build` green; `dotnet run` serves the home page (HTTP 200).
  *Route:* delegated writer (writer trigger: 4+ new non-trivial files).
  *Evidence:* pending.

- [ ] **T2 — `ListAllAsync` repository method**
  Add `Task<IReadOnlyList<Member>> ListAllAsync(CancellationToken)` to
  `IMemberVectorRepository`, implement in `SqliteVecMemberRepository`
  (verify SqliteVec preview enumeration support; fallback: raw SQL via Microsoft.Data.Sqlite
  on the same table), tests in `tests/Enrollment.UnitTests` following `FindByFullNameTests`
  patterns.
  *Acceptance:* `dotnet test` green including new list tests.
  *Route:* delegated writer (writer trigger: 3 non-trivial files: port + impl + tests).
  *Evidence:* pending.

- [ ] **T3 — Tailwind 4 + dark token system + layout zones**
  Dev-only `package.json` in `Enrollment.Web` (`@tailwindcss/cli` build/watch →
  `wwwroot/app.css`), `@theme` tokens (surface ladder, borders, text, accent, focus ring),
  `@source` for `*.razor`, base layout with zones separated by surface contrast, global
  `:focus-visible` ring (never `outline: none` without replacement).
  *Acceptance:* CSS builds; `dotnet build` green; layout renders zone contrast + visible
  focus ring on tab navigation.
  *Route:* delegated writer (writer trigger: 3+ files: package.json, CSS, layout razor).
  *Evidence:* pending.

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
