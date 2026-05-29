# Granit.Auditing.Abstractions

Inter-module contracts for the Granit audit trail (ISO 27001 / GDPR).

Pure abstractions, no runtime — pull this from any base module that needs to
**emit** or **read** audit entries without taking a dependency on the
`Granit.Auditing` runtime (persistence pipeline, background workers, query /
export definitions, DI wiring).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Auditing.Abstractions
```

## What's inside

- **Domain model** — `AuditEntry`, `AuditEntityChange`, `AuditPropertyChange`,
  `AuditCategory`, `AuditChangeType`, `AuditPersistenceMode`.
- **Contracts** — `IAuditingReader`, `IAuditingWriter`, `IAuditingCleaner`,
  `IAuditEntryPublisher`, `IAuditBatchPersister`.
- **Timeline aggregation** — `IAuditChildResolver`,
  `IAuditEntityTypeAliasProvider`, `AuditEntityRef`, `AuditChildScope`,
  `StaticAuditEntityTypeAliasProvider`, and the resolver/alias extension helpers.
- **Bus messages** — `AuditingBatch`, `AuditEntityChangeSnapshot`,
  `AuditPropertyChangeSnapshot`.
- **Integration event** — `AuditEntryPersistedEto` (`IIntegrationEvent`), for
  SIEM / real-time security alerting.
- **`AuthenticationAuditEntry`** convenience shape and the `[AuditIgnore]`
  property attribute.

## Dependencies

- `Granit`
- `Granit.QueryEngine.Abstractions` (paging contracts on `IAuditingReader`)

## Related packages

- `Granit.Auditing` — the runtime (persistence, cleanup, diagnostics, query /
  export definitions).
- `Granit.Auditing.EntityFrameworkCore` — the EF Core reader / writer / persister
  implementations.
- `Granit.Auditing.Endpoints` — read-only Minimal API endpoints + channel health check.
