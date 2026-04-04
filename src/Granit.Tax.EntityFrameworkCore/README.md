# Granit.Tax.EntityFrameworkCore

EF Core persistence for Granit.Tax. Stores cached VIES validation results
and admin-managed tax rate overrides.

## Usage

```csharp
builder.AddGranitTaxEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```
