# Pending Verification Record Specification

## Purpose

Current behavior is documented in [docs/features/pending-verification-record.md](../../../docs/features/pending-verification-record.md).

Defines the terminal `PendingVerification` record persisted whenever a workflow run resolves `HumanVerificationRequired`, whether from person-detection's zero-face path or access-decision-policy's mid-threshold path. The record is a completion artifact for a bounded, independent workflow run, not a suspended/resumable workflow. Automatic resolution and any resolution UI/API are out of scope.

## Requirements

### Requirement: Terminal Record On HumanVerificationRequired
The system MUST persist exactly one `PendingVerification` record whenever a workflow run resolves `HumanVerificationRequired`, regardless of whether that outcome originated from zero detected faces or from a mid-threshold distance decision.

#### Scenario: Zero-face outcome persists a PendingVerification record
- GIVEN person detection resolved `HumanVerificationRequired` due to zero detected faces
- WHEN the workflow run finalizes that outcome
- THEN exactly one `PendingVerification` record is persisted for that run

#### Scenario: Mid-threshold outcome persists a PendingVerification record
- GIVEN access-decision-policy resolved `HumanVerificationRequired` for a distance strictly between the two configured thresholds
- WHEN the workflow run finalizes that outcome
- THEN exactly one `PendingVerification` record is persisted for that run

### Requirement: Record Field Completeness
Each persisted `PendingVerification` record MUST include: a reference to the archived image, the candidate distance value (nullable, since a zero-face path has none), a candidate `MemberId` (nullable), the UTC time the event occurred, and a status field.

#### Scenario: Record fields reflect the triggering path
- GIVEN a `PendingVerification` record is persisted from either triggering path
- WHEN the record is inspected
- THEN it includes an image reference and an occurred-at timestamp
- AND it includes a distance value and candidate `MemberId` only when the triggering path produced them

### Requirement: Initial Pending Status
The system MUST persist every new `PendingVerification` record with a `Pending` status, and MUST NOT persist it in any resolved (approved/rejected) status.

#### Scenario: New record starts as Pending
- GIVEN a `PendingVerification` record is being persisted for the first time
- WHEN persistence completes
- THEN the record's status is `Pending`

### Requirement: No Automatic Resolution
The system MUST NOT automatically transition a `PendingVerification` record out of `Pending` status. Resolving a record requires a separate write path (a future UI/API), which is out of scope for this change.

#### Scenario: Record remains Pending without external action
- GIVEN a `PendingVerification` record exists with status `Pending`
- WHEN no external resolution action is taken
- THEN the record's status remains `Pending` indefinitely
