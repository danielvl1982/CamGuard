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
- [ ] T4 Monitoring routing: PublicApi exposes channels for member/default; channel-selecting notification service; tests.
- [ ] T5 UI: Roles page (CRUD + channels), role selector on Members/MemberDetail, default-channels settings.

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

## Assumption to confirm (T4)
Recognized member with NO role assigned -> default channels (keeps today's behavior on upgrade). Role with zero channels -> no notification.

## Next step
T4 (Monitoring routing) on `feat/member-roles-t4-routing`; review the T3+T4 slice against base 048ba18 once committed.
