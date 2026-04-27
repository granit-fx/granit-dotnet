# Granit.Authentication.ApiKeys.BackgroundJobs

Background jobs for the Granit API keys module. Scans for keys approaching their
expiration date and emits `ApiKeyExpiringSoonEto` so tenant admins can rotate
the key on schedule.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.ApiKeys.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `apikeys-expiring-scan` | daily | Emits `ApiKeyExpiringSoonEto` for keys whose `ExpiresAt` falls within `ApiKeysOptions.ExpirationLeadTimeDays` (default 14). Per-key dedupe via `ApiKey.LastExpirationNotifiedAt` prevents same-week re-emission. |

Supports the least-privilege rotation policy expected by ISO 27001 A.9.4 — admins
get a proactive heads-up rather than discovering an expiration after a service
disruption.
