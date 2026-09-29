# Feature: member-roles-notifications

## Objective
Allow renaming a member, and assign each member a "family" role. A role defines which notification channels (push, email, none) are used when that member is recognized. Roles are created and managed from the UI. Denied/unknown accesses use a default channel set, also configurable from the UI.

## Decisions (user-confirmed)
- Recognized member -> channels of the member's role (role may have no channels = no notification).
- Denied / unknown -> default channel set (stored in DB, editable in UI).
- Recipients stay global (ntfy topic, SMTP To address from config); roles only select channels. (Assumption, not yet confirmed.)
- No default row stored -> both channels (preserves current behavior).

## Constraints
- Same SQLite file; plain-SQL tables + probe-and-ALTER pattern (see SqliteIncidentRepository, SqliteVecMemberRepository.EnsureAnnulledColumnAsync).
- Monitoring reaches Enrollment only via Enrollment.PublicApi.
- Artifacts in English. Conventional commits, no AI attribution. Push to main blocked by hook; feature branch + PR.
- Tests: xunit + Moq.AutoMock, net10.0. TDD: off (source: openspec/config.yaml). Runner: `dotnet test`.
- Delivery strategy: ask-on-risk, chain strategy chosen by user: `feature-branch-chain`. Integration branch `feat/member-roles-notifications`; one slice branch per task (`feat/member-roles-tN-<name>`), each PR targets the integration branch; the integration branch enters `main` once. Nothing pushed yet.

## Tasks
- [x] T1 Rename member: `Member.Rename`, repository update, `RenameMemberCommand` + handler, UI on MemberDetail. Route: delegated writer (2+ files). Commit 2b0de8b on `feat/member-roles-t1-rename`. Rename rejects a name held by another member (annulled included); same-name is a no-op success.
- [x] T2 Roles persistence + domain: `Role` (name, channels), tables `roles`, `role_channels`, `members.RoleId` column, `IRoleRepository`, CRUD + assign-role commands. Route: delegated writer. Branch `feat/member-roles-t2-roles`. Single `UpdateRoleCommand` (name + channels). Annulled members count as holding a role when deleting.
- [x] T3 Default notification channels: table + repository + commands. Route: delegated writer. Commit 3834b55 on `feat/member-roles-t3-default-channels`. Assessed medium, 339 lines since boundary 048ba18: `under_budget`, review pending in slice (accumulates with T4). Unset -> both channels; stored empty -> none (marker row in `notification_settings`).
- [x] T4 Monitoring routing: PublicApi exposes channels for member/default; channel-selecting notification service; tests. Route: delegated writer. Branch `feat/member-roles-t4-routing`. Recognized = Outcome Allowed with MemberId; Denied/HumanVerificationRequired use defaults. No-role -> defaults implemented in `NotificationRoutingReader`. CompositeNotificationService extended in place (scoped, no captive dependency).
- [x] T5 UI: Roles page (CRUD + channels), role selector on Members/MemberDetail, default-channels settings. Route: delegated writer + separate browser verifier. Branch `feat/member-roles-t5-ui`. Enter/Escape not implemented in Roles page inputs (keyboard gap, not required).

## Acceptance
- Member can be renamed (blank rejected, duplicate policy same as enrollment).
- Roles CRUD from UI; member can be assigned/unassigned a role.
- Recognized alert goes only through role channels; denied through default channels.
- Existing DBs upgrade without data loss.
- `dotnet test` green.

## Progress
T1 done. Slice 1 = T1 (~250 changed lines + new files).

## Verification evidence
- T1: `dotnet test tests/Enrollment.UnitTests` -> 118 passed, 0 failed (parent re-run). Writer also ran full solution: Monitoring.UnitTests 76 passed. UI not browser-tested.
- Review T1 (range base..HEAD, medium risk, 491 lines): consent granted, review-reliability approved, acknowledgement burned (lineage review-ddfd60d0cafe0b3f). Boundary advances to the T1 slice head.
- Non-blocking advisory findings (follow-ups, not reopened): (1) rename uniqueness check is check-then-act, not atomic across concurrent renames; (2) no automated test for the MemberDetail rename UI; (3) dead `Rename` failure branch in `SqliteVecMemberRepository.RenameAsync`.

- T2: `dotnet test` whole solution -> Enrollment 157 passed, Monitoring 76 passed, 0 failed (parent re-run). Commit d0fa2df.
- Review T2 (base T1 head a07ea6e, medium risk, 1394 lines, above the ~400 heuristic because roles domain+persistence+use cases+tests are one coherent behavior): consent granted, review-reliability approved, acknowledgement burned (lineage review-105dc9fc42f692dc).
- Non-blocking advisory findings (follow-ups): create/update role race can surface a raw SqliteException on the UNIQUE name (handlers check-then-act); delete role can race with a concurrent assignment (dangling RoleId); rename-preserves-role test coverage is weak; `Role` does not validate enum values in channels.
- Stale: a consent granted for candidate main..HEAD (target ed569cdb...) was never executed; its invocation became invalid once T2 files appeared. Not a review record.

- T3: `dotnet test` whole solution -> Enrollment 165 passed, Monitoring 76 passed, 0 failed (parent re-run).
- Stop-hook offered a START for `main..HEAD` (accumulated branch); not run: T1/T2 already reviewed per slice, ODD candidate is a slice, and new untracked files would invalidate it. No consent asked, no record created. Reviewed boundary stays 048ba18.

- T4: `dotnet test` whole solution -> Monitoring 85 passed, Enrollment 170 passed, 0 failed (parent re-run). Commit 9a56f01. Not run against real Development/Production hosts.
- Review T3+T4 (base 048ba18, medium, 780 lines): consent granted, review-reliability approved, acknowledgement burned (lineage review-fd7fac9c2f7db053). Reviewed boundary is now d68ba8a.
- Non-blocking advisory findings (follow-ups, all test-coverage): Allowed + null MemberId not tested in the composite; `Guid.Empty` branch of `NotificationRoutingReader` untested; reject-then-unchanged not tested against a prior custom default set; Monitoring hard-depends on `INotificationRoutingReader` (AddEnrollment) with no descriptive failure if a host omits it.

- T5: `dotnet test` whole solution -> Enrollment 170 passed, Monitoring 85 passed, 0 failed. Real-browser verification (Playwright, scratch DB, lena.jpg face from outside repo): roles create/validate/edit/delete-confirm, default channels (never-stored=both, stored-empty distinct), assign/unassign role, role-in-use error, Members Role column, T1 rename regression: all PASS; no console errors.

- Review T5 (base d68ba8a, medium, 703 lines): consent granted, review-reliability ran and ended `escalated` (cause `unknown_causality`, finding R3-1 unresolved; refuter corroborated R3-2 at Members.razor:139-140 and MemberDetail.razor:317-318). Native STATUS is terminal (`native_stop_required`), no correction offered, no acknowledgement possible; the finding texts are not exposed by the CLI. Observed by reading those lines: a dangling RoleId renders '—' on the Members list but 'unknown role' on MemberDetail (inconsistent). T5 is therefore NOT approved by review; the decision on how to proceed is pending with the user.

- Review T5 round 2 (T5 + dangling-role label fix ba04e2f, base d68ba8a, medium, 709 lines): consent granted, review-reliability opened `correction_required` with a real BLOCKER R3-1 (MainLayout.razor:29, missing space in `href="/incidents"class=`; introduced by my T5 commit ba4cad1, undetected by the browser check because HTML parsers tolerate it). One bounded correction (plan 2 lines) committed cd5c737; targeted validation approved; acknowledgement burned (lineage review-63e672b18ec32d1b). Reviewed boundary is now cd5c737. Lesson: the first `escalated` round was R3-1 with unknown causality; the refuter-corroborated R3-2 was the dangling-role label inconsistency, fixed in ba04e2f.
- Non-blocking advisory findings (follow-ups): R3-2 WARNING Roles.razor (whole file, no automated coverage for the page), R3-3 SUGGESTION MemberDetail.razor:317-318.

## Assumption to confirm (T4)
Recognized member with NO role assigned -> default channels (keeps today's behavior on upgrade). Role with zero channels -> no notification.

## Next step
All five tasks done and reviewed. Deliver the chain (needs user go-ahead to push): PRs T1..T5 into `feat/member-roles-notifications`, then one PR into `main`.
