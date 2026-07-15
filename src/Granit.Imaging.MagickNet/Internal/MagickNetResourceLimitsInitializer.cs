using Granit.Imaging.MagickNet.Options;
using ImageMagick;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Imaging.MagickNet.Internal;

/// <summary>
/// Applies the configured ImageMagick resource limits when the host starts.
/// </summary>
/// <remarks>
/// <see cref="ResourceLimits"/> is process-global native state: the last writer wins when
/// several hosts share a process, and the limits are NOT applied outside a host lifetime
/// (e.g. a bare service provider in tests). The per-request guards
/// (<see cref="ImagingMagickNetOptions.MaxInputBytes"/>, the magic-byte allowlist, the
/// header-only <c>Identify</c>) do not depend on these limits.
/// </remarks>
internal sealed partial class MagickNetResourceLimitsInitializer(
    IOptions<ImagingMagickNetOptions> options,
    ILogger<MagickNetResourceLimitsInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ImagingMagickNetOptions opts = options.Value;

        if (opts.MaxMemoryBytes > 0)
        {
            ResourceLimits.Memory = (ulong)opts.MaxMemoryBytes;
        }

        if (opts.MaxWidthPixels > 0)
        {
            ResourceLimits.Width = (ulong)opts.MaxWidthPixels;
        }

        if (opts.MaxHeightPixels > 0)
        {
            ResourceLimits.Height = (ulong)opts.MaxHeightPixels;
        }

        if (opts.MaxListLength > 0)
        {
            ResourceLimits.ListLength = (ulong)opts.MaxListLength;
        }

        LogLimitsApplied(opts.MaxMemoryBytes, opts.MaxWidthPixels, opts.MaxHeightPixels, opts.MaxListLength);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ImageMagick resource limits applied (memory={MaxMemoryBytes} bytes, width={MaxWidthPixels}px, height={MaxHeightPixels}px, listLength={MaxListLength}; 0 = ImageMagick default)")]
    private partial void LogLimitsApplied(long maxMemoryBytes, int maxWidthPixels, int maxHeightPixels, int maxListLength);
}
