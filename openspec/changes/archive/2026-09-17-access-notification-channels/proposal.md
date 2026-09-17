# Proposal: Real Notification Channels for Access Events

## Intent

The archived `monitoring-pipeline` change shipped `INotificationService`/`AccessAlert` with the ONLY implementation being `LoggingNotificationService` — its own XML doc and the archived `access-notification` spec explicitly call it "a logging placeholder, not a real delivery channel," and the proposal's Out of Scope deferred real channels on purpose. This change closes that gap: deliver real, free-tier notification channels (push + email) for non-development environments, while keeping the logging placeholder for local development.

## Scope

### In Scope
- `NtfyNotificationService`: posts an `AccessAlert` to a configurable `ntfy.sh` topic over HTTP via `IHttpClientFactory` (free, no-account push).
- `EmailNotificationService`: sends the alert over SMTP via MailKit 4.16.0 (free via Gmail SMTP + App Password, 500/day).
- `CompositeNotificationService`: fans out to both real channels, isolating one channel's failure from the other.
- Runtime channel selection via `IHostEnvironment` in `AddMonitoring`: `LoggingNotificationService` when `environment.IsDevelopment()`, else `CompositeNotificationService` wrapping both real adapters.
- Configuration surfacing (`NtfyOptions`, `EmailNotificationOptions`) with empty placeholders in `appsettings.json`; SMTP password supplied via `dotnet user-secrets`, never committed.

### Out of Scope
- SMS delivery — no real free tier exists on any provider evaluated.
- A webhook or self-hosted API/SignalR channel — requires standing up and exposing an owned service, disproportionate infrastructure for personal/educational use.
- Any change to `INotificationService`, `AccessAlert`, or `NotifyExecutor` — the Application-layer port and its consumer were already correct and are untouched.

## Capabilities

### New Capabilities
None — no new port or consumer-facing contract is introduced; both new adapters implement the existing `INotificationService`.

### Modified Capabilities
- `access-notification`: the spec's "logging placeholder, not a real delivery channel" framing no longer holds for non-development environments; add channel-selection requirements (Development → logging, otherwise → composite ntfy+email fan-out with per-channel failure isolation).

## Approach

Two new `INotificationService` adapters in `Monitoring.Infrastructure` (`dotnet-email-delivery`: composition separated from delivery, failures isolated and logged, no secrets logged) plus one composite fan-out adapter. `MonitoringServiceCollectionExtensions.AddMonitoring` gains an `IHostEnvironment` parameter and branches registration on `environment.IsDevelopment()` (`dotnet-composition-root`: one extension method, `TryAddScoped`, typed `AddHttpClient<NtfyNotificationService>()`, Options bound via `configuration.GetSection(...).Bind(...)`). A `#if DEBUG` gate was considered and rejected — that is a compile-time build symbol, not a runtime signal, and would dead-code-eliminate the untaken branch instead of allowing environment/config-driven selection.

## Implementation Status

**Already implemented and merged into the working tree (uncommitted) directly in-session, per explicit user request**, ahead of this formal proposal. Spec/design/tasks/verify below describe and validate what was built, not a forward plan. Full solution build is clean; `Monitoring.UnitTests` 62/62 and `Enrollment.UnitTests` 32/32 pass.

## Affected Areas

| Area | Impact | Description |
|------|--------|--------------|
| `src/Monitoring.Infrastructure/NtfyNotificationService.cs`, `NtfyOptions.cs` | New | ntfy.sh HTTP push channel |
| `src/Monitoring.Infrastructure/EmailNotificationService.cs`, `EmailNotificationOptions.cs` | New | MailKit SMTP email channel |
| `src/Monitoring.Infrastructure/CompositeNotificationService.cs` | New | Fan-out with per-channel isolation |
| `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs` | Modified | `AddMonitoring` takes `IHostEnvironment`, branches channel registration |
| `src/Monitoring.Host/Program.cs` | Modified | Passes `builder.Environment` to `AddMonitoring` |
| `src/Monitoring.Host/appsettings.json` | Modified | `Ntfy`/`EmailNotification` placeholder sections |
| `Directory.Packages.props` | Modified | MailKit pinned to 4.16.0 (4.9.0/4.13.0 carry CVE-2026-41319) |
| `tests/Monitoring.UnitTests/*` | New | Coverage for both channels, composite fan-out, and selection |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| ntfy.sh public topics are guessable by anyone who knows the topic name | Medium | Document choosing a long random topic; personal/educational use accepted this tradeoff |
| Gmail SMTP 500/day cap or account lockout | Low | Personal-use volume is far below the cap; App Password scoped to SMTP only |
| MailKit CVE-2026-41319 in older versions | Low (mitigated) | Pinned to 4.16.0 in `Directory.Packages.props`, verified above the fixed version |

## Rollback Plan

Revert `AddMonitoring`'s environment branch to always register `LoggingNotificationService`, or delete the three new adapter files and their `AddMonitoring` registrations. `INotificationService`/`AccessAlert`/`NotifyExecutor` are untouched, so no caller-side change is needed either way.

## Dependencies

- MailKit 4.16.0 (new package reference).
- ntfy.sh public service (no package, HTTP-only).
- Gmail SMTP + App Password (user-provisioned, via `dotnet user-secrets`).

## Success Criteria

- [ ] In Development, `AddMonitoring` resolves `INotificationService` to `LoggingNotificationService`.
- [ ] Outside Development, it resolves to `CompositeNotificationService` wrapping ntfy + email.
- [ ] A failure in one real channel does not prevent the other from delivering.
- [ ] No SMTP credentials appear in `appsettings.json` or source control.
- [ ] `Monitoring.UnitTests` and `Enrollment.UnitTests` pass in full.
