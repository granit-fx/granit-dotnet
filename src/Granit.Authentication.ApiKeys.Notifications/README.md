# Granit.Authentication.ApiKeys.Notifications

Notification bridge for `Granit.Authentication.ApiKeys`. Alerts tenant administrators
on API key lifecycle events (creation, rotation, revocation) so they can manage
rotation cadence, catch out-of-band provisioning, and avoid service disruption from
silent key changes. Supports ISO 27001 A.9.2 (user access provisioning) and A.9.4.3
(secret authentication information management).

Ships embedded HTML templates in **English and French**, overridable at runtime
through the `Granit.Templating` admin API.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.ApiKeys.Notifications
```

## Dependencies

- `Granit.Authentication.ApiKeys`
- `Granit.Notifications`
- `Granit.Templating`

## Notification types

| Name | Default channels | Severity | Trigger |
| ---- | ---------------- | -------- | ------- |
| `apikeys.new_key_issued` | Email + InApp | Info | `ApiKeyCreatedEto` (new key created) |
| `apikeys.rotation_completed` | Email + InApp | Info | `ApiKeyRotatedEto` (key rotated; old key still valid during grace period) |
| `apikeys.revoked` | Email + InApp | Info | `ApiKeyRevokedEto` (key revoked, no longer accepted) |

Recipients are resolved through `INotificationPublisher.PublishToSubscribersAsync` —
admins opt in via the notifications admin UI rather than being hardcoded into options.

## Secret-redaction guarantee

API key material is **never** propagated through this bridge. Specifically:

- The raw key value (the secret returned to the caller at creation time) is **never**
  carried by any integration event in `Granit.Authentication.ApiKeys` — only its
  SHA-256 hash is persisted, and only by the core module.
- The SHA-256 hash itself is present on `ApiKeyRevokedEto`, `ApiKeyRotatedEto`, and
  `ApiKeyScopesUpdatedEto` for cache-eviction purposes — this bridge **does not**
  forward that hash to notification payloads, recipient-bound rendering, or templates.
- Notification data records expose only public-safe metadata: stable identifiers,
  the display name, and the key type. No `Hash`, `HashedKey`, `Secret`, `Value`, or
  `RawKey` field is ever read by a handler in this package.
- Templates likewise reference only `{{ model.key_id }}` / `{{ model.key_name }}` /
  `{{ model.key_type }}` / `{{ model.old_key_id }}` / `{{ model.new_key_id }}`. Tests
  in `Granit.Authentication.ApiKeys.Notifications.Tests` assert that no field
  matching `Hash` / `Secret` / `Value` ever reaches the published payload.

This is defence in depth — the originating events are designed not to carry the raw
key, and the bridge is designed never to forward the hash that they do carry.

## Documentation

See the [full documentation](https://granit-fx.dev).
