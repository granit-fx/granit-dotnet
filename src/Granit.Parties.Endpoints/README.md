# Granit.Parties.Endpoints

Minimal API REST endpoints for [Granit.Parties](../Granit.Parties/README.md).
Exposes the full CRUD + lifecycle surface of the `Party` aggregate (create,
update, archive, suspend, activate), multi-collection mutation routes (addresses,
emails, phones, external mappings), parent/user linkage routes, and the avatar
soft-reference. Ships paginated listing through `Granit.QueryEngine` and CSV/XLSX
export through `Granit.DataExchange`, both backed by the module's
`PartyQueryDefinition` / `PartyExportDefinition`.

All endpoints are gated by the `Parties.*` permission group (`Read`, `Manage`)
and emit OpenAPI metadata for Scalar.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Parties`
- `Granit.DataExchange.Abstractions`
- `Granit.Guids`
- `Granit.Http.Abstractions`
- `Granit.Http.ApiDocumentation`
- `Granit.QueryEngine.Abstractions`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
