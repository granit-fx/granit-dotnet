using System.Diagnostics;
using System.Security.Cryptography;
using Granit.MultiTenancy;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Services;

/// <summary>
/// Portable <see cref="ITransitMacService"/> implementation that pulls a 32-byte HMAC
/// key from any <see cref="ISecretStore"/> and computes HMAC-SHA256 locally.
/// Use this when the underlying provider lacks a native HMAC primitive
/// (e.g. Azure Key Vault Standard tier) or to avoid the per-operation Vault round-trip.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trust-boundary trade-off:</b> the key crosses the vault boundary into process
/// memory. Memory is zeroed on dispose and on every refresh. See
/// <see cref="SecretBackedMacOptions"/> for the full rationale.
/// </para>
/// <para>
/// <b>Tag format:</b> <c>sbm:v{N}:{base64Url(hmac)}</c> where <c>N</c> is <c>1</c> for the
/// current key and <c>0</c> for the previous key (rolling window). Callers MUST NOT
/// parse the tag — pass it back to <see cref="VerifyAsync"/> verbatim.
/// </para>
/// </remarks>
public sealed partial class SecretBackedMacService : ITransitMacService, IDisposable
{
    private const string ProviderName = "secret-backed";
    private const string TagPrefix = "sbm:";
    private const string VerifyOperation = "verify";
    private const int RequiredKeySizeBytes = 32;

    private readonly ISecretStore _secretStore;
    private readonly SecretBackedMacOptions _options;
    private readonly VaultMetrics _metrics;
    private readonly ICurrentTenant? _currentTenant;
    private readonly ILogger<SecretBackedMacService> _logger;

    private readonly System.Threading.Lock _gate = new();
    private byte[]? _currentKey;
    private byte[]? _previousKey;
    private bool _disposed;

    /// <summary>Initializes the service. Use <see cref="RefreshAsync"/> at startup to populate the cache.</summary>
    public SecretBackedMacService(
        ISecretStore secretStore,
        IOptions<SecretBackedMacOptions> options,
        VaultMetrics metrics,
        ICurrentTenant? currentTenant,
        ILogger<SecretBackedMacService> logger)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _secretStore = secretStore;
        _options = options.Value;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TransitMacResult> MacAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        await EnsureCurrentLoadedAsync(cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = _currentTenant?.IsAvailable == true ? _currentTenant.Id?.ToString() : null;

        try
        {
            byte[] tag = ComputeWithKey(_currentKey!, input.Span);
            string mac = $"{TagPrefix}v1:{Base64UrlEncode(tag)}";
            CryptographicOperations.ZeroMemory(tag);
            _metrics.RecordOperationCompleted(tenantId, "mac", ProviderName, "success");
            return new TransitMacResult(mac, KeyVersion: 1);
        }
        catch
        {
            _metrics.RecordOperationError(tenantId, "mac", ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordOperationDuration(tenantId, "mac", ProviderName, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        string mac,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentException.ThrowIfNullOrEmpty(mac);

        if (!mac.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        await EnsureCurrentLoadedAsync(cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = _currentTenant?.IsAvailable == true ? _currentTenant.Id?.ToString() : null;

        try
        {
            ReadOnlySpan<char> body = mac.AsSpan(TagPrefix.Length);
            int colon = body.IndexOf(':');
            if (colon <= 0)
            {
                _metrics.RecordOperationCompleted(tenantId, VerifyOperation, ProviderName, "rejected");
                return false;
            }

            byte[] presented;
            try
            {
                presented = Base64UrlDecode(body[(colon + 1)..].ToString());
            }
            catch (FormatException)
            {
                _metrics.RecordOperationCompleted(tenantId, VerifyOperation, ProviderName, "rejected");
                return false;
            }

            // Constant-time fallback — always compute both candidates when a previous key
            // exists, so the latency does not leak which key matched.
            byte[] currentExpected = ComputeWithKey(_currentKey!, input.Span);
            bool currentMatch = CryptographicOperations.FixedTimeEquals(currentExpected, presented);
            CryptographicOperations.ZeroMemory(currentExpected);

            bool previousMatch = false;
            byte[]? previousSnapshot = _previousKey;
            if (previousSnapshot is not null)
            {
                byte[] previousExpected = ComputeWithKey(previousSnapshot, input.Span);
                previousMatch = CryptographicOperations.FixedTimeEquals(previousExpected, presented);
                CryptographicOperations.ZeroMemory(previousExpected);
            }

            CryptographicOperations.ZeroMemory(presented);
            bool ok = currentMatch || previousMatch;
            _metrics.RecordOperationCompleted(
                tenantId,
                VerifyOperation,
                ProviderName,
                ok ? "success" : "rejected");
            return ok;
        }
        catch
        {
            _metrics.RecordOperationError(tenantId, VerifyOperation, ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordOperationDuration(tenantId, VerifyOperation, ProviderName, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Re-reads the configured secrets from the vault and replaces the in-memory key
    /// material. Called by the background refresh hosted service and at startup;
    /// safe to call concurrently — the lock serialises writes, reads stay lock-free.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] newCurrent = await ReadKeyAsync(_options.CurrentSecretName, cancellationToken).ConfigureAwait(false);
        byte[]? newPrevious = null;
        if (!string.IsNullOrEmpty(_options.PreviousSecretName))
        {
            try
            {
                newPrevious = await ReadKeyAsync(_options.PreviousSecretName, cancellationToken).ConfigureAwait(false);
            }
            catch (SecretNotFoundException)
            {
                // Operator has not yet provisioned a previous key — first deployment.
                LogPreviousKeyMissing(_logger, _options.PreviousSecretName);
            }
        }

        lock (_gate)
        {
            if (_currentKey is not null)
            {
                CryptographicOperations.ZeroMemory(_currentKey);
            }
            if (_previousKey is not null)
            {
                CryptographicOperations.ZeroMemory(_previousKey);
            }
            _currentKey = newCurrent;
            _previousKey = newPrevious;
        }

        LogRefreshed(_logger, _options.CurrentSecretName, newPrevious is not null);
    }

    private async Task EnsureCurrentLoadedAsync(CancellationToken cancellationToken)
    {
        if (_currentKey is not null)
        {
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadKeyAsync(string secretName, CancellationToken cancellationToken)
    {
        SecretDescriptor descriptor = await _secretStore
            .GetSecretAsync(SecretRequest.Latest(secretName), cancellationToken)
            .ConfigureAwait(false);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(descriptor.AsString());
        }
        catch (FormatException ex)
        {
            throw new MacVerificationException(
                secretName,
                "SecretBackedMacService expects a base64-encoded 32-byte key payload.",
                ex);
        }

        if (bytes.Length != RequiredKeySizeBytes)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new MacVerificationException(
                secretName,
                $"SecretBackedMacService key must be exactly {RequiredKeySizeBytes} bytes after base64 decoding (got {bytes.Length}).");
        }

        return bytes;
    }

    private static byte[] ComputeWithKey(byte[] key, ReadOnlySpan<byte> input)
    {
        byte[] mac = new byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, input, mac);
        return mac;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        int padding = (4 - (padded.Length % 4)) % 4;
        if (padding > 0)
        {
            padded += new string('=', padding);
        }

        return Convert.FromBase64String(padded);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            if (_currentKey is not null)
            {
                CryptographicOperations.ZeroMemory(_currentKey);
                _currentKey = null;
            }
            if (_previousKey is not null)
            {
                CryptographicOperations.ZeroMemory(_previousKey);
                _previousKey = null;
            }
            _disposed = true;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "SecretBackedMacService refreshed key from secret '{SecretName}' (previous-key={HasPrevious}).")]
    private static partial void LogRefreshed(ILogger logger, string secretName, bool hasPrevious);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SecretBackedMacService: previous-key secret '{SecretName}' is not provisioned yet — rolling verify disabled until the operator creates it.")]
    private static partial void LogPreviousKeyMissing(ILogger logger, string secretName);
}
