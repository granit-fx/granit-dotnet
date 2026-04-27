# Granit.CustomerBalance.Notifications

Notification bridge for `Granit.CustomerBalance`. Routes credit-lifecycle integration
events to users (and tenant administrators when the event lacks recipient info), so
nobody learns that a credit expired by reading their next invoice. Ships embedded HTML
templates in **English and French** for every notification type (overridable at runtime
through the `Granit.Templating` admin API).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.CustomerBalance.Notifications
```

## Dependencies

- `Granit.CustomerBalance`
- `Granit.Notifications`
- `Granit.Templating`

## Notification types

| Notification name | Trigger | Recipient | Default channels | Severity |
| ----------------- | ------- | --------- | ---------------- | -------- |
| `customer-balance.credit_granted` | `BalanceCreditedEto` | Owning party (end user) | Email, InApp | Info |
| `customer-balance.credit_expired` | `CreditExpiredEto` | Tenant administrators (subscribers) | Email, InApp | Warning |

`credit_granted` is addressed directly to the `PartyId` carried by
`BalanceCreditedEto`. `credit_expired` falls back to
`PublishToSubscribersAsync` because `CreditExpiredEto` does not carry an owning party
identifier — admins can then follow up with the affected user or adjust their
expiration policy.

## Documentation

See the [full documentation](https://granit-fx.dev).
