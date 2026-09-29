# Feature: roles-robustness-followups

Follow-up to `member-roles-notifications` (delivered in PR #10). Closes the non-blocking review findings that are cheap and safe to fix, and records the ones deliberately left out.

## Objective
Make role management robust under concurrent use and fill the test gaps the native reviews reported, without changing user-visible behavior.

## Scope
1. **Role name races.** A concurrent create/update with the same name must surface the typed `DuplicateName` error, never a raw `SqliteException`.
2. **Delete-vs-assign race.** Deleting a role must not leave a member pointing at a role that no longer exists. Make the "role is unused" check and the delete a single atomic statement, and make assignment conditional on the role existing, so SQLite's statement serialization closes the race.
3. **Enum validation.** `Role` rejects undefined `NotificationChannel` values.
4. **Dead branch.** Remove the unreachable `Rename` failure branch in `SqliteVecMemberRepository.RenameAsync`.
5. **Test gaps.** `Allowed` with a null `MemberId` in `CompositeNotificationService`; the `Guid.Empty` branch of `NotificationRoutingReader`; reject-then-unchanged against a previously stored custom default set; rename preserves the member's role.

## Out of scope (decided, not forgotten)
- Atomic member-rename uniqueness: a unique index on the vector table's name column could fail on legacy duplicates and needs a data migration decision. Accepted limitation for a single-admin UI.
- Automated coverage of `Roles.razor` and the rename UI: needs a component-test framework (bUnit) the repo does not have; browser verification stands in.
- Enter/Escape in the `/roles` inputs and the descriptive DI error when `AddEnrollment` is omitted: minor.
- Smoke test against a real ntfy/SMTP host: separate task, needs a destination from the user.

## Constraints
- Same as `member-roles-notifications`: English artifacts, conventional commits without AI attribution, feature branch + PR (hook blocks direct push to `main`), xunit + Moq.AutoMock, TDD off, runner `dotnet test`.
- Delivery strategy: `single-pr` (small, expected under ~400 changed lines; if it grows past that, split).

## Tasks
- [x] R1 Repository/handlers: duplicate-name race mapped to `NameAlreadyInUse` (via `DuplicateRoleNameException`, extended SQLite code 2067 only); atomic `DeleteIfUnusedAsync`; conditional `SetRoleAsync` (single UPDATE, `RoleNotFound`); enum validation in `Role` (`RoleError.InvalidChannel`); dead branch removed. Route: delegated writer (2+ non-trivial files). Commit bf4bd15.
- [x] R2 Tests for the five gaps above plus tests for R1 behavior. Same writer as R1.

## Acceptance
- Two concurrent creates with the same name: exactly one succeeds, the other returns `DuplicateName` (test with real SQLite).
- Delete on a role in use returns the in-use error and keeps the role; delete-then-assign or assign-then-delete never leaves a dangling `RoleId` (test).
- `Role` with an undefined channel value is rejected.
- `dotnet test` green (baseline: Enrollment 170, Monitoring 85).

## Progress
R1 and R2 committed (bf4bd15). Native review round: `correction_required` on a real CRITICAL finding (clearing a role with `SetRoleAsync(id, null)` threw 'no such table' when the roles table had never been created, because the conditional UPDATE always named it). One bounded correction (plan 30 lines) committed as 0232ec4 with a new regression test that fails without the fix (proved by stashing the fix) and passes with it. Targeted validation approved; acknowledgement burned (lineage review-b21c080996bc438b). Pending: PR and merge to `main`.

## Verification evidence
- `dotnet test` whole solution (parent re-run) after the correction: Enrollment 186 passed, Monitoring 86 passed, 0 failed (baseline 170 / 85). The writer re-ran the three race tests 5 times, all green.
- Race tests run two repositories on one SQLite file: concurrent saves, creates and renames with the same name each yield exactly one winner and one typed duplicate error.
- Review advisories after the correction (non-blocking): `DeleteIfUnusedAsync` classifies InUse/NotFound after the delete in a separate read (result classification can be stale, no data risk); `UpdateRoleCommand` `InvalidChannel` mapping is untested; `RenameAsync` ignores the `Rename` result (justified by the earlier blank-name guard, but implicit); the `SetRoleAsync` doc overclaimed 'never dangling' (fixed: it now states the rename/annul caveat).

## Known residual (not fixed here)
`SetAnnulledAsync` and `RenameAsync` are whole-record read-modify-write. A concurrent `SetRoleAsync` between their read and their upsert is silently reverted, and this can resurrect a deleted role id: Rename reads RoleId=A; SetRoleAsync(null); DeleteIfUnused(A) succeeds; Rename upserts A back. Fix idea: make those two paths conditional column updates like `SetRoleAsync`. Needs a separate decision because it touches the vector-store write path.

## Deviations
- `MemberRoleTests` assigned roles that did not exist; updated to save a real role first, since assignment now requires the role row.
- Create/Update handlers map any `RoleError` (including `InvalidChannel`) to their existing `InvalidName`; the UI cannot produce an undefined channel.
- `DeleteAsync` and `IsInUseAsync` remain on `IRoleRepository`; `DeleteAsync` has no production caller now.

## Next step
Open PR to `main` and merge; then task 2 (smoke test against real ntfy/SMTP) once the user names a destination.
