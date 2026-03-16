# Granit.Authorization.AI

AI-powered access anomaly detection for the Granit framework.
Provides `IAIAccessAnomalyDetector` for evaluating access patterns and detecting
suspicious behavior via LLM analysis. Fail-open design ensures authorization
never blocks requests when the AI service is unavailable.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authorization.AI
```

## Dependencies

- `Granit.AI`
- `Granit.Authorization`

## Documentation

See the [full documentation](https://granit-fx.dev/-/tree/develop/docs/framework/authorization).
