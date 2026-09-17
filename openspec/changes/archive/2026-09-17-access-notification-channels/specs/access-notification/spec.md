# Delta for Access Notification

## MODIFIED Requirements

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

## ADDED Requirements

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
