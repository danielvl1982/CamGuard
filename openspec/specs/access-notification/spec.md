# Access Notification Specification

## Purpose

Current behavior is documented in [docs/features/access-notification.md](../../../docs/features/access-notification.md).

Defines `INotificationService` firing an `AccessAlert` for every access-decision outcome (including `Allowed`), and defines that a notification failure MUST NOT crash the pipeline. In Development environments, notifications are logged via `LoggingNotificationService`. In Production and other non-Development environments, notifications are delivered through real channels (ntfy push and SMTP email), with per-channel failure isolation to prevent one channel's misconfiguration or outage from blocking the other.

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

The system MUST NOT allow a failure raised by `INotificationService` (or any registered channel it resolves to) to crash the workflow run or prevent file archival. The system MUST log the failure and allow the run to continue to completion. When the resolved `INotificationService` fans out to multiple real channels (ntfy + email), a failure raised by one channel MUST NOT prevent the other channel from attempting delivery for the same `AccessAlert`.
(Previously: only described the workflow-level catch around a single `INotificationService`/`LoggingNotificationService` call; did not yet address multi-channel fan-out because no real channel existed.)

#### Scenario: Notification failure does not crash the pipeline

- GIVEN `INotificationService.NotifyAsync` throws an exception for a given outcome
- WHEN the workflow run continues
- THEN the exception is caught and logged
- AND the workflow run still completes and the file is still archived

#### Scenario: One channel's failure does not block the other in the composite

- GIVEN the resolved `INotificationService` fans out to both the ntfy channel and the email channel
- WHEN the ntfy channel raises an exception delivering an `AccessAlert`
- THEN the exception is caught and logged for that channel
- AND the email channel still attempts delivery of the same `AccessAlert`

### Requirement: Channel Selection By Environment

The system MUST resolve `INotificationService` based on the current `IHostEnvironment` at composition time. In the Development environment, the system MUST resolve `LoggingNotificationService`. In every other environment, the system MUST resolve a composite service that fans out to real delivery channels (a push channel and an email channel).

#### Scenario: Development resolves the logging channel

- GIVEN the host environment reports Development
- WHEN `INotificationService` is resolved from the container
- THEN the resolved instance is `LoggingNotificationService`

#### Scenario: Non-Development resolves the composite real-channel service

- GIVEN the host environment reports an environment other than Development
- WHEN `INotificationService` is resolved from the container
- THEN the resolved instance fans out to both the push channel and the email channel
- AND neither channel is `LoggingNotificationService`

### Requirement: No Delivery Secrets In Source Control

The system MUST NOT persist notification-channel credentials (SMTP password, or any equivalent delivery secret) in `appsettings.json` or any other file committed to source control. Secrets MUST be supplied at runtime through a mechanism external to the repository (e.g., user secrets, environment variables, or a secret store). Non-secret configuration (host, port, from/to addresses, push topic name) MAY remain in `appsettings.json` as empty or placeholder values.

#### Scenario: Committed configuration carries no secret

- GIVEN the repository's `appsettings.json`
- WHEN its email and push notification sections are inspected
- THEN no SMTP password or equivalent delivery credential is present

#### Scenario: Missing required channel configuration fails fast

- GIVEN a real notification channel's required configuration (e.g. push topic, or SMTP host/from/to address) is unset
- WHEN that channel attempts to deliver an `AccessAlert`
- THEN the channel raises an error identifying the missing configuration
- AND it does not silently skip delivery without logging the failure
