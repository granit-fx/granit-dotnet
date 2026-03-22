# Granit.Validation.AI

AI-powered content moderation for the Granit framework.
Provides `IAIContentModerator` for detecting toxic content, prompt injection
attempts, and spam/gibberish via LLM analysis. Fail-open design ensures
validation never blocks requests when the AI service is unavailable.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Validation.AI
```

## Dependencies

- `Granit.AI`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
