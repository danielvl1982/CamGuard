# Identity Verification Specification

## Purpose

Current behavior is documented in [docs/features/identity-verification.md](../../../docs/features/identity-verification.md).

Defines how a detected face's embedding is checked against the enrolled member directory via `IMemberDirectoryReader`, and how the returned `Score` field MUST be interpreted as cosine DISTANCE (lower value = closer match), never as a similarity score. Decision thresholds and outcome resolution belong to `access-decision-policy`; this capability is responsible for querying and correctly forwarding the distance.

## Requirements

### Requirement: Directory Query On Detected Face
For each detected face's embedding, the system MUST query `IMemberDirectoryReader` and MUST forward the returned result (matched `MemberId` and `Score`, or an explicit no-match) to the access-decision policy.

#### Scenario: Detected face triggers a directory query
- GIVEN person detection produced a face embedding
- WHEN identity verification runs
- THEN the system queries `IMemberDirectoryReader` with that embedding
- AND forwards the returned result to access-decision-policy

### Requirement: Distance Interpretation, Never Similarity
The system MUST interpret `MemberMatch.Score` as cosine distance, where a LOWER value indicates a closer match. The system MUST NOT interpret, label, or present this value as a similarity score, and MUST NOT invert its ordering.

#### Scenario: Known member matched within the allowed distance
- GIVEN `IMemberDirectoryReader` returns a `MemberMatch` whose `Score` is a low distance value close to the query embedding
- WHEN identity verification completes
- THEN the system forwards that `MemberId` and distance value, correctly labeled as distance, for access-decision-policy to evaluate as a close match

#### Scenario: Unrecognized face yields no usable match
- GIVEN `IMemberDirectoryReader` returns either an explicit no-match result or a `MemberMatch` with a high distance value
- WHEN identity verification completes
- THEN the system forwards the result as an unrecognized/high-distance outcome
- AND the system does not present the high distance value as a low similarity or otherwise invert its meaning

### Requirement: Exact Distance Passthrough
The system MUST forward the raw distance value returned by `IMemberDirectoryReader` to access-decision-policy without rounding, truncation, or unit conversion, because threshold boundary evaluation depends on the exact value.

#### Scenario: Boundary distance value is passed through unmodified
- GIVEN `IMemberDirectoryReader` returns a `Score` exactly equal to a configured decision threshold value
- WHEN identity verification forwards the result
- THEN the forwarded distance value is bit-for-bit identical to the value returned by `IMemberDirectoryReader`
