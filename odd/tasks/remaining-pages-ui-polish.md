# Extend UI polish to remaining pages

## Objective
Extend the same responsive/accessible/hover-motion treatment already applied to Home.razor
and Incidents.razor to the rest of the web UI: Members.razor, Register.razor,
MemberDetail.razor, and MainLayout.razor (header/nav).

## Why
Follow-up batch explicitly requested by the user after the Home/Incidents pass: "resto de
páginas (Members/Register/MemberDetail/nav)". Same design language, no new patterns invented.

## Findings (via Explore agent, full file reads)
- Members.razor's member table (`div` at line 49 wrapping the `<table>`) has **no**
  `overflow-x-auto` — the Actions cell (Annul/Cancel/Confirm buttons) can wrap/overflow on a
  375px viewport. This is the only table across these 4 files.
- Register.razor and MemberDetail.razor have no tables — only `<ul>` photo-preview/stored-image
  grids, not horizontally scrollable, so no overflow wrapper needed there.
- Every interactive element across all 4 files (buttons, links, NavLink items) currently uses
  only `transition-colors hover:*` (color-only) — none has the motion-safe translate/shadow
  treatment from Home.razor's cards.
- MainLayout.razor's logo/home link (line 9) has **zero** hover treatment at all (no
  transition/hover classes whatsoever).
- MainLayout.razor's `#blazor-error-ui` banner (lines 46-50, Blazor scaffold boilerplate) has
  no `role="alert"` on its container, and its dismiss control is a plain
  `<span class="dismiss">🗙</span>` — not a `<button>`, no accessible name, not
  keyboard-operable.
- Photo-preview `<li>` cards in Register.razor and MemberDetail.razor (both the stored-images
  list and the "add photos" preview list) are card-like tiles with border+bg but currently no
  hover treatment at all.

## Design decisions (this batch, consistent with the Home/Incidents pass)
1. **Motion-safe hover** (`transition-all duration-150 motion-safe:hover:-translate-y-0.5
   motion-safe:hover:shadow-lg`, respecting `prefers-reduced-motion`) goes on genuine
   card/tile containers only:
   - Register.razor's photo-preview `<li>` cards.
   - MemberDetail.razor's stored-image `<li>` cards AND its "add photos" preview `<li>` cards.
   - Do NOT apply to buttons, pills, nav items, or table rows — those keep color-only
     transitions (or gain one where missing), to keep the same "cards float, controls don't"
     language established on Home.razor.
2. **MainLayout nav items**: add a subtle `motion-safe:hover:scale-[1.03]` (no translate — a
   nav row shouldn't visually shift baseline) alongside their existing color transition.
3. **MainLayout logo/home link**: currently has no hover feedback at all — add
   `transition-transform motion-safe:hover:scale-[1.02]`.
4. **Members.razor table row**: add `transition-colors hover:bg-surface` to the `<tr>` so rows
   read as interactive (they link to member detail) — color-only, matches the "rows don't
   float" rule.
5. **overflow-x-auto**: wrap Members.razor's table container (the div at line 49) the same way
   already done for Incidents.razor and Home's preview table.
6. **Accessibility fix on `#blazor-error-ui`**: add `role="alert"` to the container div, and
   turn the dismiss `<span class="dismiss">` into a real `<button type="button" class="dismiss"
   aria-label="Dismiss">` (keep the exact `dismiss` class so any existing click-handler
   selector in Blazor's framework script keeps working — do not rename the class).
7. Everything else (buttons, badges, existing `role="status"`/`role="alert"` states) is
   already correct — leave untouched to avoid unrelated churn.

## Out of scope
- No new accessibility audit beyond the two concrete gaps found (error-UI banner + Members
  table hover). A full audit was explicitly deferred to a later batch.
- No restyling of buttons/pills — only the fixes listed above.

## Tasks
- [x] T1 — Members.razor: `overflow-x-auto` on table wrapper + row hover (`hover:bg-surface`)
- [x] T2 — Register.razor: motion-safe hover on photo-preview `<li>` cards
- [x] T3 — MemberDetail.razor: motion-safe hover on stored-image + add-photos preview `<li>` cards
- [x] T4 — MainLayout.razor: nav item scale hover, logo hover, `#blazor-error-ui` a11y fix
- [x] T5 — Tailwind rebuild (`npm run build`) + `dotnet build` sanity check

## Verification evidence

- `npm run build` (in `src\Enrollment.Web`): **success**.
  ```
  > enrollment-web@1.0.0 build
  > tailwindcss -i ./Styles/app.css -o ./wwwroot/app.css

  ≈ tailwindcss v4.3.3
  Done in 55ms
  ```
  `node_modules` was already present, so `npm install` was not needed.
- `dotnet build src\Enrollment.Web\Enrollment.Web.csproj` (from repo root): **success — Build succeeded, 0 Error(s), 12 Warning(s)**.
  All 12 warnings are the pre-existing `NU1900` "Unable to load the service index for source
  https://pkgs.dev.azure.com/gaa-devs/_packaging/Semicrol.GAA.NuGet/nuget/v3/index.json" notices
  (unreachable private NuGet feed) — unrelated to this change, expected per task instructions.
  No compiler errors or warnings from the edited `.razor` files.

## Delivery
Second work-unit commit on the existing `feat/home-incidents-ui-polish` branch (same ongoing
UI-polish initiative, not yet delivered/merged) — Conventional Commit message.

## TDD mode
Off / not applicable — same reasoning as the prior batch (no Razor/bUnit test harness exists).
