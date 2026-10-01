# Feature: members-roles-layout

Design adjustment requested by the user (2026-10-01): make `/members` follow the same grouped layout as `/roles`, and simplify navigation and Home accordingly.

## Objective
`/members` groups a "New member" form and the members list on one page (as `/roles` groups "New role" and the roles list). The separate Register page and its navigation disappear. Home keeps only the Members and Roles cards plus the recent incidents list.

## Scope
1. **Members page.** Move the registration form from `Register.razor` into `Members.razor` as a "New member" panel above the list. After a successful registration the list refreshes on the same page.
2. **Remove Register.** Delete the `/register` page and its nav link in `MainLayout.razor`; fix every link to `/register` (Home, `MemberDetail.razor`).
3. **Home.** Cards: Members and Roles only (no Register card, no "Enroll a face" hero button). Remove the roles preview added in PR #15; keep the recent incidents list.

## Out of scope
- Domain/application changes (`RegisterMemberCommand` and handlers stay as they are).
- Redirect from the old `/register` URL (internal admin UI; the route is simply removed).

## Constraints
- English artifacts, conventional commits without AI attribution, feature branch + PR.
- TDD off (no component-test framework in the repo, no bUnit); ordinary functional checks: `dotnet build CamGuard.slnx`, full `dotnet test`, Playwright browser smoke.
- Delivery strategy: `ask-on-risk`, expected single PR; revisit if the running count passes ~400 authored changed lines.

## Tasks
- [x] T1 Home: drop Register card and hero button, drop the roles preview section, keep Members + Roles cards and incidents. Route: inline (one understood file). Commits bc531fa, 16770b9 (the second drops the hero CTA that native review flagged as a BLOCKER on the isolated T1 range).
- [x] T2 Members page absorbs the registration form; delete `Register.razor`; remove the nav link; fix `/register` links in `MemberDetail.razor`. Route: delegated writer (2+ non-trivial files, reading prepares the write). Writer observed: web project builds with 0 errors/0 warnings (full solution build blocked by the user's running Enrollment.Web locking DLLs, so an alternate OutDir was used); Enrollment 186 + Monitoring 86 tests green; no `/register` leftovers in src/tests.
- [ ] T3 Verification: build, full tests, browser smoke (nav, Home, register from `/members`, list refresh, member detail links).

## Acceptance
1. `/members` shows "New member" and the members list on one page; registering a member updates the list without navigating away.
2. No `/register` route and no Register link anywhere in the UI.
3. Home shows Members and Roles cards and the incidents list, with no roles list.
4. Build 0 errors, tests green, browser smoke passes.

## Progress log
- 2026-10-01 — Feature doc created from the user's request; branch `feat/members-page-grouping`.
- 2026-10-01 — T1 native review (medium): reliability lens found one BLOCKER on the isolated T1 range (hero CTA left pointing to /members before T2 existed). Corrected in 16770b9; the post-correction STATUS then stopped with `captured_artifacts_unverifiable` (terminal), so that lineage was not acknowledged. Delivery follows ordinary policy.
- 2026-10-01 — T2 landed (Members.razor absorbs the form, Register.razor and nav link removed). Pending: T3 browser smoke (user's running app holds old code).
