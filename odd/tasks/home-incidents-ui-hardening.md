# Home incidents preview + UI hardening pass

## Objective
Bring the Blazor web UI (Enrollment.Web) up to a presentable v1 design pass: confirm/harden
against injection, fix mobile responsiveness gaps, embed a filterable incidents preview on
Home below the existing actions, and add accessible hover micro-interactions.

## Problem / why
User request: interface is functional, time to apply web best practices (anti-injection,
responsive handling) and give the design a first pass — without over-complicating it. Also
wants the incidents list surfaced on Home (below current actions) with filtering, and small
hover-driven size transitions on sections for polish.

## Findings (exploration, via Explore agent + direct reads)
- No injection risk found: zero `MarkupString`/`innerHTML`/`eval`/`IJSRuntime` usage anywhere
  in the repo; all Razor interpolation is auto-encoded. `SqliteIncidentRepository` uses
  parameterized `SqliteParameter`s for every user-supplied value; only fixed
  config/hardcoded identifiers (table/column names) are interpolated into SQL, never user data.
  → No code-level injection fix needed; this task adds defense-in-depth (input length cap)
  only, not a vulnerability fix.
- Responsive gap: `Incidents.razor`'s table has no horizontal-scroll wrapper — overflows on
  narrow viewports. Only one Tailwind breakpoint compiled in (`sm:` = 40rem).
- No hover scale/transform pattern exists yet — only `transition-colors` on borders/bg.
- Tailwind v4 CLI build pipeline exists (`npm run build` in `src/Enrollment.Web`, source at
  `Styles/app.css` → compiled `wwwroot/app.css`); new utility classes need a rebuild to appear.
- `Incident` model has no member-name or confidence field, only `MemberId` (Guid) — filtering
  by member would need a name-resolution join. Scoped out to keep this pass uncomplicated.
- `IIncidentRepository.ListRecentAsync(limit, ct)` is the only list method, no server-side
  filter params — client-side filtering (already-fetched recent list) is sufficient at this
  volume and avoids a repository/schema change.

## Scope (this batch)
1. Home.razor: add an interactive "Recent incidents" preview section below the existing
   "Get started" cards — Kind filter dropdown + note text search (client-side, over a
   `ListRecentAsync` fetch), loading/empty/error states matching `Incidents.razor`'s
   accessibility pattern (`role="status"`/`role="alert"`), link to `/incidents` for the full list.
2. Accessible hover micro-interactions: `motion-safe:`-gated subtle scale/translate transitions
   on the Home action cards and the new incidents preview card, respecting
   `prefers-reduced-motion`.
3. Responsive fix: wrap incident tables (Home preview + `Incidents.razor`) in `overflow-x-auto`
   so they scroll instead of breaking layout on narrow viewports.
4. Defense-in-depth: add a `maxlength` on the incident Note textarea (currently unbounded)
   in both the existing report form and any new input, matching a sane cap.
5. Tailwind rebuild (`npm run build`) so new utility classes land in the committed
   `wwwroot/app.css`.

## Out of scope (explicitly deferred — "seguimos mejorando después")
- Filtering by member name/date range (needs a repository change).
- Nav/layout redesign beyond what's touched here.
- Any backend/API hardening beyond the Note maxlength.

## Constraints
- Keep the design simple ("sin complicaciones") — reuse existing design tokens/CSS variables,
  no new component libraries.
- No existing Razor/bUnit test project for Enrollment.Web (`tests/Enrollment.UnitTests` and
  `tests/Monitoring.UnitTests` only) — TDD not applicable here; verification is build +
  manual/documented check, no fabricated test evidence.
- RDD/native review: follow whatever `gentle-ai review mode status` reports; do not toggle it.

## Tasks
- [x] T1 — Add incidents preview section to Home.razor (rendermode, DI, filter, states, link to /incidents)
- [x] T2 — Accessible hover micro-interactions on Home cards + new preview card (motion-safe)
- [x] T3 — overflow-x-auto wrap on incident tables (Home + Incidents.razor) for mobile
- [x] T4 — Note textarea maxlength (report form + any new input) as defense-in-depth
- [x] T5 — Tailwind rebuild (`npm run build`) + `dotnet build` sanity check

## Verification evidence
- `npm install && npm run build` in `src/Enrollment.Web` — succeeded, compiled
  `wwwroot/app.css` now contains `motion-safe`, `translate-y`, `overflow-x-auto`, `shadow-lg`
  utilities (7 occurrences confirmed via grep). `package-lock.json` churn from a newer local
  npm was reverted (unrelated to this change).
- `dotnet build src/Enrollment.Web/Enrollment.Web.csproj` — Build succeeded, 0 errors (only
  pre-existing NU1900 warnings from an unreachable private NuGet feed, unrelated).
- Ran `dotnet run` for Enrollment.Web locally (`http://127.0.0.1:5299`) and curled `/` and
  `/incidents`: both return HTTP 200. `/` HTML contains the new `aria-label="Recent incidents"`
  section, `All kinds`/`Search note` filter controls. No local `incidents.db` existed in this
  ad-hoc run (`SQLite Error 14: unable to open database file`), which correctly surfaced as the
  existing friendly error state ("The incident list could not be loaded…") instead of a crash —
  confirms the try/catch/error-UI path added to Home.razor degrades gracefully, mirroring
  Incidents.razor's existing behavior.
- Not yet checked with real incident data or in an actual browser (no visual/DOM screenshot,
  no manual hover/filter interaction, no narrow-viewport check) — only HTTP-level and
  markup-level verification was done in this session.

## Acceptance criteria
- Home page renders the two existing action sections unchanged, plus a new incidents preview
  below them, filterable by Kind and note text, with visible loading/empty/error states.
- Hover interactions are subtle, only apply under `prefers-reduced-motion: no-preference`.
- No horizontal page overflow on a narrow (375px) viewport for either page's incident table.
- `dotnet build` succeeds; `npm run build` regenerates `wwwroot/app.css` with the new utilities.

## Delivery
- Single work-unit commit on a feature branch (`feat/home-incidents-ui-polish` or similar),
  Conventional Commit message. Small scope, single-PR strategy — no chaining needed.

## TDD mode
Off / not applicable — no Razor component test harness exists in this repo; verification is
build success + documented manual check of the rendered page.
