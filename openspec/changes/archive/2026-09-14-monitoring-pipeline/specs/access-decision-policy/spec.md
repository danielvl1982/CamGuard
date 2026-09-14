# Access Decision Policy Specification

## Purpose

Defines the deterministic access decision made from a forwarded cosine-distance value: two named, configured distance thresholds (`AllowedMaxDistance`, `DeniedMinDistance`) partition the distance range into exactly three outcomes (`Allowed`, `Denied`, `HumanVerificationRequired`). Also defines mandatory startup validation of threshold configuration.

## Requirements

### Requirement: Two Named Distance Thresholds
The system MUST expose exactly two named, independently configured distance thresholds: `AllowedMaxDistance` and `DeniedMinDistance`. The system MUST NOT use any other naming (e.g. "score" or "similarity") for these values.

#### Scenario: Thresholds are independently configurable
- GIVEN an access-decision configuration source
- WHEN `AllowedMaxDistance` and `DeniedMinDistance` are bound
- THEN each is read as its own independent distance value

### Requirement: Three-Outcome Deterministic Decision
Given a distance value, the system MUST deterministically resolve exactly one of three outcomes: `Allowed` when the distance is at or below `AllowedMaxDistance`; `Denied` when the distance is at or above `DeniedMinDistance`; `HumanVerificationRequired` when the distance is strictly between the two thresholds. The system MUST NOT produce any other outcome value.

#### Scenario: Distance at or below AllowedMaxDistance resolves Allowed
- GIVEN a distance value that is less than or equal to `AllowedMaxDistance`
- WHEN the access decision is evaluated
- THEN the outcome is `Allowed`

#### Scenario: Distance at or above DeniedMinDistance resolves Denied
- GIVEN a distance value that is greater than or equal to `DeniedMinDistance`
- WHEN the access decision is evaluated
- THEN the outcome is `Denied`

#### Scenario: Distance strictly between thresholds resolves HumanVerificationRequired
- GIVEN a distance value strictly greater than `AllowedMaxDistance` and strictly less than `DeniedMinDistance`
- WHEN the access decision is evaluated
- THEN the outcome is `HumanVerificationRequired`

### Requirement: Startup Threshold Validation
The system MUST validate, at startup or options-binding time, that `AllowedMaxDistance` is strictly less than `DeniedMinDistance`. The system MUST fail fast (prevent the host from starting) when this invariant is violated, and MUST NOT silently accept an invalid configuration.

#### Scenario: Valid threshold configuration starts normally
- GIVEN `AllowedMaxDistance` is configured strictly less than `DeniedMinDistance`
- WHEN the host starts
- THEN startup validation passes and the host starts normally

#### Scenario: Invalid threshold configuration fails fast
- GIVEN `AllowedMaxDistance` is configured greater than or equal to `DeniedMinDistance`
- WHEN the host starts
- THEN startup validation fails
- AND the host does not start
