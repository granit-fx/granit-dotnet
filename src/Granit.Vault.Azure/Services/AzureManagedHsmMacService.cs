using System.Diagnostics;
using System.Globalization;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Granit.MultiTenancy;
using Granit.Vault.Azure.Diagnostics;
using Granit.Vault.Azure.Options;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.Services;

/// <summary>
/// Azure Managed HSM-backed <see cref="ITransitMacService"/> using <c>HS256</c>
/// (HMAC-SHA-256) on an <c>oct-HSM</c> key. The key never leaves the HSM
/// boundary. Standard-tier Key Vault has no HMAC primitive — use
/// <see cref="Granit.Vault.Services.SecretBackedMacService"/> there instead.
/// </summary>
internal sealed partial class AzureManagedHsmMacService : ITransitMacService, IDisposable
{
    private const string ProviderName = "azure-hsm";
    private const string TagPrefix = "ahsm:";

    private readonly AzureManagedHsmMacOptions _options;
    private readonly VaultMetrics _metrics;
    private readonly ICurrentTenant? _currentTenant;
    private readonly ILogger<AzureManagedHsmMacService> _logger;
    private readonly Func<Uri, CryptographyClient> _clientFactory;
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<string, CryptographyClient> _clients = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Initializes the service with the default credential chain.</summary>
    public AzureManagedHsmMacService(
        IOptions<AzureManagedHsmMacOptions> options,
        VaultMetrics metrics,
        ICurrentTenant? currentTenant,
        ILogger<AzureManagedHsmMacService> logger)
        : this(options, metrics, currentTenant, logger, DefaultClientFactory)
    {
    }

    /// <summary>Test-friendly constructor — allows substitution of the <see cref="CryptographyClient"/> factory.</summary>
    internal AzureManagedHsmMacService(
        IOptions<AzureManagedHsmMacOptions> options,
        VaultMetrics metrics,
        ICurrentTenant? currentTenant,
        ILogger<AzureManagedHsmMacService> logger,
        Func<Uri, CryptographyClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clientFactory);

        _options = options.Value;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public async Task<TransitMacResult> MacAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        using Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvMac);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, _options.KeyName);
        activity?.SetTag(VaultAzureActivitySource.Tags.VaultUri, _options.HsmUri);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = _currentTenant?.IsAvailable == true ? _currentTenant.Id?.ToString() : null;

        try
        {
            CryptographyClient client = GetClient(_options.KeyVersion);
            SignResult result = await client
                .SignDataAsync(SignatureAlgorithm.HS256, input.ToArray(), cancellationToken)
                .ConfigureAwait(false);

            string version = ExtractVersion(result.KeyId);
            string tag = $"{TagPrefix}{version}:{Convert.ToBase64String(result.Signature)}";

            LogMacSucceeded(_logger, _options.KeyName, version);
            _metrics.RecordOperationCompleted(tenantId, "mac", ProviderName, "success");
            return new TransitMacResult(tag, ParseVersionAsInt(version));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _metrics.RecordOperationError(tenantId, "mac", ProviderName);
            throw new MacKeyNotFoundException(_options.KeyName, ex);
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

        ReadOnlySpan<char> body = mac.AsSpan(TagPrefix.Length);
        int colon = body.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        string version = body[..colon].ToString();
        byte[] presented;
        try
        {
            presented = Convert.FromBase64String(body[(colon + 1)..].ToString());
        }
        catch (FormatException)
        {
            return false;
        }

        using Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvVerify);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, _options.KeyName);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = _currentTenant?.IsAvailable == true ? _currentTenant.Id?.ToString() : null;

        try
        {
            CryptographyClient client = GetClient(version);
            VerifyResult result = await client
                .VerifyDataAsync(SignatureAlgorithm.HS256, input.ToArray(), presented, cancellationToken)
                .ConfigureAwait(false);

            bool ok = result.IsValid;
            _metrics.RecordOperationCompleted(
                tenantId,
                "verify",
                ProviderName,
                ok ? "success" : "rejected");
            return ok;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 403)
        {
            // Version unknown / disabled / forbidden — treat as miss rather than error.
            _metrics.RecordOperationCompleted(tenantId, "verify", ProviderName, "rejected");
            return false;
        }
        catch
        {
            _metrics.RecordOperationError(tenantId, "verify", ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordOperationDuration(tenantId, "verify", ProviderName, stopwatch.Elapsed);
        }
    }

    private CryptographyClient GetClient(string? version)
    {
        string cacheKey = string.IsNullOrEmpty(version) ? "<latest>" : version;
        lock (_gate)
        {
            if (_clients.TryGetValue(cacheKey, out CryptographyClient? cached))
            {
                return cached;
            }

            Uri keyUri = string.IsNullOrEmpty(version)
                ? new Uri($"{_options.HsmUri.TrimEnd('/')}/keys/{_options.KeyName}")
                : new Uri($"{_options.HsmUri.TrimEnd('/')}/keys/{_options.KeyName}/{version}");
            CryptographyClient client = _clientFactory(keyUri);
            _clients[cacheKey] = client;
            return client;
        }
    }

    private static CryptographyClient DefaultClientFactory(Uri keyUri) =>
        new(keyUri, new DefaultAzureCredential());

    private static string ExtractVersion(string keyId)
    {
        int slash = keyId.LastIndexOf('/');
        return slash >= 0 && slash < keyId.Length - 1
            ? keyId[(slash + 1)..]
            : "0";
    }

    private static int ParseVersionAsInt(string version) =>
        int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        lock (_gate)
        {
            _clients.Clear();
            _disposed = true;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Managed HSM MAC produced for key {KeyName} (version {Version}).")]
    private static partial void LogMacSucceeded(ILogger logger, string keyName, string version);
}
