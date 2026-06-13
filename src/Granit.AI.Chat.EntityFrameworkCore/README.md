# Granit.AI.Chat.EntityFrameworkCore

EF Core persistence for [Granit.AI.Chat](../Granit.AI.Chat): an isolated, tenant-aware DbContext
for the `Conversation` / `Message` aggregates and the owner-scoped `IConversationStore`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat.EntityFrameworkCore
```

## Usage

```csharp
services.AddGranitAIChatEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```

The host owns migrations; apply the entity configurations to a shared context via
`modelBuilder.ConfigureAIChatModule()` if not using the isolated one.

## Dependencies

- `Granit.AI.Chat`
- `Granit.Persistence.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
