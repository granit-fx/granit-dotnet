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
# The Charlesw Tesseract NuGet probes for "libleptonica-1.82.0" and
# "libtesseract50" by name. The runtime apt packages only ship the fully
# versioned `.so.MAJOR.MINOR.PATCH` files (`liblept.so.5.0.4`,
# `libtesseract.so.5.0.3` on Ubuntu 24.04) — no `.so.MAJOR` shortcut.
# Discover the file via glob and symlink the canonical names:
LIB_DIR=/usr/lib/x86_64-linux-gnu
ln -sf "$(ls $LIB_DIR/liblept.so.5* | head -n 1)" "$LIB_DIR/libleptonica-1.82.0.so"
ln -sf "$(ls $LIB_DIR/libtesseract.so.5* | head -n 1)" "$LIB_DIR/libtesseract50.so"
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

## Running the live OCR test suite locally

The unit suite (`tests/Granit.TextExtraction.Ocr.Tesseract.Tests`) mocks the
recognizer and runs everywhere. The live suite
(`tests/Granit.TextExtraction.Ocr.Tesseract.Tests.Integration`) drives the real
native engine and is opt-in via the `TESSDATA_PREFIX` env var — without it the
tests skip themselves so a missing system package doesn't block the unit run:

```bash
sudo apt-get install -y libtesseract5 tesseract-ocr-eng
export TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata
dotnet test tests/Granit.TextExtraction.Ocr.Tesseract.Tests.Integration
```

CI installs the deps on the `integration` shard automatically; see
`.github/workflows/ci.yml`.
