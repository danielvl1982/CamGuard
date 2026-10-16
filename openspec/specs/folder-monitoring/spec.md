# folder-monitoring Specification

## Purpose

Baseline spec (current behavior, derived from code on 2026-10-16). The hosted monitoring process observes created image files, runs the monitoring workflow, and archives completed or failed files. Documentation: `docs/features/folder-monitoring.md`. Ticket: none.

## Requirements

### Requirement: Start and observe the configured watch folder

The Monitoring host SHALL register Enrollment and Monitoring and run `MonitoringHostedService` for the host lifetime (`src/Monitoring.Host/Program.cs:8-13`; `src/Monitoring.Infrastructure/MonitoringHostedService.cs:26-30`). The ingestion source SHALL create the configured folder and watch file name, last-write, and size changes, forwarding `Created` file paths through a bounded channel (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-75,89-99`). The default `WatchFolder` path SHALL be `watch` (`src/Monitoring.Infrastructure/WatchFolderOptions.cs:9-24`).

#### Scenario: Host starts monitoring
- GIVEN the Monitoring host starts
- WHEN its hosted service executes
- THEN the system SHALL consume paths emitted by `IIngestionSource.WatchAsync` until cancellation

#### Scenario: A file is created in the watch folder
- GIVEN the configured watch folder exists or can be created
- WHEN `FileSystemWatcher` raises a `Created` event for a file
- THEN the system SHALL enqueue its full path in the bounded channel

### Requirement: Filter and stabilize created paths

The ingestion source SHALL ignore paths that are directories and SHALL NOT apply an extension filter to created file events (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:61-67,89-99`). Before emitting a path, it SHALL try to open the file with exclusive access, retry I/O errors with increasing delays, and omit a missing or persistently unstable file after the configured maximum attempts (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-139`; `src/Monitoring.Infrastructure/WatchFolderOptions.cs:9-24`).

#### Scenario: Directory creation is observed
- GIVEN a directory is created in the watched folder
- WHEN the `Created` event is handled
- THEN the system SHALL ignore that path

#### Scenario: File is still being written
- GIVEN a created file cannot yet be opened exclusively
- WHEN stability checks are performed
- THEN the system SHALL retry with increasing delays and emit the path only after a successful exclusive open

#### Scenario: File disappears or remains unstable
- GIVEN the created file disappears or never becomes stable within the configured attempts
- WHEN stability checking ends
- THEN the system SHALL omit the path

### Requirement: Process each emitted file with bounded workflow retries

For each emitted path, the hosted service SHALL create an independent dependency-injection scope and execute the workflow with a fresh file stream on each of up to three attempts, using increasing pauses after transient exceptions (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:24,34-44,72-104`). The workflow SHALL open the path as an image and provide the path and stream in `MonitoringWorkflowContext` (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:79-91`).

#### Scenario: Workflow succeeds for an emitted file
- GIVEN an ingestion path is emitted
- WHEN the hosted service processes that path
- THEN the system SHALL create a scoped workflow execution and pass it a newly opened stream

#### Scenario: Workflow encounters transient exceptions
- GIVEN workflow execution throws a transient exception
- WHEN an attempt fails and retries remain
- THEN the system SHALL pause for an increasing delay and retry with a newly opened stream, up to three attempts

### Requirement: Run detection, identity, access, and notification

The workflow SHALL detect people and notify directly when none are detected; otherwise it SHALL verify identity, decide access, and notify (`src/Monitoring.Application/MonitoringWorkflowHost.cs:32-38`). Notification channels SHALL use role channels for an allowed result with a recognized member and default channels for other results; if member lookup fails, the system SHALL use default channels, and if default-channel resolution fails, it SHALL attempt every configured channel (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:12-17,56-79`; `src/Enrollment.PublicApi/NotificationRoutingReader.cs:15-32`). A failure to send on one channel SHALL NOT prevent attempts on other channels (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:42-52`).

#### Scenario: No person is detected
- GIVEN the workflow detects no person in an image
- WHEN notification routing is selected
- THEN the system SHALL notify without running the identity and access decision path

#### Scenario: Notification routing falls back
- GIVEN the workflow produces a result requiring notification
- WHEN the member is not recognized or member lookup fails
- THEN the system SHALL use default channels, falling back to every configured channel if resolving defaults fails

#### Scenario: One notification channel fails
- GIVEN multiple notification channels are selected
- WHEN sending to one channel fails
- THEN the system SHALL continue attempting the remaining channels

### Requirement: Archive completed and unsuccessful files

The hosted service SHALL move a file to `processed/` when its workflow completes and SHALL move it to `quarantine/` when the workflow does not complete or a non-cancellation exception persists (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-68`; `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs:25-42`). Errors during archival SHALL be logged without terminating the processing loop; cancellation SHALL NOT be treated as a file-processing failure (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:51-68`).

#### Scenario: Workflow completes
- GIVEN the workflow completes for a file
- WHEN processing finishes
- THEN the system SHALL move the file to `processed/`

#### Scenario: Workflow fails after retries
- GIVEN the workflow does not complete or a non-cancellation exception persists
- WHEN processing is finalized
- THEN the system SHALL log the failure and move the file to `quarantine/`

#### Scenario: Processing is cancelled
- GIVEN cancellation is requested while processing a file
- WHEN an `OperationCanceledException` is raised
- THEN the system SHALL NOT treat the cancellation as a file failure

### Requirement: Report ingestion overload and watcher errors

When the bounded channel is full, the ingestion source SHALL discard the `Created` event and log a warning (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:45-54,96-99`). When the watcher raises `Error`, the system SHALL log a possible internal-buffer overflow; current behavior does not show automatic rescanning or recovery of missed events (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:102-107`).

#### Scenario: Bounded channel is full
- GIVEN the bounded ingestion channel has no capacity
- WHEN a `Created` event is handled
- THEN the system SHALL discard that event and log a warning

#### Scenario: Watcher reports an error
- GIVEN `FileSystemWatcher` raises an `Error` event
- WHEN the event is handled
- THEN the system SHALL log a possible buffer overflow without automatically recovering missed events
