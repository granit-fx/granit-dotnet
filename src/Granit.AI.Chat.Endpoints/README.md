# Granit.AI.Chat.Endpoints

HTTP endpoints for [Granit.AI.Chat](../Granit.AI.Chat): owner-scoped conversation management
(list, get, create, rename, delete) with permission gating and validation.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat.Endpoints
```

## Usage

```csharp
app.MapGranitConversations();
```

Endpoints are gated by `AIChat.Conversations.{Read,Manage,Delete}` and scoped to the calling
user — a conversation owned by another user is reported as not found.

## Dependencies

- `Granit.AI.Chat`
- `Granit.Authorization`
- `Granit.Validation`
- `Granit.Http.ApiDocumentation`

## Documentation

See the [full documentation](https://granit-fx.dev).
