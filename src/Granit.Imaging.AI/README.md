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

## Which image-to-text path to use

Granit deliberately ships THREE image-to-text implementations; they serve different layers
and their differences (workspace resolution, prompt, failure semantics) are intentional —
see ADR-067.

| Path | Package | Use when |
| --- | --- | --- |
| `TesseractOcrExtractor` (`ITextExtractor`) | `Granit.TextExtraction.Ocr.Tesseract` | Deterministic, on-prem OCR in the indexing pipeline; no data leaves the host |
| `AIVisionOcrExtractor` (`ITextExtractor`) | `Granit.TextExtraction.Ocr.AI` | LLM-vision OCR in the indexing pipeline; workspace named in options; soft-skips on failure |
| `extract_text_from_image` tool (`IImageTextExtractor`) | `Granit.Imaging.AI` | Vision-as-tool for agentic chat (opt-in, default-off); resolves a Vision workspace via the capability resolver; degrades to null |

All LLM paths share the `<granit-vlm-ocr>` envelope (`Granit.AI.Vision.VisionOcrEnvelope`)
and an OCR-only system message as prompt-injection defence (OWASP LLM01).

## Documentation

See the [full documentation](https://granit-fx.dev).
