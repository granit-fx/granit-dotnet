# Granit.TextExtraction.Ocr.AI

Opt-in Vision Language Model (VLM) OCR extractor that uses Granit.AI's
multimodal `IChatClient` to extract verbatim text from raster images. Best
suited for handwriting, complex layouts, multi-column scans, and non-Latin
scripts where Tesseract struggles.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction.Ocr.AI
```

## Dependencies

- `Granit.TextExtraction`
- `Granit.AI` (workspace + `IChatClient` factory)

## GDPR Article 28 disclosure

**Enabling this extractor sends document bytes to a third-party LLM
provider** (Azure OpenAI, Anthropic, customer-hosted vLLM, Ollama, …).
The host is responsible for executing a Data Processing Agreement (DPA)
with that provider before processing personal data through this extractor.

For on-prem-only deployments, configure the Granit.AI workspace to point at
a local provider (Ollama, vLLM, LM Studio) and audit the workspace
configuration with an architecture test on your host application.

## Configuration

`appsettings.json`:

```json
{
  "TextExtraction": {
    "Ocr": {
      "AI": {
        "WorkspaceName": "vision-ocr",
        "AllowedContentTypes": ["image/png", "image/jpeg", "image/webp", "image/tiff"]
      }
    }
  }
}
```

The named workspace MUST exist in Granit.AI's workspace store and MUST point
at a model with vision/multimodal support (e.g. `gpt-4o`, `claude-3.5-sonnet`,
`llava` on Ollama).

## Custom prompts

Replace the default prompt by registering an `IVisionOcrPromptBuilder` before
calling `AddAIVisionOcrExtractor`:

```csharp
services.AddSingleton<IVisionOcrPromptBuilder, MyDomainPromptBuilder>();
services.AddAIVisionOcrExtractor();
```

The default prompt asks the model to "extract verbatim text, preserving table
structure as GitHub-Flavored Markdown, and leave the answer empty if the
image contains no readable text".

## Security posture

- **Cost ceiling** — inherited from `Granit.AI.Options.AIQuotaOptions`
  (`MaxRequestsPerTenantPerHour`). The IChatClient pipeline applies the quota
  middleware automatically; this package does NOT implement its own counter.
- **`LimitedStream`** wraps the input before sending to the model — caps the
  HTTP body at `GranitTextExtractionOptions.MaxBodySizeBytes` (default 100 MB).
  Effectively also bounds the pixel-bomb attack surface: a 100k × 100k image
  decoded to bytes would exceed 100 MB long before reaching the provider.
- **Response cap** — the extractor truncates the model's reply at
  `maxCharLength` on receive, so a hallucinating model can't produce
  unbounded text.

## Deferred to follow-ups

- **`AllowedProviderHosts` enforcement** — would need an introspection
  surface on `IChatClient` to report its endpoint URI. Microsoft.Extensions.AI
  doesn't expose that today. Tracked separately.
- **`RequireOnPremProvider` capability tag** — same blocker; needs a
  provenance-tag abstraction the upstream library doesn't have.
- **Scanned PDF rasterisation** — out of scope here; combine with
  `Granit.TextExtraction.Pdf` + an image pipeline downstream.

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
