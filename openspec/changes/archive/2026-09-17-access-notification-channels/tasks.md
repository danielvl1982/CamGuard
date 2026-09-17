# Tasks: Real Notification Channels for Access Events

**Implementation status**: already implemented, built, and tested directly in-session (uncommitted in the working tree) ahead of this SDD paper trail, per explicit user request. Every task below documents completed work — there is nothing left for `sdd-apply`.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~550–650 (5 new source files + 4 new test files + 5 modified files) |
| 400-line budget risk | Medium — likely exceeds the 400-changed-line review policy as a single PR |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (adapters: Ntfy + Email + options) → PR 2 (composite + composition-root wiring + host/config) → PR 3 (tests) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main (precedent from `monitoring-pipeline`, `ai-access-control`) |

Decision needed before apply: No — nothing left to apply.
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | `NtfyOptions`/`NtfyNotificationService`, `EmailNotificationOptions`/`EmailNotificationService`, `Directory.Packages.props` MailKit pin | PR 1 | `dotnet test --filter NtfyNotificationService\|EmailNotificationService` | N/A — no host boundary yet | Delete the four new adapter/options files, revert `Directory.Packages.props` |
| 2 | `CompositeNotificationService`, `AddMonitoring`'s `IHostEnvironment` branch, `Program.cs`/`appsettings.json` | PR 2 | `dotnet test --filter CompositeNotificationService\|NotificationChannelSelection\|AccessDecisionPolicy` | `dotnet build` + manual channel-selection smoke test in each environment | Revert `AddMonitoring`'s branch to always register `LoggingNotificationService`; delete `CompositeNotificationService.cs` |
| 3 | Tests (`CompositeNotificationServiceTests`, `EmailNotificationServiceTests`, `NtfyNotificationServiceTests`, `NotificationChannelSelectionTests`, `FakeHostEnvironment`) | PR 3 | `dotnet test --filter Monitoring` | N/A — unit-only | Delete the five new test files |

## Phase 1: Configuration Options (`src/Monitoring.Infrastructure`)

- [x] 1.1 Create `NtfyOptions.cs` — `SectionName = "Ntfy"`, `BaseUrl` (defaults to `https://ntfy.sh`), `Topic` (empty placeholder)
- [x] 1.2 Create `EmailNotificationOptions.cs` — `SectionName = "EmailNotification"`, `SmtpHost`/`SmtpPort` (587)/`FromAddress`/`ToAddress`/`Username`/`Password`, all empty placeholders; XML doc warns `Password` must never carry a real value in this file
- [x] 1.3 Add `Ntfy`/`EmailNotification` sections to `src/Monitoring.Host/appsettings.json`, values empty or non-secret defaults only

## Phase 2: Real Notification Adapters (`src/Monitoring.Infrastructure`)

- [x] 2.1 Pin `MailKit` to `4.16.0` in `Directory.Packages.props` (4.9.0/4.13.0 carry CVE-2026-41319) and reference it from `Monitoring.Infrastructure.csproj`
- [x] 2.2 Create `NtfyNotificationService.cs` : `INotificationService` — typed-client `HttpClient` (never `new HttpClient()`), POSTs to `{BaseUrl}/{Topic}`, maps `AccessOutcome` to ntfy `Priority`/`Tags` headers, throws `InvalidOperationException` fail-fast when `Topic` is empty
- [x] 2.3 Create `EmailNotificationService.cs` : `INotificationService` — MailKit `SmtpClient`, STARTTLS on `SmtpPort`, optional auth when `Username` set, throws `InvalidOperationException` fail-fast when `SmtpHost`/`FromAddress`/`ToAddress` incomplete

## Phase 3: Fan-Out and Channel Selection

- [x] 3.1 Create `CompositeNotificationService.cs` : `INotificationService` over `IEnumerable<INotificationService>` — per-channel try/catch, logs (`Error`) and continues to the next channel on failure, never rethrows
- [x] 3.2 Change `AddMonitoring`'s signature to `(services, configuration, environment)`; bind `NtfyOptions`/`EmailNotificationOptions`; register `AddHttpClient<NtfyNotificationService>()` and `TryAddScoped<EmailNotificationService>()`
- [x] 3.3 Branch `INotificationService` registration in `AddMonitoring`: `environment.IsDevelopment()` → `LoggingNotificationService` (unchanged); otherwise → `CompositeNotificationService` wrapping both real channels
- [x] 3.4 Update `src/Monitoring.Host/Program.cs` to pass `builder.Environment` into `AddMonitoring(...)`

## Phase 4: Tests (`tests/Monitoring.UnitTests`)

- [x] 4.1 Create `NtfyNotificationServiceTests.cs` — fake `HttpMessageHandler` asserts request URI/`Title`/`Priority` headers and body content; empty `Topic` throws `InvalidOperationException`
- [x] 4.2 Create `EmailNotificationServiceTests.cs` — theory over each incomplete-option combination (`SmtpHost`/`FromAddress`/`ToAddress`) throws `InvalidOperationException`
- [x] 4.3 Create `CompositeNotificationServiceTests.cs` — every channel is called; one channel throwing does not block the other; the failure is logged at `Error`
- [x] 4.4 Create `FakeHostEnvironment.cs` — minimal `IHostEnvironment` test double so `AddMonitoring` can be exercised without a full host
- [x] 4.5 Create `NotificationChannelSelectionTests.cs` — `AddMonitoring` resolves `LoggingNotificationService` in Development and `CompositeNotificationService` in Production
- [x] 4.6 Update `AccessDecisionPolicyTests.cs`'s `AddMonitoring` startup-validation tests to pass a `FakeHostEnvironment`, matching the new signature
- [x] 4.7 Add `FakeHostEnvironment` and `Moq` references as needed in `Monitoring.UnitTests.csproj`
- [x] 4.8 Verify `dotnet test --filter Monitoring` passes — 62/62

## Phase 5: Verification

- [x] 5.1 Verify no SMTP password or equivalent secret appears in `appsettings.json` or any file tracked by source control
- [x] 5.2 Run full `dotnet build` + `dotnet test` across the solution — `Monitoring.UnitTests` 62/62, `Enrollment.UnitTests` 32/32 pass
- [x] 5.3 Confirm `INotificationService`, `AccessAlert`, and `NotifyExecutor` remain unmodified — no new port, adapter-and-composition-root work only

## Phase 6: Remediation (closes `sdd-verify` CRITICAL finding)

- [x] 6.1 Close the `sdd-verify` CRITICAL finding for the "Committed configuration carries no secret" scenario (`No Delivery Secrets In Source Control` requirement): add `AppSettingsSecretsTests.cs`, which loads the committed `src/Monitoring.Host/appsettings.json` through `IConfiguration` and asserts `EmailNotification:Password` is empty, turning the prior manual-inspection-only compliance into an automated regression test. Wired via a `testdata/appsettings.json` link in `Monitoring.UnitTests.csproj` (same fixture-reuse pattern as `blank-image.png`).
