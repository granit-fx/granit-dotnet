# Granit.AI.Chat.BackgroundJobs

Background jobs for [`Granit.AI.Chat`](../Granit.AI.Chat) (ADR-067, GDPR data minimisation). Ships a
distributed, cluster-wide recurring job that purges conversations past the configured retention
window.

Retention is **opt-in**: with the default `RetentionDays` of 0 the job purges nothing.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat.BackgroundJobs
```

## Configuration

```jsonc
{
  "AI": {
    "Chat": {
      "Retention": {
        "RetentionDays": 365,   // 0 (default) disables purging
        "CleanupBatchSize": 500
      }
    }
  }
}
```

The job runs daily at 03:00 (override via `BackgroundJobs:Jobs:ai-chat-retention-cleanup`).

## Dependencies

- `Granit.AI.Chat`
- `Granit.BackgroundJobs`

## Documentation

See the [full documentation](https://granit-fx.dev).
