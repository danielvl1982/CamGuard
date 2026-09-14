# Member Enrollment Specification

## Purpose

Defines the behavior of registering an authorized member into the local face-embedding vector store from identity data and one or more reference photos, so a later Monitoring pipeline can identify that member. Covers input acceptance, face-detection gating, embedding extraction, and vector persistence. Decision-policy behavior (Allowed/Denied/HumanVerificationRequired) belongs to the deferred `monitoring-pipeline` change and is out of scope here.

## Requirements

### Requirement: Member Registration Input
The system MUST accept a register-member command containing member identity/profile data and one or more reference photos.

#### Scenario: Command accepted with valid inputs
- GIVEN identity data and at least one reference photo are provided
- WHEN the register-member command is submitted
- THEN the system proceeds to face-detection validation for each submitted photo

### Requirement: Face Detection Gate
The system MUST run face detection on every submitted reference photo before extracting an embedding, and MUST treat "exactly one detected face" as the only passing condition per photo.

#### Scenario: Photo with exactly one face passes the gate
- GIVEN a reference photo containing exactly one detected face
- WHEN the photo is validated
- THEN the photo proceeds to embedding extraction

### Requirement: Zero-Face Rejection
The system MUST reject enrollment when a submitted reference photo has zero detected faces, and MUST NOT extract an embedding or persist any vector record from that photo.

#### Scenario: Photo with no detectable face is rejected
- GIVEN a reference photo with zero detected faces
- WHEN the register-member command is processed
- THEN the enrollment is rejected
- AND no vector record is persisted for that photo
- AND the failure reason returned to the caller identifies "zero faces detected"

### Requirement: Multiple-Face Rejection
The system MUST reject enrollment when a submitted reference photo has more than one detected face, and MUST NOT extract an embedding or persist any vector record from that photo.

#### Scenario: Photo with multiple faces is rejected
- GIVEN a reference photo with two or more detected faces
- WHEN the register-member command is processed
- THEN the enrollment is rejected
- AND no vector record is persisted for that photo
- AND the failure reason returned to the caller identifies "multiple faces detected"

### Requirement: Embedding Extraction
For each reference photo that passes the face-detection gate, the system MUST extract a face embedding via the `IFaceEmbeddingExtractor` port.

#### Scenario: Embedding extracted from a validated photo
- GIVEN a reference photo that passed the face-detection gate
- WHEN embedding extraction runs
- THEN a face embedding vector is produced for that photo

### Requirement: Atomic Vector Persistence
The system MUST persist the member's identity data together with the extracted embedding(s) to the vector store as a single durable record, and MUST NOT persist a partial record when any submitted photo fails the face-detection gate.

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

## Out of Scope (this spec)

Re-enrollment of an existing member and duplicate-identity detection are not specified by the source proposal and are NOT covered here; they require a follow-up decision before being implemented.
