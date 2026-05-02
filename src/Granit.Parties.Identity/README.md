# Granit.Parties.Identity

Identity integration for [Granit.Parties](../Granit.Parties/README.md).
Subscribes to `Granit.Identity.Events.UserCreatedEto` and
`UserProfileChangedEto` from the canonical `User` aggregate (ADR-051) and
keeps a matching `Party` of kind `Individual` in sync — so admin grids,
BI exports, and the CRM/ERP surfaces of `Granit.Parties` always see a
`Party` for every authenticatable user, without manual intervention.
Idempotent: Wolverine's at-least-once delivery is safe.

Kept separate from the base `Granit.Parties` so apps that do not load
`Granit.Identity` do not inherit a dependency on it.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.Identity
```

## Dependencies

- `Granit.Parties`
- `Granit.Identity`

## Bridge contract

| Event | Handler | Action |
| ----- | ------- | ------ |
| `UserCreatedEto` | `EnsurePartyForUserHandler` | Materialise a `Party` of kind `Individual` linked to `User.Id` (idempotent — keys on `Party.UserId`). |
| `UserProfileChangedEto` | `SyncProfileToPartyHandler` | Propagate `DisplayName`, `PreferredLocale`, `Timezone` to the linked Party. No-op when the Party hasn't been created yet. |

Reverse sync (Party → User) is intentionally out of scope: the canonical
`User` aggregate is the source of truth for identity-side fields.

## Configuration

```json
{
  "Granit": {
    "Parties": {
      "Identity": {
        "DefaultCurrency": "EUR"
      }
    }
  }
}
```

`DefaultCurrency` (ISO 4217) — applied to `Party` rows materialised on
`UserCreatedEto`. Admin can override per-Party post-creation.

## Documentation

See the [full documentation](https://granit-fx.dev).
