# Feature: Site chrome (header + footer + home)

> ODD feature document — single source of truth for this feature's life. Every phase/task
> outcome, verification evidence, and decision is logged here automatically as work progresses.

## Objective

Give CamGuard a presentable face: a refined header with a minimalist CamGuard logo,
a proper footer with the typical legal notice + project ownership, and a real home page
replacing the Blazor default placeholder.

## Problem / Why

The site chrome is still the Blazor template default: Home renders "Hello, world! /
Welcome to your new app", the header is a bare text link, and the footer is a one-line
"CamGuard Enrollment portal." — no brand, no ownership, no finished feel (session
2026-09-27, user request in Spanish, UI copy stays in English).

## Scope

### In scope
- Header: improved design within the existing zone system + minimalist CamGuard logo
  (inline SVG or `wwwroot` asset, no new packages, no emoji).
- Footer: typical notice (© year, All rights reserved) + project ownership
  (CamGuard enrollment app).
- Home (`/`): real landing content replacing the placeholder — brand, what the app
  does, links to Register / Members.
- Active nav state if cheap (NavLink); otherwise keep anchors.

### Out of scope (v1)
- Full landing-page marketing design, docs pages, privacy/terms pages (footer links
  to them only if they exist — no dead links).
- Light theme, responsive overhaul beyond what the token system already gives.
- Favicon (nice-to-have only if trivial via the same SVG).

## Constraints

- .NET 10, Blazor Server. Stay inside the T3 token system: surface ladder
  (`bg-base`/`bg-surface`/`bg-elevated`), `text-text`/`text-text-muted`,
  `border-border`, `accent` — separation by contrast steps, not new hues.
- UI copy in English.
- Tailwind: one-shot `npm run build` after introducing new utility classes — NEVER
  use a watcher as a verification command.
- Pre-push hook blocks direct pushes to `main` (repo policy; bypass only on explicit
  user order, precedent: two prior features).

## TDD & checks

- **TDD mode: OFF.** Design work — ordinary checks apply.
- Test runner: `dotnet test`; build: `dotnet build CamGuard.slnx`.
- Per-task verification commands are listed in each task.

## Delivery strategy

- Small feature, single work unit expected (<400 lines): implement on
  `feat/site-chrome`, merge to `main` on user order after browser smoke.

## Tasks

- [x] **T1 — Header + minimalist logo — DONE**
  Refine the header block in `MainLayout.razor` (spacing, hierarchy) + minimalist
  CamGuard logo (camera/shield geometric SVG, currentColor/accent-driven, readable
  at 16–32px). Brand link keeps `/` navigation.
  *Acceptance:* build green; header shows logo + brand on every page.
  *Route:* delegated (writer, part of single-writer task batch).
  *Evidence:* 2026-09-27. Inline shield+lens SVG (28px, `text-text` + `text-accent`
  lens), two-line brand (CamGuard + tagline), anchors → `NavLink` with
  `ActiveClass="bg-surface text-text"`. Verified: build 0 errors.
  Parent spot-check: diff readback + `border-border-strong` token verified in
  `Styles/app.css` source AND built `wwwroot/app.css`.

- [x] **T2 — Footer with notice + ownership — DONE**
  Footer block: `© <year> CamGuard` + "All rights reserved." + ownership line
  (enrollment / face-recognition access app). No dead links.
  *Acceptance:* build green; footer renders on every page.
  *Route:* delegated (same writer).
  *Evidence:* 2026-09-27. `© @DateTime.Now.Year CamGuard. All rights reserved.` +
  ownership line, no links at all (nothing dead). Same commit as T1.

- [x] **T3 — Home landing content — DONE**
  Replace placeholder in `Home.razor`: headline, one-line description (face-enrollment
  for access control), cards/links to Register and Members. Existing CSS classes
  preferred; Tailwind rebuild only if new utilities are introduced.
  *Acceptance:* build green; `/` shows branded landing, no "Hello, world!".
  *Route:* delegated (same writer).
  *Evidence:* 2026-09-27. Hero (headline + tagline + accent/secondary CTAs) +
  two cards (Register/Members). One-shot `npm run build` (tailwindcss v4.3.3,
  51ms, no watcher); `wwwroot/app.css` +45 lines. Enrollment tests 94/94.
  *Incident:* first build failed with MSB3021/MSB3027 file locks from the stale
  `Enrollment.Web.exe` (PID 12484, running since 18:27) — writer stopped it,
  rebuild green. Parent must restart the app before browser smoke.

- [x] **T4 — Verification & closure — DONE 2026-09-30**
  `dotnet build` + `dotnet test` (full), browser smoke (user), final commit(s),
  feature-doc evidence refresh.
  *Acceptance:* all checks green or failures recorded honestly.
  *Route:* inline (bounded verification).
  *Evidence:* build 0 errors (10 warnings, pre-existing Moq.AutoMocker analyzer
  CS9057); Monitoring 86/86, Enrollment 186/186; browser smoke done by the user
  (reported 2026-09-30).

## Acceptance criteria (feature-level)

1. Every page shows the new header (logo + brand) and footer (© + rights + ownership).
2. `/` is a branded landing with working links to Register and Members.
3. `dotnet build` 0 errors; `dotnet test` green; no new warnings introduced.
4. Work lands as work-unit commit(s) on the feature branch (Conventional Commit,
   no AI attribution); merge + push only on user order.

## Route declarations

| Task | Route | Trigger evidence |
|---|---|---|
| T1+T2+T3 | delegated | 2+ non-trivial files (writer trigger), single writer batch |
| T4 | inline | bounded verification |

## Progress log

- 2026-09-27 — Feature doc created from user request (header/footer/logo/home).
  Baseline: `Home.razor` is the untouched Blazor placeholder; header/footer are
  single-block text in `MainLayout.razor`. Branch `feat/site-chrome` next.
- 2026-09-27 — T1+T2+T3 landed in one writer batch (MainLayout + Home + rebuilt
  app.css). Verified: build 0 errors, Enrollment 94/94. Parent spot-check: build
  0 errors + full diff readback + token check. Stale server PID 12484 stopped by
  writer; parent restarts before smoke.
- 2026-09-30 — T4 closed: build 0 errors, Monitoring 86/86, Enrollment 186/186,
  browser smoke confirmed by the user.
