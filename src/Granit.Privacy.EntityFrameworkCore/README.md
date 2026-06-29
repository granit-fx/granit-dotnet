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

## Usage

Point the privacy trackers at an EF Core store via the privacy builder. Two paths:

```csharp
// 1. Default — use the built-in PrivacyDbContext:
services.AddGranitPrivacy(privacy => privacy.UseEntityFrameworkCoreTrackers());

// 2. Fold the privacy model into your own DbContext:
//    a) in OnGranitModelCreating (or OnModelCreating):
modelBuilder.ConfigurePrivacyModule();
//    b) target your context in the privacy builder:
services.AddGranitPrivacy(privacy =>
    privacy.UseEntityFrameworkCoreTrackers<MyAppDbContext>());
```

## Documentation

See the [full documentation](https://granit-fx.dev).
