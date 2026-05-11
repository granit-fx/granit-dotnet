using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Office.Options;
using Granit.Documents.Renditions.Providers;
using Granit.Guids;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Renditions.Office.Internal;

/// <summary>
/// Office (<c>docx</c> / <c>xlsx</c> / <c>pptx</c> + legacy + ODF + RTF) →
/// <c>application/pdf</c> rendition provider. Drives LibreOffice headless via
/// <c>soffice --headless --convert-to pdf</c>.
/// </summary>
/// <remarks>
/// <para>
/// LibreOffice headless is not thread-safe against the same user profile; invocations
/// are serialised through a <see cref="SemaphoreSlim"/> sized by
/// <see cref="OfficeRenditionOptions.MaxConcurrentConversions"/>. Each invocation gets
/// a fresh <c>-env:UserInstallation</c> directory so per-worker concurrency &gt; 1
/// requires no extra wiring.
/// </para>
/// <para>
/// The provider writes the source bytes to a per-invocation secure temp file, runs
/// <c>soffice</c> with <c>--outdir</c> pointed at the same directory, then reads the
/// resulting PDF back. The directory is removed after the conversion regardless of
/// outcome.
/// </para>
/// </remarks>
internal sealed partial class OfficeRenditionProvider : IRenditionProvider, IDisposable
{
    private readonly IOptions<OfficeRenditionOptions> _options;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<OfficeRenditionProvider> _logger;
    private readonly SemaphoreSlim _gate;

    public OfficeRenditionProvider(
        IOptions<OfficeRenditionOptions> options,
        IGuidGenerator guidGenerator,
        ILogger<OfficeRenditionProvider> logger)
    {
        _options = options;
        _guidGenerator = guidGenerator;
        _logger = logger;
        _gate = new SemaphoreSlim(options.Value.MaxConcurrentConversions);
    }

    /// <inheritdoc />
    public string Name => "office-libre";

    /// <inheritdoc />
    public string OutputContentType => "application/pdf";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType) =>
        !string.IsNullOrEmpty(sourceContentType) &&
        OfficeMimeTypes.ExtensionByMime.ContainsKey(sourceContentType);

    /// <inheritdoc />
    public async Task<RenditionResult> GenerateAsync(
        Stream source,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (!OfficeMimeTypes.ExtensionByMime.TryGetValue(sourceContentType, out string? sourceExt))
        {
            throw new NotSupportedException(
                $"Source content type '{sourceContentType}' is not in the Office MIME set.");
        }

        OfficeRenditionOptions cfg = _options.Value;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string workDir = Path.Combine(
                Path.GetTempPath(),
                $"granit-soffice-{_guidGenerator.Create():N}");
            Directory.CreateDirectory(workDir);
            try
            {
                string inputPath = Path.Combine(workDir, $"input.{sourceExt}");
                await using (FileStream input = File.Create(inputPath))
                {
                    await source.CopyToAsync(input, cancellationToken).ConfigureAwait(false);
                }

                string userProfile = cfg.UserProfileDirectoryTemplate is { Length: > 0 } tmpl
                    ? tmpl.Replace("{guid}", _guidGenerator.Create().ToString("N"), StringComparison.Ordinal)
                    : Path.Combine(workDir, "profile");

                await RunSofficeAsync(cfg, inputPath, workDir, userProfile, cancellationToken)
                    .ConfigureAwait(false);

                string outputPath = Path.Combine(workDir, "input.pdf");
                if (!File.Exists(outputPath))
                {
                    throw new InvalidOperationException(
                        $"LibreOffice did not produce '{outputPath}' (source MIME '{sourceContentType}').");
                }

                byte[] bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken)
                    .ConfigureAwait(false);
                return new RenditionResult(bytes, OutputContentType);
            }
            finally
            {
                TryDeleteDirectory(workDir);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunSofficeAsync(
        OfficeRenditionOptions cfg,
        string inputPath,
        string outDir,
        string userProfileDir,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
        {
            FileName = cfg.SofficeBinary,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        Uri profileUri = new(new Uri("file:///"), userProfileDir.Replace('\\', '/'));
        psi.ArgumentList.Add($"-env:UserInstallation={profileUri}");
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--nologo");
        psi.ArgumentList.Add("--nofirststartwizard");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(inputPath);

        using Process process = new() { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Failed to start LibreOffice binary '{cfg.SofficeBinary}'.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(cfg.ConversionTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException(
                $"LibreOffice conversion exceeded {cfg.ConversionTimeout.TotalSeconds:F0}s.");
        }

        if (process.ExitCode != 0)
        {
            string stderr = await process.StandardError.ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);
            LogSofficeFailed(_logger, process.ExitCode, stderr);
            throw new InvalidOperationException(
                $"LibreOffice exited with code {process.ExitCode}: {stderr.Trim()}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() => _gate.Dispose();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "LibreOffice headless exited with code {ExitCode}: {StdErr}")]
    private static partial void LogSofficeFailed(ILogger logger, int exitCode, string stdErr);
}
