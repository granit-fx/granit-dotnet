# Granit.Identity.Federated.Notifications

Notification bridge for `Granit.Identity.Federated`. Routes federated identity
audit events (token exchange / impersonation, GDPR Art. 17 user-deletion
receipts) to platform / tenant administrators via Email + InApp, satisfying
**ISO 27001 A.12.4** logging-and-monitoring obligations by paging a human
responder proactively rather than relying on a silent log line, and providing
a human-readable **GDPR Art. 17** erasure receipt that documents the
application-layer compliance step in addition to the upstream IdP deletion.

Ships embedded HTML templates in **English and French** (overridable at runtime
through the `Granit.Templating` admin API).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.Notifications
```

## Dependencies

- `Granit.Identity.Federated`
- `Granit.Notifications.Abstractions`
- `Granit.Templating`

## Notification types

| Name | Severity | Default channels | Trigger | Audience |
| ---- | -------- | ---------------- | ------- | -------- |
| `identity.token_exchange_audit` | Warning | Email + InApp | `IdentityTokenExchangedEto` (RFC 8693) | Platform admin / SOC |
| `identity.user_provisioning_removed` | Info | Email + InApp | `IdentityUserDeletedEto` | Tenant admin |

Recipients are resolved via the standard notifications subscription system —
administrators / on-call operators opt in through the admin UI; the bridge
itself owns no platform-admin or tenant-admin roster lookup logic.

### Why these two events

- **Token exchange audit** — RFC 8693 direct naked impersonation lets a service
  obtain a user-scoped access token without that user's interactive consent.
  ISO 27001 A.12.4 (logging and monitoring of privileged operations) requires
  every such operation to be observable in real time. The notification is the
  human-readable companion of the SIEM trail.
- **User-provisioning removal receipt** — when the IdP confirms a user
  deletion, the local user-cache entry is hard-deleted (GDPR Art. 17). The
  receipt makes the application-layer compliance step auditable and warns
  the tenant admin that downstream attribution (audit log, invoices) will
  start rendering the user as *"deleted user"*.

### Sync-failure events — intentionally not shipped

The Epic checklist also mentions an `identity.sync_failed` notification type for
IdP / cache drift. The federated identity layer does not currently emit a
distributed integration event on sync failure — the failure path
(`LogUserNotFoundInProvider` in `IdentityUserEventHandler`) only logs a
warning. Adding the notification would require introducing a new
`IdentityUserSyncFailedEto` in the core module, which is out of scope for this
bridge package (the bridge must not modify the federated identity core). When
the upstream Eto lands, this README and the package will be updated in a
follow-up to add the third notification type.

## Wolverine routing

Both notification triggers are **distributed** integration events
(`IIntegrationEvent`). Federated identity flows commonly span process / service
boundaries (an edge worker translating provider webhooks publishes to a queue,
the application API consumes). Wolverine routes these through the configured
transport — the handlers do not assume in-process delivery.

## Documentation

See the [full documentation](https://granit-fx.dev).
