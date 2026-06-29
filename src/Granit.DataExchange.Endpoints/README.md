# Granit.DataExchange.Endpoints

Minimal API endpoints for `Granit.DataExchange`.

**Import** (`DataExchange.Import` permission): upload, preview, mapping confirmation,
async execution, dry-run, status, report, correction file download.

**Export** (`DataExchange.Export` permission): list definitions, list fields,
create export job, check status, download file, preset CRUD.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Authorization`
- `Granit.DataExchange`
- `Granit.Guids`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
