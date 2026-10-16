# incident-log Specification

## Purpose

Baseline spec (current behavior, derived from code on 2026-10-16). The incident log lets an operator manually record incident evidence, browse recent incidents, and view a filtered recent preview on Home. Documentation: `docs/features/incident-log.md`. Ticket: none (the catalog has no ticket for F3).

## Requirements

### Requirement: Load the incident history

The `/incidents` page MUST request at most 100 recent incidents when initialized and MUST show an inline refresh instruction if loading fails (`src/Enrollment.Web/Components/Pages/Incidents.razor:109-139`).

#### Scenario: Incident history loads
- GIVEN the operator opens `/incidents`
- WHEN the page initializes
- THEN the system SHALL request the 100 most recent incidents

#### Scenario: Incident history loading fails
- GIVEN the incident repository fails while `/incidents` is loading
- WHEN the page handles the failure
- THEN the system SHALL show "The incident list could not be loaded. Refresh the page to try again."

### Requirement: Submit a manual incident

The manual incident form MUST begin with kind `ManualNote`, offer incident kinds and an optional note with a visual limit of 1000 characters, and MUST NOT offer member or snapshot-image association (`src/Enrollment.Web/Components/Pages/Incidents.razor:16-39,114`; `src/Enrollment.Web/Components/Pages/Incidents.razor:28-30`). On submission, the page MUST ignore concurrent submissions while one is in progress and create an incident with a new ID, the current UTC time, the selected kind and note, and null member and snapshot-image values (`src/Enrollment.Web/Components/Pages/Incidents.razor:142-156`).

#### Scenario: Operator submits a manual note
- GIVEN the operator selects an incident kind and optionally enters a note
- WHEN the operator submits the form
- THEN the system SHALL save an incident with a new ID, current UTC time, the selected kind and note, and no member or snapshot-image association

#### Scenario: A submission is already in progress
- GIVEN a manual incident submission is in progress
- WHEN another submission is invoked
- THEN the system SHALL return without saving another incident

### Requirement: Complete or reject a manual incident submission

After a successful save, the page MUST clear the note, restore kind `ManualNote`, reload the list, and display "Incident recorded."; after a save failure, it MUST display "Could not record the incident. Please try again." and retain the submitted values (`src/Enrollment.Web/Components/Pages/Incidents.razor:157-167`).

#### Scenario: Manual incident save succeeds
- GIVEN the repository accepts the incident
- WHEN the submission completes
- THEN the system SHALL clear the note, restore the default kind, reload the list, and show confirmation

#### Scenario: Manual incident save fails
- GIVEN the repository fails to save the incident
- WHEN the page handles the failure
- THEN the system SHALL show the error message and preserve the entered note and selected kind

### Requirement: Persist and list incident records

The SQLite incident repository MUST save or replace a row by incident ID with its ID, occurrence time, kind, member ID, note, and snapshot-image path (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:104-127`). Recent-list requests with a non-positive limit MUST return an empty list without querying the database; other requests MUST return at most the requested number ordered by occurrence time descending, then row ID descending (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:132-163`).

#### Scenario: Save an incident with an existing ID
- GIVEN an incident row already exists with the same ID
- WHEN the repository saves another incident with that ID
- THEN the system SHALL replace the existing row with the supplied incident fields

#### Scenario: List recent incidents
- GIVEN multiple incidents exist
- WHEN the repository is asked for a positive limit
- THEN the system SHALL return no more than that limit, newest occurrence first, with later row IDs first for equal timestamps

#### Scenario: List with a non-positive limit
- GIVEN any number of incidents exist
- WHEN the repository is asked for a limit less than or equal to zero
- THEN the system SHALL return an empty list

### Requirement: Normalize incident values

Creating or rehydrating an incident MUST normalize its timestamp to UTC, trim nonblank optional note and snapshot-image-path values, and normalize null, empty, or whitespace-only optional values to null (`src/Enrollment.Domain/Incident.cs:48-63,66-91`).

#### Scenario: Normalize timestamp and optional strings
- GIVEN an incident is created with a non-UTC timestamp and padded optional text
- WHEN the domain creates the incident
- THEN the system SHALL store the timestamp in UTC and trim the optional text

#### Scenario: Normalize blank optional values
- GIVEN an incident is created with empty or whitespace-only optional values
- WHEN the domain creates the incident
- THEN the system SHALL store those optional values as null

### Requirement: Render the Home incident preview

The Home page MUST request at most 20 recent incidents and filter the loaded items in memory by exact incident-kind text and by case-insensitive note substring; an empty kind or blank note filter MUST not exclude items for that criterion (`src/Enrollment.Web/Components/Pages/Home.razor:95-108,110-116`). If loading fails, it MUST show the incident-list refresh instruction (`src/Enrollment.Web/Components/Pages/Home.razor:118-122`).

#### Scenario: Filter the Home preview
- GIVEN the Home page has loaded its recent incidents
- WHEN a kind and/or note filter is applied
- THEN the system SHALL show only items matching the exact kind and case-insensitive note substring criteria

#### Scenario: Home preview loading fails
- GIVEN loading the Home incident preview fails
- WHEN the page handles the failure
- THEN the system SHALL show "The incident list could not be loaded. Refresh the page to try again."

### Requirement: Reject corrupt persisted incident identifiers and kinds

When reading a persisted incident row, the repository MUST throw `InvalidOperationException` rather than return an incident if its ID or member ID is corrupt or its kind is unknown (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:192-239`).

#### Scenario: Persisted incident row has an invalid identifier or kind
- GIVEN a stored incident row contains an invalid incident ID, member ID, or kind
- WHEN the repository reads the row
- THEN the system SHALL throw `InvalidOperationException`

### Requirement: Keep incidents as evidence, not access decisions

An incident MUST represent evidence of an event and MUST NOT itself grant, deny, or revoke access (`src/Enrollment.Domain/Incident.cs:6-9`).

#### Scenario: Incident is recorded
- GIVEN an incident is created or stored
- WHEN access is evaluated
- THEN the incident itself SHALL NOT grant, deny, or revoke access
