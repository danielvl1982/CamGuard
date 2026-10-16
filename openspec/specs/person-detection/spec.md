# Person Detection Specification

## Purpose

Current behavior is documented in [docs/features/person-detection.md](../../../docs/features/person-detection.md).

Defines person-presence detection for an ingested image using a face detector as a presence proxy: one or more detected faces proceed to identity verification, and zero detected faces MUST route to human verification rather than being ignored or silently dropped.

## Requirements

### Requirement: Face-Based Person-Presence Detection
The system MUST run face detection on an ingested image and MUST treat "at least one detected face" as evidence of person presence, proceeding to identity verification for each detected face.

#### Scenario: One or more faces detected proceeds to identity verification
- GIVEN an ingested image contains at least one detected face
- WHEN person detection runs
- THEN the system proceeds to identity verification for the detected face(s)

### Requirement: Zero-Face Routing To Human Verification
The system MUST NOT ignore, drop, or silently discard an image when zero faces are detected. The system MUST route a zero-face result to a `HumanVerificationRequired` outcome.

#### Scenario: Zero faces detected routes to HumanVerificationRequired
- GIVEN an ingested image contains zero detected faces
- WHEN person detection runs
- THEN the system resolves a `HumanVerificationRequired` outcome for that image
- AND the image is not discarded or ignored
- AND identity verification is not attempted for that image

#### Scenario: Zero-face result is never silently ignored
- GIVEN person detection has completed with zero detected faces
- WHEN the workflow run continues
- THEN the system does not treat the zero-face result as "no person present and nothing to do"
