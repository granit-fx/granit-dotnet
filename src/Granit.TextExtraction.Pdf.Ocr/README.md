# Granit.TextExtraction.Pdf.Ocr

Opt-in OCR fallback for scanned PDFs. The base `Granit.TextExtraction.Pdf`
package extracts the embedded text layer via PdfPig; pages whose text layer is
empty or near-empty (scans, photographs, image-only exports) silently return
nothing. This package wires those pages through whatever `image/png` OCR
extractor the host already registered — `Granit.TextExtraction.Ocr.Tesseract`,
`Granit.TextExtraction.Ocr.AI`, or any custom one.

## Opt-in

Register the base PDF + an OCR extractor + this package, in any order between
the last two:

```csharp
services.AddGranitTextExtractionPdf();
services.AddTesseractOcrExtractor(o =>
{
    o.DataPath = "/usr/share/tesseract-ocr/5/tessdata";
});
services.AddGranitTextExtractionPdfOcr();
```

`AddGranitTextExtractionPdfOcr()` REPLACES the base `PdfTextExtractor` with the
OCR-aware one for `application/pdf`. Text-only PDFs still bypass OCR entirely —
the rasteriser only runs for pages whose embedded text is shorter than
`MinNativeCharsPerPage` (default 32).

## How it works

For each page in the document:

1. PdfPig runs the content-order extractor.
2. If the text layer ≥ `MinNativeCharsPerPage` → emit it as-is (no OCR).
3. Otherwise → rasterise the page (one bitmap at a time, no whole-doc buffer)
   and route the PNG through the host's `image/png` `ITextExtractor`.
4. Pages are joined in document order — mixed PDFs (text + scan) preserve
   reading order.

`PdfOcrTextExtractor.Confidence` drops to `Heuristic` as soon as any page used
OCR — consumers can flag the result as partial.

## Security

- Body-size cap inherited from `GranitTextExtractionOptions.MaxBodySizeBytes`.
- Pixel-bomb defence: pages whose computed bitmap surface
  (`width × height × dpi/72`) exceeds `MaxPagePixels` (default 100 MP) are
  rejected BEFORE rasterisation — no allocation, no native call.
- Page-count cap: `MaxPagesToRasterise` (default 200) protects against
  thousand-page scanned reports burning a worker.
- Encrypted / malformed PDFs soft-skip — they never throw out of the extractor.
- Per-page failures (rasterisation crash, OCR failure) soft-skip the *page*,
  not the document.

## Deployment

The default `IPdfRasterizer` uses [PDFtoImage](https://www.nuget.org/packages/PDFtoImage)
(PDFium + SkiaSharp). Three operational things to know:

### Glibc, not musl

PDFium and SkiaSharp ship pre-compiled native binaries built against `glibc`.
Alpine images use `musl`, so `DllNotFoundException` fires at the first OCR
page. Use `bookworm-slim` / `noble` (or any Debian-family image).

### libfontconfig1 must be installed

SkiaSharp's text rendering pipeline probes for system fonts via `fontconfig`.
Without it the rasteriser crashes on PDFs containing text fallbacks:

```Dockerfile
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*
```

### Scale-out, not scale-up

PDFium's native engine is not thread-safe. The default rasteriser pins access
via a per-instance lock, so 50 concurrent `Task.WhenAll` rasterisations on the
same pod serialise. For throughput at scale, add pods — adding threads inside
one pod buys nothing.

If you have a custom thread-safe wrapper (pooled engines, GPU acceleration),
register it BEFORE `AddGranitTextExtractionPdfOcr`:

```csharp
services.AddSingleton<IPdfRasterizer, MyPooledPdfRasterizer>();
services.AddGranitTextExtractionPdfOcr();
```

## Options

| Setting | Default | Purpose |
| ------- | ------- | ------- |
| `MinNativeCharsPerPage` | 32 | Below this PdfPig output length the page is treated as scanned and OCR'd. |
| `RenderDpi` | 300 | Render resolution. 300 is the Tesseract sweet spot; raise for small glyphs, lower for throughput. |
| `MaxPagesToRasterise` | 200 | Per-document cap. Extra pages drop and the result is flagged truncated. |
| `MaxPagePixels` | 100 MP | Pixel-bomb cap — pages declaring extreme dimensions reject before render. |

Bind from `appsettings.json` under `TextExtraction:Pdf:Ocr`.
