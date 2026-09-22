# Enrollment Watch Ingestion Specification

## Purpose

Isolated folder-watch ingestion for enrollment photos in `photos/enroll`, separate from `motion-image-ingestion` (`photos/watch`). Covers detection, stability gate, per-file `FullName` prompt with watcher pause, and `processed/`/`quarantine/` archival.

## Requirements

### Requirement: Enrollment Watch Folder Isolation

The system MUST watch `photos/enroll` for new image files and MUST NOT watch or process files in `photos/watch`.

#### Scenario: New enrollment file enqueued

- GIVEN the enrollment watcher runs on `photos/enroll`
- WHEN a new image file is created in `photos/enroll`
- THEN the system enqueues it for stability checking

#### Scenario: Monitoring folder ignored

- GIVEN a file is created in `photos/watch`
- WHEN the enrollment watcher runs
- THEN the enrollment pipeline MUST NOT enqueue it

### Requirement: Debounce and File Stability Gate

The system MUST verify a file is fully written via exclusive open (`FileShare.None`) and MUST retry up to 5 times at 200 ms intervals when locked; only stable files proceed to enrollment.

#### Scenario: Locked file retried until stable

- GIVEN a detected file cannot be opened exclusively
- WHEN the stability check runs
- THEN the system retries with backoff
- AND WHEN the file becomes readable within retries, it proceeds to prompt

#### Scenario: File never stabilizes

- GIVEN a file remains locked after all retries
- WHEN the stability window expires
- THEN the system MUST NOT enroll it
- AND MUST move it to `photos/enroll/quarantine/`

### Requirement: Per-File FullName Prompt and Watcher Pause

The system MUST prompt for `FullName` once per stable file and MUST pause the watcher while blocked on input so queued files are not lost; it MUST resume after input.

#### Scenario: Stable file prompts for FullName

- GIVEN a file passed the stability gate
- WHEN the system is ready to enroll it
- THEN it prompts `Member FullName for '{fileName}':` and waits for one line

#### Scenario: No loss while prompt blocks

- GIVEN the watcher is paused for a prompt
- WHEN 5 more files arrive during the prompt
- THEN none are lost to bounded-channel eviction
- AND they are processed sequentially after the current file

### Requirement: FullName Validation

The system MUST reject empty or whitespace-only `FullName`, MUST NOT extract embeddings or persist records, and MUST return `InvalidName`.

#### Scenario: Empty FullName rejected

- GIVEN a stable file is prompted and user enters `""` or whitespace
- WHEN validation runs
- THEN enrollment is rejected with `InvalidName`
- AND no vector record is persisted
- AND the file moves to `photos/enroll/quarantine/`

### Requirement: Completion Archival

The system MUST move each completed file out of `photos/enroll`: successes to `photos/enroll/processed/`, failures (validation, NoFace, MultipleFaces) to `photos/enroll/quarantine/`; it MUST NOT leave completed files in the drop folder. Success determination follows `member-enrollment` branching on normalized `FindByFullNameAsync` (existing → Upsert overwrite same `MemberId`, new → `Enroll`).

#### Scenario: Success archived to processed

- GIVEN a stable file with valid `FullName` and exactly one face
- WHEN enrollment succeeds (new or overwrite)
- THEN the file moves to `photos/enroll/processed/`

#### Scenario: NoFace archived to quarantine

- GIVEN a photo has zero detected faces
- WHEN enrollment is processed
- THEN no record is persisted and file moves to `photos/enroll/quarantine/`

#### Scenario: MultipleFaces archived to quarantine

- GIVEN a photo has two or more detected faces
- WHEN enrollment is processed
- THEN no record is persisted and file moves to `photos/enroll/quarantine/`
