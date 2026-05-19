using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Granit.IO.Diagnostics;
using Granit.IO.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.IO.Internal;

/// <summary>
/// Default <see cref="ITempFileFactory"/>. Singleton.
/// </summary>
internal sealed partial class DefaultTempFileFactory : ITempFileFactory
{
    [GeneratedRegex(@"^[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,16}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionRegex();

    private readonly TempFileOptions _options;
    private readonly ICurrentTenant? _currentTenant;
    private readonly IOMetrics _metrics;
    private readonly ILogger<DefaultTempFileFactory> _logger;
    private readonly string _rootDirectory;

    public DefaultTempFileFactory(
        IOptions<TempFileOptions> options,
        IOMetrics metrics,
        ILogger<DefaultTempFileFactory> logger,
        ICurrentTenant? currentTenant = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _currentTenant = currentTenant;
        _metrics = metrics;
        _logger = logger;
        _rootDirectory = _options.EffectiveRootDirectory;
    }

    /// <summary>Root directory in use by the factory (resolved at construction time).</summary>
    public string RootDirectory => _rootDirectory;

    public ValueTask<ITempFile> CreateAsync(string category, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(extension);

        if (!CategoryRegex().IsMatch(category))
        {
            throw new ArgumentException(
                $"Temp file category must match [a-z0-9-]{{1,32}}; got '{category}'.",
                nameof(category));
        }

        string normalizedExt = extension.StartsWith('.') ? extension[1..] : extension;
        if (!ExtensionRegex().IsMatch(normalizedExt))
        {
            throw new ArgumentException(
                $"Temp file extension must be alphanumeric, 1..16 chars; got '{extension}'.",
                nameof(extension));
        }

        ct.ThrowIfCancellationRequested();

        string? tenantId = ResolveTenantId();
        string tenantSegment = tenantId is not null && _options.TenantPartition
            ? $"t-{tenantId}"
            : "host";

        string directory = Path.Combine(_rootDirectory, tenantSegment, category);
        Directory.CreateDirectory(directory);
        ApplyDirectoryMode(directory);

        // Temp file names use Guid.NewGuid (random V4) deliberately: temp files do not
        // back a clustered index, and randomness avoids predictable paths (OWASP ASVS V12.4.1).
#pragma warning disable GRSEC002
        string fileName = $"{Guid.NewGuid():N}.{normalizedExt}";
#pragma warning restore GRSEC002
        string fullPath = Path.Combine(directory, fileName);

        UnixFileMode unixCreateMode = _options.ChmodOwnerOnly && !OperatingSystem.IsWindows()
            ? UnixFileMode.UserRead | UnixFileMode.UserWrite
            : UnixFileMode.None;

        FileStreamOptions fso = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            BufferSize = 4096,
        };

        if (!OperatingSystem.IsWindows() && _options.ChmodOwnerOnly)
        {
            fso.UnixCreateMode = unixCreateMode;
        }

        FileStream fileStream = new(fullPath, fso);

        ApplyWindowsAcl(fullPath);

        LimitedStream limited = new(fileStream, _options.MaxSizeBytes);
        _metrics.RecordCreated(category, tenantId);

        ITempFile temp = new TempFile(
            fullPath,
            limited,
            _options.MaxSizeBytes,
            _metrics,
            category,
            tenantId,
            _logger);

        return ValueTask.FromResult(temp);
    }

    private string? ResolveTenantId()
    {
        if (_currentTenant is null || !_currentTenant.IsAvailable || _currentTenant.Id is null)
        {
            return null;
        }

        return _currentTenant.Id.Value.ToString("N");
    }

    private void ApplyDirectoryMode(string directory)
    {
        if (OperatingSystem.IsWindows() || !_options.ChmodOwnerOnly)
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (IOException ex)
        {
            LogDirectoryModeFailed(_logger, directory, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDirectoryModeFailed(_logger, directory, ex);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void ApplyWindowsAclCore(string path)
    {
        FileInfo info = new(path);
        FileSecurity security = info.GetAccessControl();
        // Inherited rules would let non-owner principals read the file; remove them.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        SecurityIdentifier? owner = WindowsIdentity.GetCurrent().Owner;
        if (owner is not null)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                owner,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
        }

        info.SetAccessControl(security);
    }

    private void ApplyWindowsAcl(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            ApplyWindowsAclCore(path);
        }
        catch (IOException ex)
        {
            LogAclFailed(_logger, path, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogAclFailed(_logger, path, ex);
        }
        catch (PlatformNotSupportedException ex)
        {
            LogAclFailed(_logger, path, ex);
        }
    }

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Debug,
        Message = "Failed to apply 0700 mode to temp directory '{Directory}'.")]
    private static partial void LogDirectoryModeFailed(ILogger logger, string directory, Exception exception);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Warning,
        Message = "Failed to apply restrictive NTFS ACL to temp file '{Path}'.")]
    private static partial void LogAclFailed(ILogger logger, string path, Exception exception);
}
