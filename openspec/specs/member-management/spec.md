# member-management Specification

## Purpose

Baseline spec (current behavior, derived from code on 2026-10-16). The member-management capability supports enrolling, listing, viewing, updating, annulling members, managing their images, and retrieving member images over HTTP. Documentation: `docs/features/member-management.md`. Ticket: none.

## Requirements

### Requirement: List active members and roles

The system SHALL display members that are not annulled and their role names, loading the member and role data for the list (`src/Enrollment.Web/Components/Pages/Members.razor:223-232`).

#### Scenario: Load the members page
- GIVEN members exist, including annulled members, and roles are available
- WHEN the members page loads
- THEN the system SHALL show non-annulled members with their role names

### Requirement: Register a member with reference images

The system SHALL create a registration command from the entered name and selected image streams, execute it in a new dependency-injection scope, and process the command result (`src/Enrollment.Web/Components/Pages/Members.razor:372-379`).

#### Scenario: Submit member registration
- GIVEN a name and selected reference images
- WHEN the user submits registration
- THEN the system SHALL execute `RegisterMemberCommand` with the name and image streams and process its result

### Requirement: Annul a member logically

The system SHALL annul a member through `SetAnnulledAsync(id, true, ...)` and reload the list; persistence SHALL retain the member record and update its annulled state without replacing its identity, name, embedding, or role (`src/Enrollment.Web/Components/Pages/Members.razor:480-495`; `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:149-160`).

#### Scenario: Annul an existing member
- GIVEN a member exists
- WHEN the user annuls that member
- THEN the system SHALL mark the member annulled without deleting the record and reload the list

#### Scenario: Annul an unknown member
- GIVEN no member exists with the requested identifier
- WHEN the repository annuls that identifier
- THEN the system SHALL throw `KeyNotFoundException` (`src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:140-156`)

### Requirement: Load member details

The system SHALL load the selected member's images and the available roles for the detail view (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:349-353`).

#### Scenario: Open member detail
- GIVEN a member has images and roles are configured
- WHEN the detail page loads
- THEN the system SHALL display the member's images and available roles

### Requirement: Rename a member

The system SHALL submit `RenameMemberCommand` with the member identifier and entered name, then reread the member and display the persisted name (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:413-429`).

#### Scenario: Save a member name
- GIVEN a member is open in the detail view
- WHEN the user saves a new name
- THEN the system SHALL submit the rename command and display the name read back from persistence

### Requirement: Assign or remove a member role

The system SHALL accept a valid role GUID or an empty selection to remove the role; it SHALL not assign a role that no longer exists (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:462-468`; `src/Enrollment.Domain/IMemberVectorRepository.cs:27-35`).

#### Scenario: Assign an existing role
- GIVEN the selected role value is a valid GUID and identifies an existing role
- WHEN the user saves the role
- THEN the system SHALL assign that role to the member

#### Scenario: Remove the member role
- GIVEN the role selection is empty
- WHEN the user saves the role
- THEN the system SHALL remove the member's role assignment

#### Scenario: Reject an invalid role value
- GIVEN the selected role value is not a valid GUID or does not form an accepted `RoleId`
- WHEN the user saves the role
- THEN the interface SHALL display “Choose a valid role.” (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:462-468`)

#### Scenario: Role no longer exists
- GIVEN the selected role does not exist when assignment is attempted
- WHEN the assignment operation runs
- THEN the system SHALL not assign that role and the interface SHALL process the operation result (`src/Enrollment.Domain/IMemberVectorRepository.cs:27-35`; `src/Enrollment.Web/Components/Pages/MemberDetail.razor:469-504`)

#### Scenario: Concurrent role update overlaps a full-record write
- GIVEN a role change overlaps a concurrent `RenameAsync` or `SetAnnulledAsync` full-record read/write
- WHEN both operations persist their changes
- THEN the role change MAY be reverted by the other operation's full-record write (`src/Enrollment.Domain/IMemberVectorRepository.cs:32-33`)

### Requirement: Add member images

The system SHALL create a new dependency-injection scope, execute `AddMemberImagesCommand` for the selected images, and process its result (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:694-702`).

#### Scenario: Add images to a member
- GIVEN a member is open and one or more images are selected
- WHEN the user adds the images
- THEN the system SHALL execute `AddMemberImagesCommand` and process its result

### Requirement: Delete an image belonging to the displayed member

The system SHALL reload the member's image list and verify that the selected image belongs to the displayed member before deleting it (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:528-543`).

#### Scenario: Delete a member image
- GIVEN an image appears in the displayed member's image list
- WHEN the user confirms deletion
- THEN the system SHALL verify the image belongs to that member and delete it

#### Scenario: Reject an image not belonging to the displayed member
- GIVEN the selected image is not in the displayed member's reloaded image list
- WHEN deletion is requested
- THEN the system SHALL not delete that image

### Requirement: Retrieve a member image over HTTP

The system SHALL expose `GET /api/members/{id:guid}/images/{imageId:guid}` and return the image stream when both the member and image are found, or 404 when the requested records cannot be retrieved (`src/Enrollment.Web/MemberImageEndpoints.cs:22,37-68`).

#### Scenario: Retrieve an existing member image
- GIVEN the member and its requested image exist
- WHEN a client sends `GET /api/members/{id:guid}/images/{imageId:guid}`
- THEN the system SHALL return the image stream

#### Scenario: Retrieve a missing member image
- GIVEN the member or requested image cannot be found
- WHEN a client requests that image endpoint
- THEN the system SHALL return 404
