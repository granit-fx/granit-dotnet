using System.Diagnostics;
using System.Globalization;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Granit.MultiTenancy;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.GoogleCloud.Diagnostics;
using Granit.Vault.GoogleCloud.Options;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.GoogleCloud.Services;

/// <summary>
/// Google Cloud KMS-backed <see cref="ITransitMacService"/>. Uses <c>MacSign</c> /
/// <c>MacVerify</c> on a <c>purpose=MAC</c>, <c>algorithm=HMAC_SHA256</c> CryptoKey.
/// Versioning is native — <c>MacSign</c> picks the primary version automatically
/// and the implementation pins <c>MacVerify</c> to the version that signed the tag,
/// refusing tags signed by a now-DISABLED or DESTROYED version.
/// </summary>
internal sealed partial class GoogleCloudKmsMacService(
    KeyManagementServiceClient kmsClient,
    IOptions<GoogleCloudVaultOptions> vaultOptions,
    IOptions<GoogleCloudKmsMacOptions> macOptions,
    VaultMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<GoogleCloudKmsMacService> logger) : ITransitMacService
{
    private const string ProviderName = "googlecloud";
    private const string TagPrefix = "gcpkms:";
    private const string VerifyOperation = "verify";

    private readonly CryptoKeyName _cryptoKeyName = new(
        vaultOptions.Value.ProjectId,
        vaultOptions.Value.Location,
        vaultOptions.Value.KeyRing,
        macOptions.Value.CryptoKeyId);

    public async Task<TransitMacResult> MacAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        using Activity? activity = VaultGoogleCloudActivitySource.Source.StartActivity(
            VaultGoogleCloudActivitySource.Operations.KmsMac);
        activity?.SetTag(VaultGoogleCloudActivitySource.Tags.KeyName, _cryptoKeyName.CryptoKeyId);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            // MacSign on the CryptoKey name (without version) resolves to the primary version.
            MacSignResponse response = await kmsClient.MacSignAsync(new MacSignRequest
            {
                Name = _cryptoKeyName.ToString(),
                Data = ByteString.CopyFrom(input.Span),
            }, cancellationToken).ConfigureAwait(false);

            // response.Name is the full version resource — extract the version number.
            int version = ExtractVersion(response.Name);
            string tag = $"{TagPrefix}v{version}:{Convert.ToBase64String(response.Mac.ToByteArray())}";

            LogMacSucceeded(logger, _cryptoKeyName.CryptoKeyId, version);
            metrics.RecordOperationCompleted(tenantId, "mac", ProviderName, "success");
            return new TransitMacResult(tag, version);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            metrics.RecordOperationError(tenantId, "mac", ProviderName);
            throw new MacKeyNotFoundException(_cryptoKeyName.CryptoKeyId, ex);
        }
        catch
        {
            metrics.RecordOperationError(tenantId, "mac", ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordOperationDuration(tenantId, "mac", ProviderName, stopwatch.Elapsed);
        }
    }

    public async Task<bool> VerifyAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        string mac,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentException.ThrowIfNullOrEmpty(mac);

        if (!mac.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> body = mac.AsSpan(TagPrefix.Length);
        int colon = body.IndexOf(':');
        if (colon <= 1 || body[0] != 'v')
        {
            return false;
        }

        if (!int.TryParse(body[1..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) || version <= 0)
        {
            return false;
        }

        byte[] presented;
        try
        {
            presented = Convert.FromBase64String(body[(colon + 1)..].ToString());
        }
        catch (FormatException)
        {
            return false;
        }

        using Activity? activity = VaultGoogleCloudActivitySource.Source.StartActivity(
            VaultGoogleCloudActivitySource.Operations.KmsVerify);
        activity?.SetTag(VaultGoogleCloudActivitySource.Tags.KeyName, _cryptoKeyName.CryptoKeyId);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        CryptoKeyVersionName versionName = new(
            _cryptoKeyName.ProjectId,
            _cryptoKeyName.LocationId,
            _cryptoKeyName.KeyRingId,
            _cryptoKeyName.CryptoKeyId,
            version.ToString(CultureInfo.InvariantCulture));

        try
        {
            // Refuse verification against non-ENABLED versions — Cloud KMS will happily
            // verify against DISABLED tags, which contradicts the operator's intent.
            CryptoKeyVersion versionState;
            try
            {
                versionState = await kmsClient
                    .GetCryptoKeyVersionAsync(versionName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                metrics.RecordOperationCompleted(tenantId, VerifyOperation, ProviderName, "rejected");
                return false;
            }

            if (versionState.State != CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled)
            {
                LogVersionDisabled(logger, _cryptoKeyName.CryptoKeyId, version, versionState.State.ToString());
                metrics.RecordOperationCompleted(tenantId, VerifyOperation, ProviderName, "rejected");
                return false;
            }

            MacVerifyResponse response = await kmsClient.MacVerifyAsync(new MacVerifyRequest
            {
                Name = versionName.ToString(),
                Data = ByteString.CopyFrom(input.Span),
                Mac = ByteString.CopyFrom(presented),
            }, cancellationToken).ConfigureAwait(false);

            bool ok = response.Success;
            metrics.RecordOperationCompleted(
                tenantId,
                VerifyOperation,
                ProviderName,
                ok ? "success" : "rejected");
            return ok;
        }
        catch
        {
            metrics.RecordOperationError(tenantId, VerifyOperation, ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordOperationDuration(tenantId, VerifyOperation, ProviderName, stopwatch.Elapsed);
        }
    }

    private static int ExtractVersion(string fullResourceName)
    {
        int slash = fullResourceName.LastIndexOf('/');
        if (slash < 0 || slash == fullResourceName.Length - 1)
        {
            return 0;
        }

        ReadOnlySpan<char> tail = fullResourceName.AsSpan(slash + 1);
        return int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cloud KMS MAC produced for key {KeyId} (version {Version}).")]
    private static partial void LogMacSucceeded(ILogger logger, string keyId, int version);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cloud KMS verify refused — key {KeyId} version {Version} is not enabled ({State}).")]
    private static partial void LogVersionDisabled(ILogger logger, string keyId, int version, string state);
}
