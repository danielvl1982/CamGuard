# CamGuard: architecture and monitoring workflow

This training diagram shows how member enrollment supports camera monitoring, how an image moves through the access decision, and where notifications and images go. The relationship view is conceptual; it is not a database schema.

## 1. Component map

```mermaid
flowchart LR
    Operator[Operator]
    Camera[Camera or image source]
    subgraph Enrollment["Enrollment module"]
        Web[Enrollment web UI]
        App[Enrollment application<br/>commands and queries]
        Domain[Enrollment domain<br/>Member, FaceEmbedding, Role]
        Ports[Enrollment public API<br/>member directory and notification routing]
        Store[Enrollment persistence adapters]
        Extract[Face embedding extractor]
        Web --> App
        App --> Domain
        Store -. implements domain ports .-> Domain
        Extract --> App
        App --> Ports
    end
    subgraph Monitoring["Monitoring module"]
        Watch[Folder ingestion and hosted service]
        Workflow[Monitoring workflow]
        Detect[Person detection and embedding]
        Verify[Member identity lookup]
        Decide[Access decision policy]
        Notify[Alert, pending verification, incident]
        Channels[Notification channel adapters]
        Archive[Processed or quarantine folders]
        Watch --> Workflow
        Workflow --> Detect --> Verify --> Decide --> Notify
        Detect -->|zero-face short-circuit| Notify
        Notify --> Channels
        Workflow --> Archive
    end
    Operator --> Web
    Camera --> Watch
    Verify -->|IMemberDirectoryReader| Ports
    Notify -->|role/default channel lookup| Ports
```

**How to read it:** Enrollment creates the member and reference embedding used by later recognition. Monitoring owns the incoming-image loop and decision workflow. It asks Enrollment through the public API for the closest enrolled member; it does not reach into Enrollment's infrastructure implementation.

## 2. Conceptual relationship map

```mermaid
erDiagram
    ROLE o|--o{ MEMBER : assigned_to
    MEMBER ||--|| FACE_EMBEDDING : enrolled_with
    MEMBER o|--o{ ACCESS_ATTEMPT : candidate_for
    ACCESS_ATTEMPT ||--|| ACCESS_DECISION : resolves_to
    ROLE ||--o{ ROLE_CHANNEL : selects
    NOTIFICATION_CHANNEL ||--o{ ROLE_CHANNEL : included_in
    DEFAULT_SETTINGS ||--o{ DEFAULT_CHANNEL : selects
    NOTIFICATION_CHANNEL ||--o{ DEFAULT_CHANNEL : included_in

    MEMBER {
        id MemberId
        string FullName
        bool IsAnnulled
        id RoleId_optional
    }
    FACE_EMBEDDING {
        float_vector Vector
    }
    ROLE {
        id RoleId
        string Name
    }
    NOTIFICATION_CHANNEL {
        enum Kind
    }
    ACCESS_ATTEMPT {
        string ImageReference
        id CandidateMemberId_optional
    }
    ACCESS_DECISION {
        enum Outcome
        float Distance_optional
        string Reason
    }
    ROLE_CHANNEL {
        id RoleId
        enum Channel
    }
    DEFAULT_SETTINGS {
        string Purpose
    }
    DEFAULT_CHANNEL {
        enum Channel
    }
```

`ACCESS_ATTEMPT`, `ACCESS_DECISION`, and the channel link records are teaching concepts for the relationships in the flow. This diagram does not assert that each one is a persisted table. A member may have no role; in that case notification routing uses the default channels.

## 3. One image through the workflow

```mermaid
flowchart TD
    A[Image arrives in watched folder] --> B[Hosted service opens image]
    B --> C[Detect person and create face embedding]
    C --> D{Usable face present?}
    D -->|No: none or no usable landmarks| E[Set HumanVerificationRequired<br/>reason: No face detected]
    E --> J[Build AccessAlert]
    D -->|Yes| F[Search Enrollment member directory]
    F --> G{Candidate match returned?}
    G -->|No: distance is null| H[Decision: Denied]
    G -->|Yes: distance returned| I[Apply configured access decision policy]
    I --> K{Policy outcome}
    K -->|Allowed| L[Decision: Allowed]
    K -->|Denied| H
    K -->|Gray zone| M[Decision: HumanVerificationRequired]
    H --> J
    L --> J
    M --> J
    J --> N{Allowed with member ID?}
    N -->|Yes| O[Use member role channels]
    N -->|No| P[Use default channels]
    O --> Q[Send alert through selected channels]
    P --> Q
    Q --> R{Human verification required?}
    R -->|Yes| S[Save pending verification]
    R -->|No| T[Continue]
    S --> U[Record incident signal]
    T --> U
    U --> V{Workflow completed?}
    V -->|Yes| W[Move image to processed]
    V -->|No or failed| X[Move image to quarantine]
```

### Decision notes

- **No usable face:** detection sets a terminal human-verification decision. The graph goes directly to notification and skips identity lookup and the access policy.
- **No member match:** a missing distance means no enrolled member matched; the decision is denied without calling the distance policy.
- **Matched member:** the configured policy maps the returned distance to Allowed, Denied, or HumanVerificationRequired. The actual numeric thresholds come from configuration and are intentionally not duplicated here.
- **Notification routing:** only an Allowed alert with a member ID uses that member's role channels. Other outcomes use default channels. If a member-channel lookup fails, routing falls back to defaults; if that lookup also fails, the service tries every configured channel. One channel failure does not prevent the others.
- **File handling:** a completed workflow moves the image to processed. A false result or an unhandled workflow error moves it to quarantine. Thrown workflow errors are retried up to three times with a fresh image stream; a successful run returning false is quarantined without retry.
- **Failure isolation:** notification and incident-sink failures are logged and do not fail the workflow run. Human-verification outcomes are saved as pending verifications.

## Source anchors

- Monitoring graph and its short-circuit edge: `src/Monitoring.Application/MonitoringWorkflowHost.cs`
- Detection and zero-face decision: `src/Monitoring.Application/DetectPersonExecutor.cs`
- Identity match: `src/Monitoring.Application/VerifyIdentityExecutor.cs`
- Null-distance behavior: `src/Monitoring.Application/DecideAccessExecutor.cs`
- Alert, pending verification, and incident signal: `src/Monitoring.Application/NotifyExecutor.cs`
- Watch, retries, and archive/quarantine: `src/Monitoring.Infrastructure/MonitoringHostedService.cs`
- Member/role/default channel routing: `src/Monitoring.Infrastructure/CompositeNotificationService.cs`, `src/Enrollment.PublicApi/NotificationRoutingReader.cs`
- Member, role, and enrollment concepts: `src/Enrollment.Domain/Member.cs`, `src/Enrollment.Domain/Role.cs`, `src/Enrollment.Domain/FaceEmbedding.cs`

Open this file in a Mermaid-enabled Markdown viewer to render the diagrams.
