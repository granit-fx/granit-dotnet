# Granit.AI.Chat

Conversational AI for the Granit framework (ADR-067): the `Conversation` / `Message` aggregates
(multi-tenant, **private to their owner**) and the `IConversationStore` abstraction. Persistence
lives in `Granit.AI.Chat.EntityFrameworkCore`; the HTTP surface in `Granit.AI.Chat.Endpoints`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
