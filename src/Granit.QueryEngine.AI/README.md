# Granit.QueryEngine.AI

Natural Language Query (NLQ) for Granit.QueryEngine. Translates natural language into structured QueryRequest objects using LLM, and exposes opted-in query definitions as ACL-bound `query_data` tools for the agentic chat (ADR-067).

Part of the [granit](https://granit-fx.dev) framework.

## `query_data` tools

Opt definitions in to expose them to the chat agent. Each becomes a `query_<name>` tool whose
schema is projected from the definition's metadata; it executes a structured query over the
caller-scoped `IQueryableSource<TEntity>`, so results stay bounded by the user's tenant and ACLs.

```csharp
services.AddGranitAITools(tools => tools.AddQueryData(q =>
{
    q.Add<Order>("orders", "Customer orders with status and totals");
    q.Add<Product>("products");
}));
```

## Installation

```bash
dotnet add package Granit.QueryEngine.AI
```

## Dependencies

- `Granit.AI`
- `Granit.QueryEngine`

## Documentation

See the [full documentation](https://granit-fx.dev).
