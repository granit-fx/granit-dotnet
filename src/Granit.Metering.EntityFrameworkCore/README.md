# Granit.Metering.EntityFrameworkCore

EF Core persistence for Granit.Metering.

## Usage

```csharp
builder.AddGranitMeteringEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```
