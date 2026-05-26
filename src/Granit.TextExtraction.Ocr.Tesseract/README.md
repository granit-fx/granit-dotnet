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

# The Charlesw `Tesseract` NuGet probes for "libleptonica-1.82.0" and
# "libtesseract50" by name AND it appends a platform-name subdirectory
# ("x64" on amd64) to the search root. Stage the canonical-name symlinks
# under a <root>/x64/ subdirectory — mirrors the layout the NuGet uses
# natively for its Windows DLLs. The runtime apt packages only ship the
# fully-versioned `.so.MAJOR.MINOR.PATCH` files; we discover them via glob.
SRC=/usr/lib/x86_64-linux-gnu
DEST=/opt/granit-ocr-libs/x64
mkdir -p "$DEST"
ln -sf "$(ls $SRC/liblept.so.5* | head -n 1)" "$DEST/libleptonica-1.82.0.so"
ln -sf "$(ls $SRC/libtesseract.so.5* | head -n 1)" "$DEST/libtesseract50.so"
```

The traineddata path is then `/usr/share/tesseract-ocr/5/tessdata/`. Each language
adds ~10–30 MB.

Wire the search root via options:

```csharp
services.AddTesseractOcrExtractor(o =>
{
    o.DataPath = "/usr/share/tesseract-ocr/5/tessdata";
    o.LibrarySearchPath = "/opt/granit-ocr-libs";  // NOT /opt/granit-ocr-libs/x64
    o.Language = "eng+fra";
});
```

## Security

- Body-size cap inherited from `GranitTextExtractionOptions.MaxBodySizeBytes`.
- Pixel-bomb defence: image header is read with `Image.Identify` (no decode) and
  `width × height` is checked against `TesseractOcrOptions.MaxImagePixels` (default
  100 MP — covers A1 @ 600 DPI). Oversized images soft-skip.
- Malformed / unknown formats soft-skip.
- Engine failures soft-skip (returns `IsTruncated=true`, empty content) — the
  pipeline never throws on OCR failure so a broken image doesn't tank the rest of
  the document.

## Native-library search path

The Charlesw `Tesseract` NuGet uses its own `InteropDotNet.LibraryLoader` on
Linux which does **not** honour `LD_LIBRARY_PATH` or the standard `dlopen`
search paths — it only probes the app's `bin/` directory and a
`TesseractEnviornment.CustomSearchPath` (typo in the upstream API), **and it
appends a platform-name subdirectory (`x64` on amd64) to the root before
opening files**. So `LibrarySearchPath = "/opt/X"` causes the loader to open
`/opt/X/x64/libleptonica-1.82.0.so` — the `x64/` segment is mandatory.

`DefaultTesseractRecognizer` forwards `TesseractOcrOptions.LibrarySearchPath`
to the wrapper's API. The Deployment section above shows the staging
recipe; if you point the option at a path that doesn't follow the
`<root>/x64/` convention, `DllNotFoundException` fires on the first OCR call.

```csharp
services.AddTesseractOcrExtractor(o =>
{
    o.DataPath = "/opt/myapp/tessdata";
    o.LibrarySearchPath = "/opt/myapp/native";  // libs live at /opt/myapp/native/x64/
});
```

Leave `null` (the default) when the host ships the Windows DLLs in the
app's `bin/x64` directory — the wrapper's built-in fallbacks find them
there without needing `CustomSearchPath`.

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
# Stage the canonical-name symlinks under <root>/x64/ (see Deployment above)
sudo mkdir -p /opt/granit-ocr-libs/x64
SRC=/usr/lib/x86_64-linux-gnu
sudo ln -sf "$(ls $SRC/liblept.so.5* | head -n 1)" \
    /opt/granit-ocr-libs/x64/libleptonica-1.82.0.so
sudo ln -sf "$(ls $SRC/libtesseract.so.5* | head -n 1)" \
    /opt/granit-ocr-libs/x64/libtesseract50.so
export TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata
export GRANIT_TESSERACT_LIB_DIR=/opt/granit-ocr-libs
dotnet test tests/Granit.TextExtraction.Ocr.Tesseract.Tests.Integration
```

CI installs the deps on the `integration` shard automatically; see
`.github/workflows/ci.yml`.
