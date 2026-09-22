# Delta for member-enrollment

## ADDED Requirements

### Requirement: Normalized FullName Lookup and Single-Embedding Re-enrollment

The system MUST provide `FindByFullNameAsync` that normalizes `FullName` by trim plus `OrdinalIgnoreCase` and MUST branch enrollment: if a match exists, overwrite that member's single embedding via Upsert reusing the same `MemberId`; if none, create a new member via `Member.Enroll`. The system MUST keep exactly one `FaceEmbedding` per member.

#### Scenario: Case-insensitive match overwrites same MemberId

- GIVEN member `Ana Torres` is enrolled
- WHEN a watch file arrives with `FullName` `ana torres`
- THEN `FindByFullNameAsync` returns that member
- AND after success the stored `MemberId` equals the original

#### Scenario: Trimmed name matches

- GIVEN member `Jose Garcia` is enrolled
- WHEN a watch file arrives with `FullName` `  Jose Garcia  `
- THEN lookup returns that member and enrollment overwrites its embedding

#### Scenario: Unknown name creates new member

- GIVEN no normalized match for `Luis Ponce`
- WHEN a watch file with `FullName` `Luis Ponce` and one valid face is processed
- THEN a new `MemberId` is created with one persisted record

#### Scenario: Re-enrollment replaces embedding

- GIVEN a member enrolled from photo A
- WHEN same normalized `FullName` is re-enrolled from photo B (one face)
- THEN the store holds exactly one record for that `MemberId` with photo B's embedding
- AND a directory query with photo B's embedding returns that member

### Requirement: Enrollment Name Validation

The system MUST reject null, empty, or whitespace-only `FullName`, MUST NOT run detection or persist, and MUST return `InvalidName`.

#### Scenario: Empty FullName rejected

- GIVEN a stable photo with one face and `FullName` `""` or whitespace
- WHEN enrollment is processed
- THEN it is rejected with `InvalidName` and no record is persisted

## MODIFIED Requirements

### Requirement: Atomic Vector Persistence

The system MUST persist the member's identity data together with the extracted embedding(s) to the vector store as a single durable record, and MUST NOT persist a partial record when any submitted photo fails the face-detection gate. For an existing normalized `FullName`, the system MUST atomically Upsert the same `MemberId` with the new single embedding after the gate passes, MUST NOT create a second row, and MUST keep exactly one embedding per member. Batch `register` remains all-or-nothing; watch ingestion is per-file atomic.

(Previously: persisted only new members; re-enrollment was out of scope and atomicity covered only batch register without normalized overwrite semantics)

#### Scenario: Successful end-to-end enrollment

- GIVEN identity data and one reference photo containing exactly one detected face
- WHEN the register-member command completes
- THEN a vector record for that member is persisted in the vector store
- AND the persisted record is retrievable by a later directory query

#### Scenario: Enrollment with one invalid photo persists nothing

- GIVEN identity data and multiple reference photos where at least one fails the face-detection gate
- WHEN the register-member command is processed
- THEN the entire enrollment is rejected
- AND no vector record is persisted for any of the submitted photos

#### Scenario: Re-enrollment overwrite is atomic

- GIVEN existing member `Maria Lopez`
- WHEN a watch file for `  MARIA LOPEZ  ` with one face is processed
- THEN the system Upserts the same `MemberId` atomically and member count does not increase
