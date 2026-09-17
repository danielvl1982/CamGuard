// Thin entry point: composes Enrollment + Monitoring and runs MonitoringHostedService for the
// process lifetime. All watch/workflow/archive logic lives in MonitoringHostedService
// (testable, injectable) — per dotnet-testable-code-design this Main never holds business logic.
using Enrollment.Infrastructure;
using Microsoft.Extensions.Hosting;
using Monitoring.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddEnrollment(builder.Configuration);
builder.Services.AddMonitoring(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
