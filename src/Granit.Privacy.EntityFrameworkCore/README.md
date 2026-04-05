# Granit.Privacy.EntityFrameworkCore

EF Core persistence for Granit.Privacy. Provides `PrivacyDbContext` with `LegalDocument`
entity management, `EfLegalDocumentStore` (reader/writer), and
`LegalDocumentPublicationService` for auto-archive on publish with
`LegalAgreementObsoleteEto` dispatch.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.EntityFrameworkCore
```

## Dependencies

- `Granit.Privacy`
- `Granit.Persistence.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
