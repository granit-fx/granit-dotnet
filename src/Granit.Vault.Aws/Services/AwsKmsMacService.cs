using System.Diagnostics;
using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Granit.MultiTenancy;
using Granit.Vault.Aws.Diagnostics;
using Granit.Vault.Aws.Options;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Aws.Services;

/// <summary>
/// AWS KMS-backed <see cref="ITransitMacService"/>. Uses <c>GenerateMac</c> /
/// <c>VerifyMac</c> with an <c>HMAC_256</c> KMS key (key spec <c>HMAC_256</c>,
/// usage <c>GENERATE_VERIFY_MAC</c>). Rolling rotation is implemented by issuing
/// the verify against the <c>PreviousAlias</c> as a constant-time fallback —
/// AWS KMS does not version HMAC keys in place.
/// </summary>
internal sealed partial class AwsKmsMacService(
    IAmazonKeyManagementService kmsClient,
    IOptions<AwsKmsMacOptions> macOptions,
    VaultMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<AwsKmsMacService> logger) : ITransitMacService
{
    private const string ProviderName = "aws-kms";
    private const string TagPrefix = "akms:";
    private const string CurrentVersionLabel = "current";
    private const string PreviousVersionLabel = "previous";

    private readonly AwsKmsMacOptions _options = macOptions.Value;

    /// <inheritdoc />
    public async Task<TransitMacResult> MacAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        using Activity? activity = VaultAwsActivitySource.Source.StartActivity(
            VaultAwsActivitySource.Operations.KmsMac);
        activity?.SetTag(VaultAwsActivitySource.Tags.KeyName, _options.CurrentAlias);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            byte[] tag = await GenerateAsync(_options.CurrentAlias, input, cancellationToken).ConfigureAwait(false);
            string mac = $"{TagPrefix}{CurrentVersionLabel}:{Convert.ToBase64String(tag)}";
            CryptographicOperations.ZeroMemory(tag);

            LogMacSucceeded(logger, _options.CurrentAlias);
            metrics.RecordOperationCompleted(tenantId, "mac", ProviderName, "success");
            return new TransitMacResult(mac, KeyVersion: 1);
        }
        catch (NotFoundException ex)
        {
            metrics.RecordOperationError(tenantId, "mac", ProviderName);
            throw new MacKeyNotFoundException(_options.CurrentAlias, ex);
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

    /// <inheritdoc />
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

        string body = mac[TagPrefix.Length..];
        int colon = body.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            return false;
        }

        string versionLabel = body[..colon];
        byte[] tagBytes;
        try
        {
            tagBytes = Convert.FromBase64String(body[(colon + 1)..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using Activity? activity = VaultAwsActivitySource.Source.StartActivity(
            VaultAwsActivitySource.Operations.KmsVerify);
        activity?.SetTag(VaultAwsActivitySource.Tags.KeyName, _options.CurrentAlias);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            // Constant-time fallback — always issue both calls when previous alias is
            // configured, regardless of the first result, so latency does not leak
            // which alias matched.
            Task<bool> currentTask = VerifyAgainstAsync(_options.CurrentAlias, input, tagBytes, cancellationToken);
            Task<bool> previousTask = _options.PreviousAlias is { Length: > 0 } prev
                ? VerifyAgainstAsync(prev, input, tagBytes, cancellationToken)
                : Task.FromResult(false);

            bool currentMatch = await currentTask.ConfigureAwait(false);
            bool previousMatch = await previousTask.ConfigureAwait(false);

            // Honour the tag's hint about which alias it should match — protects against
            // a "current-then-previous" match path that would let an attacker reuse a
            // tag signed under previous against a payload that only the current key
            // authorises (no real escalation, but tightens the spec).
            bool ok = string.Equals(versionLabel, CurrentVersionLabel, StringComparison.Ordinal)
                ? currentMatch
                : string.Equals(versionLabel, PreviousVersionLabel, StringComparison.Ordinal) && previousMatch;

            metrics.RecordOperationCompleted(
                tenantId,
                "verify",
                ProviderName,
                ok ? "success" : "rejected");
            return ok;
        }
        catch
        {
            metrics.RecordOperationError(tenantId, "verify", ProviderName);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tagBytes);
            stopwatch.Stop();
            metrics.RecordOperationDuration(tenantId, "verify", ProviderName, stopwatch.Elapsed);
        }
    }

    private async Task<byte[]> GenerateAsync(
        string alias,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken)
    {
        GenerateMacRequest request = new()
        {
            KeyId = alias,
            MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256,
            Message = new MemoryStream(input.ToArray(), writable: false),
        };

        GenerateMacResponse response = await kmsClient
            .GenerateMacAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return response.Mac.ToArray();
    }

    private async Task<bool> VerifyAgainstAsync(
        string alias,
        ReadOnlyMemory<byte> input,
        byte[] presented,
        CancellationToken cancellationToken)
    {
        try
        {
            VerifyMacRequest request = new()
            {
                KeyId = alias,
                MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256,
                Mac = new MemoryStream(presented, writable: false),
                Message = new MemoryStream(input.ToArray(), writable: false),
            };

            VerifyMacResponse response = await kmsClient
                .VerifyMacAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.MacValid == true;
        }
        catch (KMSInvalidMacException)
        {
            return false;
        }
        catch (NotFoundException)
        {
            // Alias unset (operator hasn't provisioned previous yet) — treat as miss.
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "KMS MAC produced under alias {Alias}.")]
    private static partial void LogMacSucceeded(ILogger logger, string alias);
}
