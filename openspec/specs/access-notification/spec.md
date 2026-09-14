# Access Notification Specification

## Purpose

Defines `INotificationService`/`LoggingNotificationService` firing an `AccessAlert` for every access-decision outcome (including `Allowed`), and defines that a notification failure MUST NOT crash the pipeline, since the shipped implementation is a logging placeholder, not a real delivery channel.

## Requirements

### Requirement: Alert Fired For Every Outcome
The system MUST invoke `INotificationService` with an `AccessAlert` for every access-decision outcome, including `Allowed`, `Denied`, and `HumanVerificationRequired`. The system MUST NOT skip notification for the `Allowed` outcome.

#### Scenario: Allowed outcome fires a notification
- GIVEN an access decision resolves `Allowed`
- WHEN the outcome is finalized
- THEN `INotificationService` is invoked with an `AccessAlert` carrying that outcome

#### Scenario: Denied outcome fires a notification
- GIVEN an access decision resolves `Denied`
- WHEN the outcome is finalized
- THEN `INotificationService` is invoked with an `AccessAlert` carrying that outcome

#### Scenario: HumanVerificationRequired outcome fires a notification
- GIVEN an outcome resolves `HumanVerificationRequired` (from either person-detection's zero-face path or access-decision-policy's mid-threshold path)
- WHEN the outcome is finalized
- THEN `INotificationService` is invoked with an `AccessAlert` carrying that outcome

### Requirement: Alert Content Shape
Each `AccessAlert` MUST carry: a nullable candidate `MemberId`, the raw distance value used in the decision (when available), the resolved `AccessOutcome`, a reference to the archived image (not raw image bytes), and the UTC time the event occurred.

#### Scenario: Alert carries required fields
- GIVEN an access decision has been finalized for an ingested image
- WHEN the corresponding `AccessAlert` is constructed
- THEN it includes the outcome, an image reference, and an occurred-at timestamp
- AND it does not include raw image bytes

### Requirement: Notification Failure Isolation
The system MUST NOT allow a failure raised by `INotificationService` (or `LoggingNotificationService`) to crash the workflow run or prevent file archival. The system MUST log the failure and allow the run to continue to completion.

#### Scenario: Notification failure does not crash the pipeline
- GIVEN `INotificationService.NotifyAsync` throws an exception for a given outcome
- WHEN the workflow run continues
- THEN the exception is caught and logged
- AND the workflow run still completes and the file is still archived
