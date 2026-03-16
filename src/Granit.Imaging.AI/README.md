# Granit.Imaging.AI

AI-powered image analysis for Granit applications. Provides `IAIImageAnalyzer` for
classification, OCR, alt text generation via multimodal LLM (GPT-4o, Claude with vision).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Imaging.AI
```

## Dependencies

- `Granit.AI` — AI workspace and chat client factory
- `Granit.Imaging` — Image processing abstractions

## Configuration

```json
{
  "AI": {
    "Imaging": {
      "WorkspaceName": "vision",
      "TimeoutSeconds": 15
    }
  }
}
```

The `WorkspaceName` must reference an AI workspace configured with a multimodal model.
When omitted, the default workspace is used.

## Documentation

See the [full documentation](https://granit-fx.dev).
