# Granit.OpenIddict.BackgroundJobs

Background jobs for the Granit OpenIddict module. Automates expired token
cleanup and idle session enforcement.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `openiddict-token-cleanup` | `0 * * * *` (hourly) | Prunes expired tokens and authorizations |
| `openiddict-idle-session-enforcement` | `*/5 * * * *` (every 5 min) | Revokes refresh tokens for idle sessions |

Both jobs are concurrency-safe via Wolverine Outbox (single execution in multi-node).
Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Integration

Wire the module into your host's module graph so its `ConfigureServices` runs and
the jobs are registered:

```csharp
[DependsOn(typeof(GranitOpenIddictBackgroundJobsModule))]
public sealed class AppHostModule : GranitModule { }
```

The jobs are auto-registered during module initialization — there is no explicit
`Add*`/`Map*` call. Without this `[DependsOn]` edge the module is never loaded and
the cleanup/idle-enforcement jobs never run.

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.OpenIddict`
- `Granit.Settings`

## Documentation

See the [full documentation](https://granit-fx.dev).
