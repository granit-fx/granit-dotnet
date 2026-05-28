using System.Diagnostics;
using System.Text.RegularExpressions;
using Granit.MultiTenancy;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.HashiCorp.Diagnostics;
using Granit.Vault.HashiCorp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Granit.Vault.HashiCorp.Services;

/// <summary>
/// HashiCorp Vault Transit-backed <see cref="ITransitMacService"/>. Uses the
/// <c>transit/hmac</c> / <c>transit/verify</c> endpoints with SHA-256, so the key
/// never leaves Vault and rolling rotation is handled natively via
/// <c>min_decryption_version</c>.
/// </summary>
internal sealed partial class HashiCorpTransitMacService(
    IVaultClient vaultClient,
    IOptions<HashiCorpVaultOptions> options,
    VaultMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<HashiCorpTransitMacService> logger) : ITransitMacService
{
    private const string ProviderName = "hashicorp";
    private readonly HashiCorpVaultOptions _options = options.Value;

    // Vault Transit HMAC tag format: vault:v{N}:base64
    [GeneratedRegex(@"^vault:v(?<version>\d+):", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VaultVersionRegex();

    public async Task<TransitMacResult> MacAsync(
        string keyName,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyName);

        using Activity? activity = VaultHashiCorpActivitySource.Source.StartActivity(
            VaultHashiCorpActivitySource.Operations.TransitMac);
        activity?.SetTag(VaultHashiCorpActivitySource.Tags.KeyName, keyName);
        activity?.SetTag(VaultHashiCorpActivitySource.Tags.MountPoint, _options.TransitMountPoint);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            HmacRequestOptions request = new()
            {
                Algorithm = TransitHashAlgorithm.SHA2_256,
                Base64EncodedInput = Convert.ToBase64String(input.Span),
            };

            // VaultSharp does not propagate the cancellation token — WaitAsync gives us
            // a defensive timeout while staying compatible with the existing pattern.
            Secret<HmacResponse> response = await vaultClient.V1.Secrets.Transit
                .GenerateHmacAsync(keyName, request, mountPoint: _options.TransitMountPoint)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            string tag = response.Data.Hmac;
            int version = ParseKeyVersion(tag);

            LogMacSucceeded(logger, keyName, version);
            metrics.RecordOperationCompleted(tenantId, "mac", ProviderName, "success");
            return new TransitMacResult(tag, version);
        }
        catch (Exception ex) when (IsKeyNotFound(ex))
        {
            metrics.RecordOperationError(tenantId, "mac", ProviderName);
            throw new MacKeyNotFoundException(keyName, ex);
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

        if (!VaultVersionRegex().IsMatch(mac))
        {
            // Vault refuses tags that don't match its own format — short-circuit
            // before the round-trip and avoid a misleading "verify failed" log.
            return false;
        }

        using Activity? activity = VaultHashiCorpActivitySource.Source.StartActivity(
            VaultHashiCorpActivitySource.Operations.TransitVerify);
        activity?.SetTag(VaultHashiCorpActivitySource.Tags.KeyName, keyName);
        activity?.SetTag(VaultHashiCorpActivitySource.Tags.MountPoint, _options.TransitMountPoint);

        var stopwatch = Stopwatch.StartNew();
        string? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;

        try
        {
            VerifyRequestOptions request = new()
            {
                HashAlgorithm = TransitHashAlgorithm.SHA2_256,
                Base64EncodedInput = Convert.ToBase64String(input.Span),
                Hmac = mac,
            };

            Secret<VerifyResponse> response = await vaultClient.V1.Secrets.Transit
                .VerifySignedDataAsync(keyName, request, mountPoint: _options.TransitMountPoint)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            bool valid = ExtractValid(response.Data);
            metrics.RecordOperationCompleted(
                tenantId,
                "verify",
                ProviderName,
                valid ? "success" : "rejected");
            return valid;
        }
        catch (Exception ex) when (IsKeyNotFound(ex))
        {
            metrics.RecordOperationError(tenantId, "verify", ProviderName);
            throw new MacKeyNotFoundException(keyName, ex);
        }
        catch
        {
            metrics.RecordOperationError(tenantId, "verify", ProviderName);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            metrics.RecordOperationDuration(tenantId, "verify", ProviderName, stopwatch.Elapsed);
        }
    }

    private static int ParseKeyVersion(string tag)
    {
        Match match = VaultVersionRegex().Match(tag);
        return match.Success && int.TryParse(match.Groups["version"].ValueSpan, out int v) ? v : 0;
    }

    private static bool ExtractValid(VerifyResponse data)
    {
        if (data.BatchResults is { Count: > 0 } batch)
        {
            return batch[0].Valid;
        }

        // VaultSharp's VerifyResponse exposes both single and batch shapes; when single is
        // used the BatchResults list is empty. Vault returns a top-level "valid" field;
        // the SDK maps it to BatchResults[0].Valid when echoing, so this branch only
        // triggers when the SDK contract changes — defensive false rather than throw.
        return false;
    }

    private static bool IsKeyNotFound(Exception ex)
    {
        string message = ex.Message;
        return message.Contains("encryption key not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("404", StringComparison.Ordinal)
            || message.Contains("no existing key", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Transit MAC produced for key {KeyName} (version {Version}).")]
    private static partial void LogMacSucceeded(ILogger logger, string keyName, int version);
}
