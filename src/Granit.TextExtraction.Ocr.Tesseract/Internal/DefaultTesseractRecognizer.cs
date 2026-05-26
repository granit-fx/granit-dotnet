using Granit.TextExtraction.Ocr.Tesseract.Options;
using Microsoft.Extensions.Options;
using Tesseract;

namespace Granit.TextExtraction.Ocr.Tesseract.Internal;

/// <summary>
/// Default <see cref="ITesseractRecognizer"/> backed by a singleton <see cref="TesseractEngine"/>.
/// The engine is NOT thread-safe — all calls are serialised by a lock. For high-throughput
/// hosts, register a custom <see cref="ITesseractRecognizer"/> that pools multiple engines.
/// </summary>
internal sealed class DefaultTesseractRecognizer : ITesseractRecognizer, IDisposable
{
    private readonly TesseractOcrOptions _options;
    private readonly Lock _gate = new();
    private TesseractEngine? _engine;
    private bool _disposed;

    public DefaultTesseractRecognizer(IOptions<TesseractOcrOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        cancellationToken.ThrowIfCancellationRequested();

        // Tesseract is synchronous + holds native state; do the work on the calling thread
        // under the lock. The cancellation token can't interrupt libtesseract mid-process,
        // but we honour it on entry to avoid queuing more work behind a cancelled request.
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_engine is null)
            {
                ApplyLibrarySearchPath();
                _engine = new TesseractEngine(
                    _options.DataPath ?? throw new InvalidOperationException(
                        "TesseractOcrOptions.DataPath is required — set it to the tessdata directory."),
                    _options.Language,
                    EngineMode.Default);
            }

            using var pix = Pix.LoadFromMemory(imageBytes);
            using Page page = _engine.Process(pix);
            return Task.FromResult(page.GetText() ?? string.Empty);
        }
    }

    private void ApplyLibrarySearchPath()
    {
        // The Charlesw `Tesseract` NuGet's InteropDotNet.LibraryLoader does NOT honour
        // LD_LIBRARY_PATH or the standard dlopen system paths on Linux — it only
        // probes the app's bin/ directory and `TesseractEnviornment.CustomSearchPath`
        // (the wrapper's typo, not ours), AND it appends a platform-name subdirectory
        // ("x64" on amd64) to the search root. So when the host sets
        // LibrarySearchPath = "/opt/X" the loader actually opens
        // "/opt/X/x64/libleptonica-1.82.0.so". Hosts installing libtesseract via apt
        // must therefore stage the .so files under a `<root>/x64/` subdir manually —
        // matching the layout the NuGet itself uses for its Windows DLLs. README
        // documents the recipe; the recognizer just forwards the host's path.
        if (!string.IsNullOrEmpty(_options.LibrarySearchPath))
        {
            TesseractEnviornment.CustomSearchPath = _options.LibrarySearchPath;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _engine?.Dispose();
            _engine = null;
            _disposed = true;
        }
    }
}
