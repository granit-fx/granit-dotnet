# Granit.Privacy.Wolverine

Wolverine sagas and handlers for `Granit.Privacy`: the scatter-gather
personal-data export saga (GDPR Art. 15/20), the deferred deletion saga with
cooling-off period and provider acknowledgement fan-in (GDPR Art. 17), and the
legal-document cache invalidation handler. Hosting them here keeps the base
`Granit.Privacy` package free of any Wolverine handler or saga — the base only
retains the `[SagaIdentity]` annotations on its `*Eto` contracts.

All types are discovered by Wolverine's assembly scanning: loading
`GranitPrivacyWolverineModule` is enough, no explicit registration.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.Wolverine
```

Any host that consumes the privacy export or deletion workflows (publishes
`PersonalDataRequestedEto` / `DeletionDeferredEto`, or maps the privacy
endpoints) must reference this package — without it the sagas are not
discovered and the events go unhandled.

## Dependencies

- `Granit.Privacy`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
