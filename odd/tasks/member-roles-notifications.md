# Feature: member-roles-notifications

**Status: delivered to `main` (PR #10, merge commit 5db4dbb, 2026-09-29).** All five tasks done, reviewed and merged. Feature branches deleted.

## Objective
Allow renaming a member, and assign each member a "family" role. A role defines which notification channels (push, email, none) are used when that member is recognized. Roles are created and managed from the UI. Denied/unknown accesses use a default channel set, also configurable from the UI.

## Decisions
User-confirmed:
- Recognized member -> channels of the member's role (a role may have no channels = no notification).
- Denied / unknown -> default channel set (stored in DB, editable in UI).
- Delivery strategy `feature-branch-chain`.

Made by the implementation, not explicitly confirmed by the user (open assumptions):
- Recipients stay global (ntfy topic, SMTP To address from config); roles only select channels.
- A recognized member with NO role uses the default channels (keeps today's behavior on upgrade).
- Nothing stored for the default set -> both channels; an explicitly stored empty set -> no notification.
- Rename rejects a name already held by another member (annulled included); same-name is a no-op success.
- "Recognized" = `Outcome == Allowed` with a `MemberId`; `Denied` and `HumanVerificationRequired` always use the defaults (their `MemberId` is only the nearest match).
- Lookup failure falls back to the defaults, then to every channel (a security alert is never silently dropped).

## Constraints
- Same SQLite file; plain-SQL tables + probe-and-ALTER pattern.
- Monitoring reaches Enrollment only via `Enrollment.PublicApi`.
- Artifacts in English. Conventional commits, no AI attribution. Push to `main` blocked by hook; feature branch + PR.
- Tests: xunit + Moq.AutoMock, net10.0. TDD off (source: openspec/config.yaml). Runner: `dotnet test`.

## Tasks
- [x] T1 Rename member (`Member.Rename`, `RenameAsync`, `RenameMemberCommand`, inline UI on MemberDetail). PR #5.
- [x] T2 Roles persistence + domain (`Role`, `NotificationChannel`, tables `roles` / `role_channels`, nullable `members.RoleId` with in-place upgrade, create/update/delete/list/assign use cases). Single `UpdateRoleCommand` (name + channels). Annulled members count as holding a role when deleting. PR #6.
- [x] T3 Default notification channels (`notification_default_channels` + marker row in `notification_settings`, get/set use cases). PR #7.
- [x] T4 Monitoring routing (`INotificationRoutingReader` in `Enrollment.PublicApi`, `CompositeNotificationService` routes per alert; scoped, no captive dependency). PR #8.
- [x] T5 UI (`/roles` page, default-channels card, role selector on MemberDetail, Role column on Members). PR #9.

Route for every task: delegated writer; T5 also had a separate browser verifier. Delivery: PRs #5 to #9 merged in order into `feat/member-roles-notifications`, then PR #10 into `main`; the integration tree matched the T5 tree exactly before the final PR.

## Acceptance
- [x] Member can be renamed (blank rejected, duplicate rejected).
- [x] Roles CRUD from UI; member can be assigned/unassigned a role.
- [x] Recognized alert goes only through role channels; denied through default channels (unit-tested).
- [x] Existing DBs upgrade without data loss (schema-upgrade test on a members table without `RoleId`).
- [x] `dotnet test` green on `main` after the merge: Enrollment 170, Monitoring 85, 0 failed.

## Verification evidence
- Tests per slice: T1 Enrollment 118; T2 Enrollment 157 + Monitoring 76; T3 165 + 76; T4 170 + 85; T5 170 + 85 (all 0 failed). Final `main`: 170 + 85.
- Real-browser check of T5 (Playwright, scratch database, a face image from outside the repo): role create/validate/edit/delete-with-confirm, default channels (never stored = both, stored empty is distinct), assign/unassign role, role-in-use error, Members Role column, T1 rename regression: all passed, no console errors.
- **Not verified:** the routing has never been exercised against a real ntfy or SMTP host; wiring is covered only by unit tests.

## Review record (native review, `review-reliability` lens, reviewed per slice against the last reviewed boundary)
- T1: approved (491 lines, medium). Lineage review-ddfd60d0cafe0b3f.
- T2: approved (1394 lines, medium; above the ~400 heuristic because domain, persistence, use cases and tests are one coherent behavior). Lineage review-105dc9fc42f692dc.
- T3 + T4: approved together (780 lines, medium; T3 alone was `under_budget`). Lineage review-fd7fac9c2f7db053.
- T5: first round ended `escalated` (finding R3-1 with unknown causality, terminal, no correction offered). A refuter-corroborated finding pointed at an inconsistent label for a dangling role (dash on the list, "unknown role" on the detail page), fixed in ba04e2f. The second round then opened `correction_required` with a real BLOCKER, R3-1: a missing space in `href="/incidents"class=` in `MainLayout.razor`, introduced by the T5 commit and not caught by the browser check because HTML parsers tolerate it. One bounded correction (2 lines, cd5c737), targeted validation approved. Lineage review-63e672b18ec32d1b.
- The stop hook repeatedly offered a START for `main..HEAD` (the accumulated branch). It was deliberately not run: every slice was already reviewed against its own base, and the candidate is a slice, never the accumulated branch. No consent was asked and no record was created for it. A consent granted on one such candidate (target ed569cdb...) was never executed and became invalid; it is not a review record.

## Follow-ups (non-blocking, not scheduled)
- Concurrent role create/update with the same name can surface a raw `SqliteException` on the UNIQUE name (handlers check-then-act); delete-role can race with a concurrent assignment and leave a dangling `RoleId`; member rename uniqueness is also check-then-act.
- Missing tests: `Allowed` with a null `MemberId` in the composite, the `Guid.Empty` branch of `NotificationRoutingReader`, reject-then-unchanged against a previously stored custom default set, an automated test for the rename UI, coverage for `Roles.razor`, and a role-preservation test for rename.
- `Role` does not validate enum values in its channels; `SqliteVecMemberRepository.RenameAsync` has a dead `Rename` failure branch.
- Monitoring hard-depends on `INotificationRoutingReader` from `AddEnrollment`; a host that omits it fails with an opaque DI error on the first notification.
- Enter/Escape are not implemented in the `/roles` inputs (MemberDetail rename has them).
- Smoke test the routing against a real ntfy topic and SMTP server.

## Next step
None for this feature. Pick up the follow-ups above only if the user asks.
