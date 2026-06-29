# Granit.Privacy.Regulations

Privacy regulation registry and policy engine. Provides immutable `PrivacyRegulationProfile`
definitions for 14+ jurisdictions (GDPR, LGPD, CCPA, PIPL, DPDPA, etc.) with per-tenant
resolution via `IPrivacyRegulationResolver`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.Regulations
```

## Dependencies

- `Granit`

## Usage

Register the regulation registry in a module's `ConfigureServices`:

```csharp
services.AddGranitPrivacyRegulations(configuration);
```

Configure the resolved regulation in `appsettings.json`:

```json
{
  "Privacy": {
    "Regulations": {
      "DefaultRegulation": "EU_GDPR"
    }
  }
}
```

A regulation must be resolvable for every request — set `DefaultRegulation`
(or per-tenant `TenantRegulations`), otherwise `IPrivacyRegulationResolver`
throws at runtime when none can be resolved.

## Documentation

See the [full documentation](https://granit-fx.dev).
