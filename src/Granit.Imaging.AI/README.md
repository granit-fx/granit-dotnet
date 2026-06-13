# Granit.Imaging.AI

AI-powered image analysis for Granit applications. Provides `IAIImageAnalyzer` for
classification, OCR, alt text generation via multimodal LLM (GPT-4o, Claude with vision).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Imaging.AI
```

## `extract_text_from_image` chat tool

Exposes vision text extraction as an opt-in, default-off agentic-chat tool (ADR-067). It routes
to a Vision-capable workspace (decoupled from the chat workspace), so a text-only chat model can
still read images, and stamps its own usage record:

```csharp
services.AddGranitAITools(tools => tools.AddImageTextExtractionTool());

// Provide image bytes for a model-supplied reference (attachment id, blob key, …):
services.AddScoped<IAIImageSource, MyBlobImageSource>();
```

If no Vision-capable workspace is configured (or no `IAIImageSource` is registered) the tool
degrades gracefully — the agent is told it cannot read the image rather than failing the run.

## Dependencies

- `Granit.AI` — AI workspace and chat client factory
- `Granit.AI.Tools` — agentic tool seam
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
