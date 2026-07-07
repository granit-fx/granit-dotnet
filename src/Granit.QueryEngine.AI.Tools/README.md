# Granit.QueryEngine.AI.Tools

Exposes opted-in `QueryDefinition`s as ACL-bound `query_data` agent tools. Each tool
publishes the definition's filterable structure as a JSON schema and executes validated
`QueryRequest`s against the caller-scoped `IQueryableSource<TEntity>`, so results are
bounded by the same tenant and ACL filters the user is subject to everywhere else.

> **Privacy note** — tool results return the matching rows to the LLM (bounded to what
> the caller may already read). If only query *metadata* may ever reach the model, use
> the NLQ translator in `Granit.QueryEngine.AI` and do not reference this package.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.QueryEngine.AI.Tools
```

## Usage

```csharp
services.AddGranitAITools(tools => tools.AddQueryData(q =>
{
    q.Add<Order>("orders", "Customer orders with status and totals");
    q.Add<Product>("products");
}));
```

## Dependencies

- `Granit.AI.Tools`
- `Granit.QueryEngine.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
