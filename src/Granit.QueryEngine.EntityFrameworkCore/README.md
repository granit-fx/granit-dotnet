# Granit.QueryEngine.EntityFrameworkCore

EF Core query engine for Granit.QueryEngine. Provides `IQueryEngine<T>` with expression
tree-based filtering, multi-column sorting, offset/keyset pagination, and GroupBy with
aggregates — including group-by on a nested EF Core complex-type member.

Part of the [granit](https://granit-fx.dev) framework.

## Nested complex-type group-by

`AllowGroupBy` accepts a member of an EF Core complex type, e.g.
`builder.AllowGroupBy(a => a.Value.Country)` where `Value` is a complex property. The field
is stored as the dotted path `"Value.Country"` and surfaces on the wire as
`?groupBy=Value.Country` — the engine translates it to a plain SQL `GROUP BY` on the mapped
column. Drilling into a `SingleValueObject`'s `.Value` is still rejected (issue #2767); mark
a value-object column `[QueryableValueObject]` to group by its underlying scalar instead.

## Installation

```bash
dotnet add package Granit.QueryEngine.EntityFrameworkCore
```

## Dependencies

- `Granit.Persistence.EntityFrameworkCore`
- `Granit.QueryEngine`

## Documentation

See the [full documentation](https://granit-fx.dev).
