# Granit.DataExchange.Definitions

Pre-built `ExportDefinition<T>` implementations for 39 Granit framework entities.
Each definition uses a security-by-design whitelist: only explicitly declared
fields are exported, with all `[SensitiveData]` fields excluded (GDPR Art. 25).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.Definitions
```

## Usage

```csharp
[DependsOn(typeof(GranitDataExchangeDefinitionsModule))]
public class AppModule : GranitModule { }
```

This registers export definitions for `Tenant`, `Invoice`, `PaymentTransaction`,
`AuditEntry`, `BlobDescriptor`, and 34 other entities. Applications can override
any definition by registering their own `ExportDefinition<T>` for the same entity.

## License

[Apache-2.0](../../LICENSE)
