# Granit.QueryEngine.Endpoints

Minimal API endpoints for Granit.QueryEngine. Provides the `MapGranitQuery<T>()`
fluent API (auto-resolving from `IQueryableSource<T>` or an explicit
`sourceProvider` delegate), `filter[field.op]=value` query string binding,
`GET /meta` metadata endpoint, and OpenAPI documentation conventions.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.QueryEngine.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Authorization`
- `Granit.Entities.Abstractions`
- `Granit.QueryEngine`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
