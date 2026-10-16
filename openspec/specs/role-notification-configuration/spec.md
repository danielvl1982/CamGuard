# role-notification-configuration Specification

## Purpose

Baseline spec (current behavior, derived from code on 2026-10-16). The `/roles` page manages roles and their notification channels, and configures default channels for denied or unknown access alerts. Documentation: `docs/features/role-notification-configuration.md`. Ticket: none.

## Requirements

### Requirement: Role management page

The system SHALL expose an interactive `/roles` page that loads and displays roles and default notification channels, with Push and Email presented as independent choices (`src/Enrollment.Web/Components/Pages/Roles.razor:1-2,14-18,193-197,260-274`).

#### Scenario: Open role management
- GIVEN the application is available
- WHEN a user opens `/roles`
- THEN the system SHALL load the role list and default notification channels and present Push and Email independently

### Requirement: Role names and role channels

The system SHALL trim role names and reject names that are empty or longer than 50 characters; it SHALL replace a role's channel set, collapse duplicate channels, allow an empty set, and reject undefined channel values (`src/Enrollment.Domain/Role.cs:11,37-52,64-94`).

#### Scenario: Create or update a role with valid name and channels
- GIVEN a role name is non-empty after trimming and no longer than 50 characters
- WHEN the role is created or updated with selected channels
- THEN the system SHALL store the trimmed name and replace the role's channel set

#### Scenario: Use an empty channel set
- GIVEN a role is being created or updated
- WHEN no notification channels are selected
- THEN the system SHALL accept the empty channel set

#### Scenario: Reject invalid role input
- GIVEN a role name is empty after trimming, exceeds 50 characters, or a channel value is undefined
- WHEN the role is created or updated
- THEN the system SHALL reject the input

### Requirement: Unique role names

The system SHALL reject a role name already used by another role, including a uniqueness collision reported while saving (`src/Enrollment.Application/CreateRoleCommand.cs:10-48`; `src/Enrollment.Application/UpdateRoleCommand.cs:27-50`).

#### Scenario: Name conflicts with another role
- GIVEN another role already uses the requested name
- WHEN a user creates or renames a role
- THEN the system SHALL reject the operation as a duplicate-name error

#### Scenario: Name conflicts during persistence
- GIVEN no duplicate was found by the initial lookup
- WHEN persistence reports a uniqueness collision
- THEN the system SHALL return a typed duplicate-name error

### Requirement: Role creation and editing

The system SHALL create roles from the submitted name and channels, and SHALL update a role by replacing its name and channel set (`src/Enrollment.Web/Components/Pages/Roles.razor:287-301,314-445`; `src/Enrollment.Application/CreateRoleCommand.cs:10-48`; `src/Enrollment.Application/UpdateRoleCommand.cs:13-53`).

#### Scenario: Create a role successfully
- GIVEN valid role input
- WHEN a user submits the create form
- THEN the system SHALL create the role and refresh the displayed list

#### Scenario: Edit a role successfully
- GIVEN an existing role and its current values are loaded in the edit form
- WHEN a user saves a valid name and channel selection
- THEN the system SHALL replace both values and refresh the displayed list

### Requirement: Prevent deletion of assigned roles

The system SHALL reject deletion when any member, including an inactive member, is assigned the role (`src/Enrollment.Application/DeleteRoleCommand.cs:6-23`).

#### Scenario: Delete an unassigned role
- GIVEN no member is assigned the role
- WHEN a user confirms deletion
- THEN the system SHALL delete the role

#### Scenario: Delete an assigned role
- GIVEN at least one member, including an inactive member, is assigned the role
- WHEN a user confirms deletion
- THEN the system SHALL preserve the role and report that it must first be removed from members (`src/Enrollment.Web/Components/Pages/Roles.razor:151-165,447-517`)

### Requirement: Default notification channels

The system SHALL return all channels when no default preferences have ever been saved; saving defaults SHALL replace the complete persisted set, accept only defined channels, and allow an empty set to mean no notifications for denied or unknown access (`src/Enrollment.Domain/INotificationSettingsRepository.cs:6-13`; `src/Enrollment.Application/SetDefaultChannelsCommand.cs:6-26`; `tests/Enrollment.UnitTests/SqliteNotificationSettingsRepositoryTests.cs:23-60`).

#### Scenario: Read defaults before preferences are saved
- GIVEN no default notification preferences have been saved
- WHEN the system reads default channels
- THEN the system SHALL return all notification channels

#### Scenario: Save an empty default set
- GIVEN default notification preferences are being changed
- WHEN the user saves no channels
- THEN the system SHALL persist the empty set and indicate that denied and unknown access will not generate notifications (`src/Enrollment.Web/Components/Pages/Roles.razor:519-552`)

#### Scenario: Reject undefined default channel
- GIVEN a submitted default channel value is undefined
- WHEN the system saves default notification preferences
- THEN the system SHALL reject the value without saving it

### Requirement: Report operation and loading outcomes

The system SHALL report operation failures and successes in the `/roles` UI; failed creation SHALL preserve the entered form values, and default-channel controls SHALL remain disabled until their load completes (`src/Enrollment.Web/Components/Pages/Roles.razor:202-213,262-284,330-364,409-445,484-517,535-546`).

#### Scenario: Create operation fails
- GIVEN a user has entered role form values
- WHEN role creation fails
- THEN the system SHALL preserve the entered values and display the corresponding message

#### Scenario: Role or preferences loading fails
- GIVEN the `/roles` page is loading roles or default preferences
- WHEN loading fails
- THEN the system SHALL log the error and display a loading error message, with default-channel controls disabled until loading completes
