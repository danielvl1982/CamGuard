# Design: Real Notification Channels for Access Events

## Technical Approach

Two new `INotificationService` adapters (`NtfyNotificationService`, `EmailNotificationService`) plus one fan-out adapter (`CompositeNotificationService`) in `Monitoring.Infrastructure`. `MonitoringServiceCollectionExtensions.AddMonitoring` gains an `IHostEnvironment` parameter and branches: `environment.IsDevelopment()` → `LoggingNotificationService` (unchanged placeholder); otherwise → `CompositeNotificationService` wrapping both real channels. `INotificationService`, `AccessAlert`, and `NotifyExecutor` are untouched — this is adapter-and-composition-root work only, no new port.

## Architecture Decisions

### Decision: Runtime channel selection via `IHostEnvironment`, not `#if DEBUG`

| Option | Tradeoff |
|---|---|
| `#if DEBUG` compile-time symbol | Dead-code-eliminates the untaken branch at compile time; cannot be toggled without recompiling; conflates *build configuration* (Debug/Release) with *runtime environment* (Development/Staging/Production) — a Release build run locally would still get real channels |
| `IHostEnvironment.IsDevelopment()` | Runtime/config-driven (`ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`), consistent with how `AddMonitoring` already closes over `IConfiguration` directly in the composition root rather than resolving it from the container |

**Choice**: `IHostEnvironment`, injected as an explicit `AddMonitoring(services, configuration, environment)` parameter — never resolved from the container mid-registration, matching the existing `configuration` parameter style.
**Rationale**: `#if DEBUG` is a build-time symbol; it cannot express "same binary, different environment," which is exactly what separates a developer's machine from a deployed instance. `dotnet-composition-root`'s existing pattern (closing over dependencies passed into the extension method) extends naturally to `environment`.

### Decision: `CompositeNotificationService` as the failure-isolation boundary

**Choice**: `CompositeNotificationService` implements `INotificationService` over `IEnumerable<INotificationService>` (the two real adapters), wrapping each channel's `NotifyAsync` in its own try/catch and logging (`Error`) any exception without rethrowing.
**Alternatives considered**: relying solely on `NotifyExecutor`'s own outer try/catch around the whole `INotificationService.NotifyAsync` call (pre-existing, unchanged).
**Rationale**: `NotifyExecutor`'s try/catch protects the *workflow run* (archival must proceed regardless of notification outcome) but treats notification as one opaque call — if ntfy throws, email never runs. `CompositeNotificationService` isolates *per-channel* failure so one misconfigured channel (e.g. bad SMTP credentials) never blocks a working one. The two boundaries are distinct and complementary: Composite isolates channel-to-channel, `NotifyExecutor` isolates notification-to-archival. This maps directly to `csharp-gof-pattern-catalog`'s Composite-over-`IEnumerable<T>` shape (interchangeable implementations, all applied together, DI-resolved as a collection) rather than Strategy (only one selected at a time) or Decorator (wrapping, not fanning out).

### Decision: Fail-fast options validation, reused from `AccessDecisionOptions`'s convention

**Choice**: `NtfyNotificationService`/`EmailNotificationService` throw `InvalidOperationException` at `NotifyAsync` call time when required options (`Topic`; `SmtpHost`/`FromAddress`/`ToAddress`) are empty, rather than silently no-op-ing or sending malformed requests.
**Alternatives considered**: `IValidateOptions<T>` + fail-fast `IHostedService` at startup (the `dotnet-configuration-options` skill's preferred pattern for cross-field rules).
**Rationale**: `AccessDecisionOptions` already establishes call-time validation (`.Validate(...)` on `AddOptions<T>()`) as this codebase's convention for options misconfiguration. A full `IValidateOptions<T>` + startup-gate would be justified for cross-field rules enforced before any request runs; here the fields are independent per-adapter completeness checks, own by each adapter, consistent with the existing style rather than introducing a second validation mechanism for one change.

### Decision: Secrets via `dotnet user-secrets`/environment variables, never `appsettings.json`

**Choice**: `EmailNotificationOptions.Password` ships with an empty placeholder in `appsettings.json`; the real value is supplied via `dotnet user-secrets set EmailNotification:Password <value>` in development and an environment variable (`EmailNotification__Password`) in production.
**Rationale**: `dotnet-email-delivery`'s "treat recipients/credentials as sensitive" rule combined with standard .NET configuration layering — `IConfiguration` composes user-secrets/env vars over `appsettings.json` automatically, so no code change is needed to keep the password out of source control.

## Data Flow

    NotifyExecutor (unchanged)
         │  INotificationService.NotifyAsync(AccessAlert)
         ▼
    CompositeNotificationService  (non-Development only)
         │
         ├──→ NtfyNotificationService  ──try/catch──→ ntfy.sh HTTP POST
         └──→ EmailNotificationService ──try/catch──→ SMTP (STARTTLS)

    Development:
    NotifyExecutor ──→ LoggingNotificationService (unchanged)

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/Monitoring.Infrastructure/NtfyOptions.cs` | Create | `BaseUrl`/`Topic` options, `SectionName = "Ntfy"` |
| `src/Monitoring.Infrastructure/NtfyNotificationService.cs` | Create | Typed-client HTTP POST to ntfy.sh, priority/tag mapping from `AccessOutcome` |
| `src/Monitoring.Infrastructure/EmailNotificationOptions.cs` | Create | SMTP options, `SectionName = "EmailNotification"` |
| `src/Monitoring.Infrastructure/EmailNotificationService.cs` | Create | MailKit SMTP send over STARTTLS |
| `src/Monitoring.Infrastructure/CompositeNotificationService.cs` | Create | Fan-out with per-channel try/catch isolation |
| `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs` | Modify | `AddMonitoring` gains `IHostEnvironment`; binds new options; branches `INotificationService` registration |
| `src/Monitoring.Infrastructure/Monitoring.Infrastructure.csproj` | Modify | Adds MailKit 4.16.0 package reference |
| `src/Monitoring.Host/Program.cs` | Modify | Passes `builder.Environment` to `AddMonitoring` |
| `src/Monitoring.Host/appsettings.json` | Modify | `Ntfy`/`EmailNotification` placeholder sections (no real secret) |
| `Directory.Packages.props` | Modify | Pins MailKit to 4.16.0 (4.9.0/4.13.0 carry CVE-2026-41319) |
| `tests/Monitoring.UnitTests/*` | Create | Coverage listed below |

## Interfaces / Contracts

No new interface. Both adapters implement the existing `Monitoring.Application.INotificationService`:

```csharp
public interface INotificationService
{
    Task NotifyAsync(AccessAlert alert, CancellationToken ct);
}
```

`AddMonitoring`'s signature changes from `(services, configuration)` to `(services, configuration, environment)`:

```csharp
public static IServiceCollection AddMonitoring(
    this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `NtfyNotificationService` builds correct request (URI, headers, body) and throws on empty `Topic` | `NtfyNotificationServiceTests` with a fake `HttpMessageHandler` |
| Unit | `EmailNotificationService` throws on incomplete options; happy-path send | `EmailNotificationServiceTests` |
| Unit | `CompositeNotificationService` calls every channel; one channel throwing does not block the other; exception is logged | `CompositeNotificationServiceTests` with `AutoMocker`-style fakes, one configured to throw |
| Unit | `AddMonitoring` resolves `LoggingNotificationService` in Development, `CompositeNotificationService` in Production | `NotificationChannelSelectionTests` + `FakeHostEnvironment` |

## Threat Matrix

N/A — no routing, shell command execution, subprocess invocation, VCS/PR automation, executable-file classification, or process-integration boundary. Both adapters are outbound HTTP/SMTP calls to third-party services using configuration-sourced, non-user-supplied addresses.

## Migration / Rollout

No migration required (additive-only). Rollback: revert `AddMonitoring`'s environment branch to always register `LoggingNotificationService`, or delete the three new adapter files and their registrations — `INotificationService`/`AccessAlert`/`NotifyExecutor` are untouched either way.

## Open Questions

None — this change is already implemented, built, and tested (`Monitoring.UnitTests` 62/62, `Enrollment.UnitTests` 32/32 passing per the proposal).
