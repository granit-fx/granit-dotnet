# Granit.Auditing.Notifications

Notification bridge for `Granit.Auditing`. Routes audit anomaly signals (repeated
access-denied, role escalation, impersonation, privileged configuration tampering)
to platform administrators / SOC operators via Email + InApp, satisfying
**ISO 27001 A.12.4** logging-and-monitoring obligations by paging a human
responder proactively rather than relying on a silent log line.

Ships embedded HTML templates in **English and French** (overridable at runtime
through the `Granit.Templating` admin API).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Auditing.Notifications
```

## Dependencies

- `Granit.Auditing`
- `Granit.Notifications.Abstractions`
- `Granit.Templating`

## Notification types

| Name | Severity | Default channels |
| ---- | -------- | ---------------- |
| `auditing.anomaly_detected` | Warning | Email + InApp |

Recipients are resolved via the standard notifications subscription system —
administrators / on-call operators opt in through the admin UI; the bridge
itself owns no SOC-roster lookup logic.

## Configuration

The bridge subscribes to every `AuditEntryPersistedEto` and filters down to a
small, configurable set of categories:

```json
{
  "Auditing": {
    "Notifications": {
      "AlertableCategories": [ "AccessDenied", "ConfigurationChange" ]
    }
  }
}
```

Default: `AccessDenied` and `ConfigurationChange`. Authorization failures are
the canonical security signal (ISO 27001 A.9.4); configuration changes capture
privileged settings/feature-flag tampering that warrants a four-eyes review.

## Documentation

See the [full documentation](https://granit-fx.dev).
