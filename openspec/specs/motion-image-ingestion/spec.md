# Motion Image Ingestion Specification

## Purpose

Defines folder-watch ingestion of dropped images: detect new files, wait until each is stable (not locked/partially written), run exactly one independent workflow per validated file, and archive the file on completion. Detection/verification/decision/notification behavior is out of scope here.

## Requirements

### Requirement: Folder-Watch Detection
The system MUST watch a configured drop folder for newly created image files and MUST enqueue each detected file for stability checking before processing.

#### Scenario: New file triggers ingestion
- GIVEN the drop folder is being watched
- WHEN a new image file is created in that folder
- THEN the system enqueues the file for stability checking

### Requirement: Debounce And Stability Check
The system MUST verify a detected file is fully written and unlocked before starting a workflow run, and MUST retry the stability check with backoff when the file is locked or still being written, rather than failing immediately.

#### Scenario: Locked file is retried until stable
- GIVEN a detected file is still being written and cannot be opened exclusively
- WHEN the system attempts its stability check
- THEN the system retries the check with backoff instead of failing
- AND WHEN the file becomes readable and stable, the system proceeds to start a workflow run

### Requirement: One Independent Workflow Run Per Validated File
The system MUST start exactly one independent workflow run per validated (stable) file, and MUST NOT process the same file twice or block one file's run on another file's outcome.

#### Scenario: Validated file starts exactly one workflow run
- GIVEN a file has passed the stability check
- WHEN ingestion completes for that file
- THEN exactly one workflow run is started for that file
- AND that run's outcome does not depend on any other file's run

### Requirement: Completion Archival
The system MUST signal workflow-run completion for a file by archiving it: moving a successfully processed file to a `processed/` location, and moving a file whose workflow run failed to a `quarantine/` location. The system MUST NOT leave a completed file in the watched drop folder.

#### Scenario: Successfully processed file is archived to processed/
- GIVEN a workflow run for a file completes without failure
- WHEN the run finishes
- THEN the system moves the file from the drop folder to the `processed/` location

#### Scenario: Failed workflow run archives the file to quarantine/
- GIVEN a workflow run for a file fails during processing
- WHEN the run finishes
- THEN the system moves the file from the drop folder to the `quarantine/` location
- AND the file is not left in the watched drop folder
