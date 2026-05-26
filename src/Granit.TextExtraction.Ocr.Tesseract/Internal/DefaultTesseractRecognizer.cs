using System.Runtime.InteropServices;
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
        // (the wrapper's typo, not ours). Hosts that install libtesseract system-wide
        // via apt (the common Linux production shape) would hit DllNotFoundException
        // on the first OCR call. Route the option to the wrapper's API; auto-detect
        // the Debian/Ubuntu canonical path when no explicit value is set.
        string? searchPath = _options.LibrarySearchPath;
        if (searchPath is null && OperatingSystem.IsLinux())
        {
            searchPath = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "/usr/lib/x86_64-linux-gnu",
                Architecture.Arm64 => "/usr/lib/aarch64-linux-gnu",
                _ => null,
            };
        }

        // Empty string is the opt-out sentinel — leave the existing wrapper default
        // alone (the wrapper falls back to its own auto-detection logic, useful for
        // Windows hosts shipping the bundled DLLs).
        if (!string.IsNullOrEmpty(searchPath))
        {
            TesseractEnviornment.CustomSearchPath = searchPath;
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
