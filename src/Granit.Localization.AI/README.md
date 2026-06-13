# Granit.Localization.AI

AI-powered translation suggestions for Granit.Localization. Generates translations for all 17 supported cultures from a source text using LLM, with context-aware quality (UI labels, error messages, notifications).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Localization.AI
```

## `translate` chat tool

Exposes the translation capability as a gated agentic-chat tool (ADR-067):

```csharp
services.AddGranitAITools(tools => tools.AddTranslateTool());
```

The tool is gated by the `AI.ChatTools.Translate` permission — it is only offered to users who
have been granted it, so admins enable it per user/role. This is the reference pattern for
wrapping any `*.AI` capability as a permission-gated chat tool.

## Dependencies

- `Granit.AI`
- `Granit.AI.Tools`
- `Granit.Localization`

## Documentation

See the [full documentation](https://granit-fx.dev).
