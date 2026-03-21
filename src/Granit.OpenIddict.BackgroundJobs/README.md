# Granit.OpenIddict.BackgroundJobs

Background jobs for Granit.OpenIddict: automated token cleanup and idle session enforcement.

## Jobs

| Job | Cron | Description |
|-----|------|-------------|
| `openiddict-token-cleanup` | `0 * * * *` (hourly) | Prunes expired tokens and authorizations |
| `openiddict-idle-session-enforcement` | `*/5 * * * *` (every 5 min) | Revokes refresh tokens for idle sessions |

Both jobs are concurrency-safe via Wolverine Outbox (single execution in multi-node).
Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Usage

Add the module to your application — jobs are auto-scheduled:

```csharp
[DependsOn(typeof(GranitOpenIddictBackgroundJobsModule))]
public sealed class MyAppModule : GranitModule { }
```

Without this package, expired tokens accumulate (acceptable in dev; required in prod).
