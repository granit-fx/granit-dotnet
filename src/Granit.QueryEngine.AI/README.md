# Granit.QueryEngine.AI

Natural Language Query (NLQ) for Granit.QueryEngine. Translates natural language into
structured `QueryRequest` objects using an LLM. The translator sends only query metadata
(column names, filter types, operator codes) plus the user's own phrase to the model —
never stored business data. Model output is whitelisted against the definition's
metadata (shared `QueryRequestSanitizer`) before it becomes a `QueryRequest`.

> Looking for the agent `query_data` tools (ADR-067)? They live in the separate
> `Granit.QueryEngine.AI.Tools` package — tool results return ACL-bounded rows to
> the model, a different privacy posture than this translator.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.QueryEngine.AI
```

## Dependencies

- `Granit.AI`
- `Granit.QueryEngine.Abstractions`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
