# home-dashboard Specification

## Purpose

Baseline spec (current behavior, derived from code on 2026-10-16). The home dashboard provides member and role navigation plus a filterable preview of recent incidents. Documentation: `docs/features/home-dashboard.md`. Ticket: none (F5).

## Requirements

### Requirement: Home dashboard entry and navigation

The system SHALL serve the home dashboard at `/` using the `InteractiveServer` render mode and provide links to `/members`, `/roles`, and `/incidents` (`src/Enrollment.Web/Components/Pages/Home.razor:1-2,18-33`).

#### Scenario: Open the home dashboard
- GIVEN a user requests `/`
- WHEN the page renders
- THEN the system SHALL display the home dashboard with links to the member, role, and full incident-list pages

### Requirement: Recent incident preview loading

The system SHALL create a service scope during initialization, resolve `IIncidentRepository`, and request at most 20 recent incidents (`src/Enrollment.Web/Components/Pages/Home.razor:94-95,110-116`). The scope SHALL be disposed after the load operation (`src/Enrollment.Web/Components/Pages/Home.razor:112-116`).

#### Scenario: Load recent incidents
- GIVEN the home page is initializing
- WHEN the incident preview is loaded
- THEN the system SHALL request the 20 most recent incidents through `IIncidentRepository.ListRecentAsync`

### Requirement: Local incident filtering

The system SHALL filter the loaded preview in memory by exact textual `IncidentKind` equality and by a note substring match that ignores case; an empty kind or whitespace-only note filter SHALL not restrict results (`src/Enrollment.Web/Components/Pages/Home.razor:100-108`).

#### Scenario: Apply kind and note filters
- GIVEN incidents have been loaded
- WHEN a user selects a kind and enters note text
- THEN the system SHALL show only incidents whose kind text exactly matches the selected kind and whose note contains the entered text without case sensitivity

#### Scenario: Clear or omit filters
- GIVEN incidents have been loaded
- WHEN the kind filter is empty or the note filter contains only whitespace
- THEN that filter SHALL not restrict the displayed incidents

### Requirement: Incident preview states and display

The system SHALL show a loading status while the request is in progress, an error alert if loading fails, an empty status if no incidents match, or a table of matching incidents otherwise (`src/Enrollment.Web/Components/Pages/Home.razor:52-90,97-99,123-126`). The table SHALL display UTC date/time, kind, and note, using an em dash for a null note (`src/Enrollment.Web/Components/Pages/Home.razor:69-85`).

#### Scenario: Show matching incidents
- GIVEN incident loading completed successfully and filters match one or more incidents
- WHEN the preview renders
- THEN the system SHALL show a table with UTC date/time, kind, and note columns

#### Scenario: Show no matches
- GIVEN incident loading completed successfully and no incidents match the filters
- WHEN the preview renders
- THEN the system SHALL show “No incidents match the current filter.”

#### Scenario: Show loading and load failure
- GIVEN the incident request is pending or throws an exception
- WHEN the preview renders
- THEN the system SHALL show “Loading incidents…” while pending and SHALL show an error alert instructing the user to refresh after failure

#### Scenario: Display a null note
- GIVEN a matching incident has a null note
- WHEN its row renders
- THEN the system SHALL display an em dash in the Note column
