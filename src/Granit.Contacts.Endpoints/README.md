# Granit.Contacts.Endpoints

Minimal API REST endpoints for [Granit.Contacts](../Granit.Contacts/README.md).
Exposes the full CRUD + lifecycle surface of the `Contact` aggregate (create,
update, archive, suspend, activate), multi-collection mutation routes (addresses,
emails, phones, external mappings), parent/user linkage routes, and the avatar
soft-reference. Ships paginated listing through `Granit.QueryEngine` and CSV/XLSX
export through `Granit.DataExchange`, both backed by the module's
`ContactQueryDefinition` / `ContactExportDefinition`.

All endpoints are gated by the `Contacts.*` permission group (`Read`, `Manage`)
and emit OpenAPI metadata for Scalar.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Contacts.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Contacts`
- `Granit.DataExchange.Abstractions`
- `Granit.Guids`
- `Granit.Http.Abstractions`
- `Granit.Http.ApiDocumentation`
- `Granit.QueryEngine.Abstractions`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
