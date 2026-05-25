# Granit.TextExtraction.Ocr.Tesseract

Opt-in Tesseract OCR extractor for `Granit.TextExtraction`. Runs the native
`libtesseract` engine **locally** — no third-party calls, no document bytes leaving
the host. Pairs with `Granit.TextExtraction.Ocr.AI` when a higher-quality cloud VLM
is also wired up.

## Opt-in

```csharp
services.AddTesseractOcrExtractor(o =>
{
    o.DataPath = "/usr/share/tesseract-ocr/5/tessdata";
    o.Language = "eng+fra";
});
```

No safe default — the module does not auto-register because deployment requires both
the native library and the traineddata files.

## Deployment

Linux (Debian / Ubuntu):

```bash
apt-get install -y libtesseract5 tesseract-ocr-eng tesseract-ocr-fra
```

The traineddata path is then `/usr/share/tesseract-ocr/5/tessdata/`. Each language
adds ~10–30 MB.

## Security

- Body-size cap inherited from `GranitTextExtractionOptions.MaxBodySizeBytes`.
- Pixel-bomb defence: image header is read with `Image.Identify` (no decode) and
  `width × height` is checked against `TesseractOcrOptions.MaxImagePixels` (default
  100 MP — covers A1 @ 600 DPI). Oversized images soft-skip.
- Malformed / unknown formats soft-skip.
- Engine failures soft-skip (returns `IsTruncated=true`, empty content) — the
  pipeline never throws on OCR failure so a broken image doesn't tank the rest of
  the document.

## Threading

`DefaultTesseractRecognizer` serialises all calls through one locked
`TesseractEngine` (Tesseract is not thread-safe). For high-throughput workloads,
register a custom `ITesseractRecognizer` that pools multiple engines before
calling `AddTesseractOcrExtractor`.
