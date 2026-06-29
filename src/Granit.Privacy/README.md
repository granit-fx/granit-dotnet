# Granit.Privacy

Reusable PIMS abstractions for GDPR/ISO 27001 user rights management. Provides Data Subject Export
via scatter-gather saga, Data Subject Deletion via domain events with opt-in cooling-off period,
Legal Agreements versioning, and `IDataProviderRegistry`. GDPR/ISO 27001 compliant.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy
```

## Dependencies

- `Granit`

## Usage

Wire the module through the `AddGranitPrivacy(builder => ...)` entry point in a
module's `ConfigureServices`. Three registrations are mandatory:

```csharp
services.AddGranitPrivacy(privacy =>
{
    // 1. Tracker persistence — the built-in PrivacyDbContext, or your own context:
    //    (UseEntityFrameworkCoreTrackers lives in Granit.Privacy.EntityFrameworkCore)
    privacy.UseEntityFrameworkCoreTrackers();

    // 2. Legal agreement store:
    privacy.UseLegalAgreementStore<MyLegalAgreementStore>();

    // 3. At least one data provider that contributes to export/erasure:
    privacy.AddDataProvider<MyDataProvider>();
    // Built-in providers ship their own Add*PrivacyProvider() extensions.
});
```

## Documentation

See the [full documentation](https://granit-fx.dev).
