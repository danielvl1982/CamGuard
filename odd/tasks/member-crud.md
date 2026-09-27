# Feature: Member CRUD (soft delete + image management + duplicate-face guard)

> ODD feature document — single source of truth for this feature's life. Every phase/task
> outcome, verification evidence, and decision is logged here automatically as work progresses.

## Objective

Complete the member CRUD in the web UI: soft-delete ("anulado") users with a one-click
action on the list, a member detail page showing every stored image with add/delete
actions, and a cross-member duplicate-face validation on both registration (alta) and
image addition (adición).

## Problem / Why

The directory is currently read-only (register + list) and — critically — **no images are
persisted**: `RegisterMemberCommandHandler` extracts embeddings from the uploaded photos,
saves only `embeddings[0]` on the member record, and disposes the streams; the image bytes
never reach disk. A detail page "with all its images" therefore requires image persistence
built from scratch, plus the soft-delete field and the duplicate-face gate requested by
the user (session 2026-09-27).

## Scope

### In scope
- Annul flag on `Member` + `MemberVectorRecord` + mapper; annul action (repository method
  + list UI); annulled users excluded from `/members` **and from recognition**
  (`FindNearestAsync`) per OD1.
- Image persistence: files under `photos/members/<memberId>/` (covered by existing
  `photos/.gitignore`) + per-image records (`memberId`, file path, **own embedding**) in
  the vector store. Registration starts persisting every submitted photo.
- Member detail page `/members/{id}`: identity + image gallery, image-serving endpoint.
- Add images from the detail page; **total** delete of an image (record + file).
- Duplicate-face validation on alta y adición: the new image's embedding is matched
  against **other** members' stored images; a match ⇒ inline error naming the matched
  member and NO partial persistence (same member's own other images are allowed).

### Out of scope (v1)
- Backfill of historical images (bytes were never stored — impossible; existing members
  will show an empty gallery).
- Auth/authorization, member rename/edit, hard-delete of members.
- Restore-from-annul UX unless OD1 decides otherwise.
- Changes to the Monitoring recognition flow beyond what OD1 forces.

## Constraints

- .NET 10, central package management, hexagonal boundaries: Domain stays free of ASP.NET
  and of the preview vector-store package.
- Photo binaries never committed (`photos/.gitignore`); SQLite store stays local.
- UI copy in English. Tailwind: one-shot `npm run build` after introducing new utility
  classes — NEVER use a watcher as a verification command (T3 incident: 2h16m hang).
- Pre-push hook blocks direct pushes to `main` (repo policy).
- Baseline: `dotnet test` 131/131 green, `dotnet build CamGuard.slnx` 0 errors.

## TDD & checks

- **TDD mode: OFF** (source: no strict-TDD configuration in project or session; frameworks
  present do not enable it). Ordinary functional checks apply.
- Test runner: `dotnet test`; build: `dotnet build CamGuard.slnx`.
- Per-task verification commands are listed in each task.

## Delivery strategy

- **Strategy: `ask-on-risk`** (default). **Forecast: ~1,200–1,400 authored lines (>400 budget).**
- **Chain strategy: `stacked-to-main` (user decision, 2026-09-27).** Slices merged to main
  in order, each under ~400 lines:
  - **PR1** = T1 + T2 (soft delete incl. recognition filter + image persistence)
  - **PR2** = T3 + T4 (duplicate-face validation + list annul action)
  - **PR3** = T5 + T6 + T7 (detail page + total delete + add images)
  - **PR4** = T8 (verification & closure)
  - Exact branch naming decided at first push (precedent: prior feature).

## Open decisions

- **OD1 — RESOLVED (user, 2026-09-27): annulled = hidden from list AND excluded from
  recognition.** An annulled member must not be returned by `FindNearestAsync`
  (recognition/monitoring must not grant access). T1 owns the flag + list exclusion;
  T-scope also covers the recognition path — the nearest-match query must post-filter
  (or filter server-side) annulled members, verified with tests.
- **OD2 (default set, revisable at implementation):** duplicate threshold = configurable
  cosine-distance value (ArcFace convention: 0 = identical); on match, the error message
  names the matched member. Matching considers ALL members' images including annulled
  ones (re-registering an annulled person's face still flags the match).

## Tasks

- [x] **T1 — Soft delete (domain + persistence) — DONE**
  Annul flag on `Member` (new state + `Annul()`, `Rehydrate` signature), on
  `MemberVectorRecord` + `MemberVectorRecordMapper`, repository annul/upsert method,
  annulled excluded from the list query (`ListAllAsync` semantics or new method) **and
  from recognition** (per OD1: `FindNearestAsync` must not return annulled members —
  filter in the repository adapter so monitoring can never grant them access).
  **Empirical probe first**: does `EnsureCollectionExistsAsync` add a column to an
  EXISTING SqliteVec table (preview package), or is raw `ALTER TABLE` via
  Microsoft.Data.Sqlite needed (precedent: T2 probe of the prior feature)?
  Unit tests: annul round-trip, list excludes annulled, recognition excludes annulled,
  legacy rows default to non-annulled.
  *Acceptance:* `dotnet test` green incl. new tests; existing DB keeps loading.
  *Route:* delegated (writer trigger: 5+ non-trivial files).
  *Evidence:* 2026-09-27. 6 paths: `Member.cs` (`IsAnnulled` + idempotent `Annul()`,
  `Rehydrate(..., isAnnulled: false)`), `IMemberVectorRepository` (new
  `SetAnnulledAsync`, `KeyNotFoundException` on unknown id), `MemberVectorRecord`
  (`[VectorStoreData] bool IsAnnulled`), mapper (both ways), `SqliteVecMemberRepository`
  (`EnsureSchemaAsync` on every touch; `FindNearestAsync` top-10 → first non-annulled;
  `ListAllAsync` skips annulled; `FindByFullNameAsync` unchanged), new
  `tests/Enrollment.UnitTests/MemberAnnulTests.cs` (9 tests incl. annulled-top-1 skip,
  all-annulled → null, legacy column-less rows read non-annulled).
  **Probe finding:** `EnsureCollectionExistsAsync` is CREATE TABLE IF NOT EXISTS —
  does NOT add columns; raw `ALTER TABLE … ADD COLUMN IsAnnulled INTEGER NOT NULL
  DEFAULT 0` via `Microsoft.Data.Sqlite` (already referenced, no new packages),
  guarded by `pragma_table_info('t')` (single quotes — double quotes fail) + race catch.
  Verified: build 0 errors (8 pre-existing warnings); `dotnet test` 140/140 green
  (Enrollment 77 = 68 + 9 new; Monitoring 63). Parent spot-check: MemberAnnul 9/9.
  *Risks carried:* top-K=10 depth (fail-safe deny beyond 10 nearer annulled);
  read-modify-write last-write-wins on concurrent annul/restore (single-admin UI, OK).

- [x] **T2 — Image persistence port + register wiring — DONE**
  `IMemberImageRepository` (domain port): `SaveAsync(memberId, stream/embedding) →
  imageId`, `ListByMemberAsync`, `DeleteAsync`; file layout `photos/members/<memberId>/`;
  new vector collection `MemberImageRecord` (`Id`, `MemberId`, `FilePath`, per-image
  `Embedding`) in a new vector collection.
  Wire `RegisterMemberCommandHandler` to persist ALL submitted photos (alta now stores
  images, not just `embeddings[0]`). Tests.
  *Acceptance:* `dotnet test` green; a web registration leaves files + records.
  *Route:* delegated (writer trigger: 4+ non-trivial files).
  *Evidence:* 2026-09-27. 7 new + 3 modified: `MemberImageId` (Guid-backed +
  `MemberImageIdError`), `MemberImage` (read-model `Id/MemberId/FileName/Embedding/
  StoredAtUtc`), `IMemberImageRepository` (+`ListAllAsync` with vectors for T3),
  `MemberImageRecord`/`Mapper` (new `member_images` collection), new
  `SqliteVecMemberImageRepository` (T1-style `ALTER TABLE` migration; delete is
  file-first-then-record so failures surface with record intact; record-write failure
  best-effort removes the file), `MemberImageTests` (9 tests), `VectorStoreOptions`
  (+`MemberImagesRoot` default `photos/members`, +`MemberImagesCollectionName`,
  auto-bound), DI registration, handler persists every photo (magic-byte extension
  sniff; images saved BEFORE the member record → store failure = new
  `ImageStoreFailed`, member NOT persisted; compensation on mid-batch failure; caller
  streams copied, never disposed, left at position 0; no annul gating).
  Verified: build 0 errors (10 pre-existing warnings); Enrollment tests 86/86
  (77 + 9 new). Parent spot-check: MemberImage 9/9.
  *Notes for later:* actual layout `<MemberImagesRoot>/<memberId>/<imageId>.<ext>`
  (= `photos/members/<id>/…`, as specced). T5 image endpoint: no byte-read method on
  the port — reconstruct path from options + `MemberImage.FileName` (or add one in T5).
  Residual orphan direction: member-save failure after images land leaves orphan images
  (pre-existing error class, unchanged).

- [x] **T3 — Duplicate-face validation (alta + adición) — DONE**
  Shared validator: extract embedding → search other members' image embeddings (exclude
  the same `memberId`) → below-threshold distance ⇒ failure carrying the matched member's
  name. Config threshold (options section bound in `AddEnrollment`). New
  `RegisterMemberError` value + inline message in `Register.razor`; validator reused by
  T7. Tests: match against other member rejected, same-member images allowed, no-match
  accepted, threshold boundary.
  *Acceptance:* `dotnet test` green; registering a face already enrolled to another
  member fails with a message naming that member and persists NOTHING.
  *Route:* delegated (writer trigger: 3+ non-trivial files).
  *Evidence:* 2026-09-27. New `MemberImageDuplicateValidator`
  (`IMemberImageDuplicateValidator.FindMatchAsync(candidate, ownerMemberId?, ct) →
  DuplicateFaceMatch?`, client-side enumeration over image `ListAllAsync`, match iff
  cosine distance strictly below threshold) + `DuplicateFaceDistanceThreshold` option
  (default **0.4** ≈ similarity 0.6, conservative ArcFace same-person cutoff, marked
  OD2-calibration-pending). Handler runs the gate after face+name validation, before
  ANY persistence (owner null at alta); new `RegisterMemberError.MatchesOtherMember` +
  `RegisterMemberFailure` payload (handler now returns
  `Result<MemberId, RegisterMemberFailure>`); matched name via new
  `IMemberVectorRepository.FindByIdAsync` (returns annulled too, unlike list);
  `Register.razor` maps the new error ("This face appears to already be registered to
  {name}…", existing CSS only). Tests: new `MemberDuplicateFaceTests` (8: reject+name+
  zero-persistence, owner exclusion, best-match id+distance, boundary 0.39/0.41,
  annulled still rejects, second-photo match stores nothing) + updated handler/image
  tests. Verified: build 0 errors; Enrollment 94/94. Parent spot-check:
  MemberDuplicateFace 8/8. T7 contract: pass owner id, resolve name via `FindByIdAsync`.

- [ ] **T4 — Members list: annul action**
  `/members` gains a per-row "Annul" action with confirmation (static SSR must become an
  interactive island or form POST), annulled users no longer listed (per OD1 scope),
  row links to the detail page. Tailwind rebuild if new utility classes appear.
  *Acceptance:* build green; smoke: annul → row disappears, member record still present.
  *Route:* delegated (writer trigger: 2+ non-trivial files).

- [ ] **T5 — Member detail page + image endpoint**
  `/members/{id}` route: identity block + image gallery (thumbnails) + empty state;
  minimal-API endpoint streaming image bytes from `photos/members/…` (files live outside
  `wwwroot`); navigation from the list. Interactive rendering for T6/T7 actions.
  *Acceptance:* build green; registered member shows all its images.
  *Route:* delegated (writer trigger: 3+ non-trivial files).

- [ ] **T6 — Total image delete**
  Backend delete path (record + file, both or nothing) + per-image "Delete" action on
  the detail page with confirmation; guard against deleting an image of another member
  (server-side `memberId` scoping). Tailwind rebuild if needed.
  *Acceptance:* build green; smoke: delete → file gone from disk, record gone, gallery
  refreshes.
  *Route:* delegated (writer trigger: 2+ non-trivial files).

- [ ] **T7 — Add images from the detail page**
  `InputFile` on `/members/{id}` → new application command (extraction + T3 validator +
  T2 store) → gallery refresh. Inline mapping of every error (gate failures, duplicate
  face with matched member's name, store failure); form state preserved on failure.
  *Acceptance:* build green; smoke: add valid image appears; add face of ANOTHER member
  → named inline error, nothing persisted.
  *Route:* delegated (writer trigger: 2+ non-trivial files).

- [ ] **T8 — Full verification & closure**
  `dotnet build` + full `dotnet test`, manual end-to-end smoke (annul → detail →
  add → duplicate-reject → delete), RDD candidate assessment per work-unit commit if
  enabled, final commit, feature-doc evidence refresh.
  *Acceptance:* all checks green or failures recorded honestly.
  *Route:* inline (bounded verification).

## Acceptance criteria (feature-level)

1. Annulling a user consumes the action from the list UI; annulled users are not shown
   in `/members` AND are never returned by recognition (`FindNearestAsync`) — OD1.
2. The detail page shows every stored image of the member; delete removes the image
   permanently (record + file); new images can be added from the same page.
3. Any image (at alta or adición) whose face matches a DIFFERENT member is rejected with
   an inline message naming that member, and nothing is persisted.
4. `dotnet test` green; Console app behavior unchanged; no photo binaries committed.
5. Each task lands as one work-unit commit on the feature branch (Conventional Commit,
   no AI attribution).

## Route declarations

| Task | Route | Trigger evidence |
|---|---|---|
| T1 | delegated | 5+ non-trivial files (writer trigger) |
| T2 | delegated | 4+ non-trivial files (writer trigger) |
| T3 | delegated | 3+ non-trivial files (writer trigger) |
| T4 | delegated | 2+ non-trivial files (writer trigger) |
| T5 | delegated | 3+ non-trivial files (writer trigger) |
| T6 | delegated | 2+ non-trivial files (writer trigger) |
| T7 | delegated | 2+ non-trivial files (writer trigger) |
| T8 | inline | bounded verification |

## Progress log

- 2026-09-27 — Feature doc created from the user's task list (soft delete + list action,
  detail page with gallery, total image delete, add images, duplicate-face validation on
  alta/adición). Key discovery: **images are not persisted today** — only `embeddings[0]`
  survives registration, so T2 (image storage) is new foundational scope and existing
  members will have empty galleries.
- 2026-09-27 — **OD1 resolved (user): annulled = hidden from list AND excluded from
  recognition.** T1 scope extended to post-filter `FindNearestAsync`.
- 2026-09-27 — **Chain strategy: `stacked-to-main` (user).** PR slicing: PR1=T1+T2,
  PR2=T3+T4, PR3=T5+T6+T7, PR4=T8. Ready to start T1.
- 2026-09-27 — T1 landed as `ac5b7bf` (7 files, 392 insertions, tests 140/140 green,
  spot-check MemberAnnul 9/9). RDD assess (base 1bd2117): medium, `slice_budget_reached`
  → review offered, **user chose "Skip this time"** → `declined_this_candidate`,
  candidate-scoped. Reviewed boundary stays at `1bd2117`.
- 2026-09-27 — T2 landed as `76c4244` (11 files, 783 insertions, Enrollment tests
  86/86, spot-check MemberImage 9/9). RDD assess: medium, `slice_budget_reached`
  (cumulative 1382 lines) → review offered, **"Skip this time" again** →
  `declined_this_candidate`. Boundary stays at `1bd2117`.
