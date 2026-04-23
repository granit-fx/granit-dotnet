# Granit.DataLookup

Unified data-lookup runtime for Granit. Registers a scoped `ILookupRegistry` that
aggregates every `ILookupSource` declared in DI and exposes them by name to the
lookup endpoints package.

**Built-in adapter:**

- `EnumLookupSource<TEnum>` — exposes a CLR enum as a lookup with localized labels
  (`Enum:{TypeName}.{Value}` keys). Zero database roundtrip.

**External adapters:**

- `Granit.DataLookup.EntityFrameworkCore` — `QueryDefinitionLookupSource<T>` wraps a
  `QueryDefinition<T>` and projects it to the canonical `LookupItem` shape.
- `Granit.ReferenceData` — wraps each reference-data entity with label localization
  from `Label{Culture}` columns.

Depend on this package from application hosts that execute lookups. For module-level
declarations (descriptors), depend on `Granit.DataLookup.Abstractions` instead.

See [ADR-023](../../docs-site/src/content/docs/dotnet/architecture/adr/023-queryengine-reference-data-lookup.md)
for the architectural rationale.
