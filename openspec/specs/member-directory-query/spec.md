# Member Directory Query Specification

## Purpose

Current behavior is documented in [docs/features/member-directory-query.md](../../../docs/features/member-directory-query.md).

Defines the public-API contract `IMemberDirectoryReader` exposed by the `Enrollment` module: given a face embedding, deterministically return the closest enrolled member and similarity score, or an explicit no-match result. This contract is consumed later by the deferred `monitoring-pipeline` change; decision-policy behavior (Allowed/Denied/HumanVerificationRequired) is out of scope here.

## Requirements

### Requirement: Query By Embedding
The system MUST expose a query operation that accepts a face embedding and returns the single closest enrolled member together with its similarity score.

#### Scenario: Query returns the closest enrolled member
- GIVEN one or more members are enrolled with persisted embeddings
- WHEN a query is submitted with a face embedding
- THEN the system returns the enrolled member whose stored embedding has the highest similarity to the query embedding
- AND the returned result includes the similarity score

### Requirement: Deterministic Matching
The system MUST compute similarity using plain deterministic vector comparison, and MUST NOT use a non-deterministic or model-arbitrated process to select the match.

#### Scenario: Repeated query yields identical result
- GIVEN the vector store's enrolled-member state is unchanged
- WHEN the same face embedding is queried twice
- THEN both queries return the same matched member and the same similarity score

### Requirement: Explicit No-Match Result
The system MUST return an explicit no-match result, distinct from any member match, when no member is enrolled in the vector store.

#### Scenario: Query against an empty directory
- GIVEN no members are enrolled in the vector store
- WHEN a query is submitted with a face embedding
- THEN the system returns an explicit no-match result
- AND the result does not reference any member identity

### Requirement: Verified Round-Trip
The system MUST return the same member for a query embedding extracted from a member's own enrolled reference photo.

#### Scenario: Query with a known member's own embedding
- GIVEN a member was enrolled from a reference photo
- WHEN a query is submitted using the embedding extracted from that same photo
- THEN the returned match identifies that same member

### Requirement: Public API Boundary
The system MUST expose this query operation only through the `IMemberDirectoryReader` port, and MUST NOT require a consumer to access Enrollment's internal persistence (vector store, `DbContext`, or repository types) directly.

#### Scenario: Consumer queries without internal access
- GIVEN a consuming module holds only a reference to `IMemberDirectoryReader`
- WHEN it invokes the query operation
- THEN it receives a match-or-no-match result without needing any other Enrollment type
