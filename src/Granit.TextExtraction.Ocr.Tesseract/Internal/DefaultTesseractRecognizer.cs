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

            _engine ??= new TesseractEngine(
                _options.DataPath ?? throw new InvalidOperationException(
                    "TesseractOcrOptions.DataPath is required — set it to the tessdata directory."),
                _options.Language,
                EngineMode.Default);

            using var pix = Pix.LoadFromMemory(imageBytes);
            using Page page = _engine.Process(pix);
            return Task.FromResult(page.GetText() ?? string.Empty);
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
