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
- [ ] T2 Roles persistence + domain: `Role` (name, channels), tables `roles`, `role_channels`, `members.RoleId` column, `IRoleRepository`, CRUD + assign-role commands.
- [ ] T3 Default notification channels: table + repository + commands.
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

## Next step
T2 (roles persistence + domain) on slice branch `feat/member-roles-t2-roles`, stacked on T1.
