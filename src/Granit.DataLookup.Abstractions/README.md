# Granit.DataLookup.Abstractions

Inter-module contracts for **Granit.DataLookup** — a unified primitive for declarative
typeahead pickers backed by QueryEngine definitions, ReferenceData sets, CLR enums, or
custom HTTP endpoints.

This package holds the pure contracts:

- `LookupDescriptor` / `LookupKind` — declarative pointer emitted alongside column or
  field metadata.
- `LookupItem` / `LookupResult` / `LookupQuery` — canonical request / response shape
  every source projects to.
- `ILookupSource` — the runtime contract every adapter implements.
- `ILookupRegistry` — central lookup dispatch resolved at startup.

Depend on this package from any module that **declares** a lookup (via a query
definition or a form field descriptor). Depend on `Granit.DataLookup` only from hosts
that need to **execute** lookups.

See ADR-023 for the architectural rationale and scope.
