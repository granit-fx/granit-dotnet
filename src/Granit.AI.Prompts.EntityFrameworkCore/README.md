# Granit.AI.Prompts.EntityFrameworkCore

EF Core persistence for [`Granit.AI.Prompts`](../Granit.AI.Prompts) (ADR-067): an isolated,
tenant-aware `AIPromptsDbContext` and the `IPromptTemplateStore` for the prompt catalogue.

The catalogue a user sees is the framework-seeded system prompts plus their own private prompts;
reads are scoped accordingly. The host owns migrations.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Prompts.EntityFrameworkCore
```

## Usage

```csharp
services.AddGranitAIPromptsEntityFrameworkCore(options => options.UseNpgsql(connectionString));
```

## Dependencies

- `Granit.AI.Prompts`
- `Granit.Persistence.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
