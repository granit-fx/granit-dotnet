using System.Diagnostics;
using System.Globalization;
using System.Net;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.HashiCorp.Diagnostics;
using Granit.Vault.HashiCorp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.Commons;

namespace Granit.Vault.HashiCorp.Services;

/// <summary>
/// <see cref="ISecretStore"/> backed by HashiCorp Vault KV v2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Path resolution:</b> <see cref="SecretRequest.Name"/> is treated as the relative path
/// under the configured <see cref="HashiCorpVaultOptions.KvMountPoint"/>.
/// </para>
/// <para>
/// <b>Version:</b> <see cref="SecretVersion.Identifier"/> must parse to a positive integer.
/// When <c>null</c>, the latest version is returned.
/// </para>
/// <para>
/// <b>Binary convention:</b> if the secret body contains a <c>__binary</c> entry
/// (Base64 string), the value is decoded into <see cref="SecretDescriptor.BinaryValue"/>.
/// Otherwise, a <c>value</c> entry is returned as <see cref="SecretDescriptor.StringValue"/>.
/// If neither is present, the JSON-serialized dictionary is returned as
/// <see cref="SecretDescriptor.StringValue"/>.
/// </para>
/// </remarks>
internal sealed partial class HashiCorpSecretStore(
    IVaultClient vaultClient,
    IOptions<HashiCorpVaultOptions> options,
    ILogger<HashiCorpSecretStore> logger) : ISecretStore
{
    private const string BinaryKey = "__binary";
    private const string ValueKey = "value";

    private readonly string _mountPoint = options.Value.KvMountPoint;

    /// <inheritdoc />
    public async Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        int? version = ParseVersion(request);

        using Activity? activity = VaultHashiCorpActivitySource.Source
            .StartActivity(VaultHashiCorpActivitySource.Operations.KvRead);
        activity?.SetTag(VaultHashiCorpActivitySource.Tags.KeyName, request.Name);
        activity?.SetTag(VaultHashiCorpActivitySource.Tags.MountPoint, _mountPoint);

        Secret<SecretData> secret;
        try
        {
            secret = await vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(request.Name, version, mountPoint: _mountPoint)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (VaultApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "not_found");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "not_found");
            throw new SecretNotFoundException(request.Name, request.Version?.Identifier, ex);
        }
        catch (VaultApiException ex) when (
            ex.HttpStatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "denied");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "denied");
            LogAccessDenied(logger, request.Name);
            throw new SecretAccessDeniedException(request.Name, ex);
        }
        catch (VaultApiException ex) when (IsTransient(ex.HttpStatusCode))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "transient");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "transient");
            throw new SecretVaultTransientException(
                request.Name,
                $"Transient HashiCorp Vault failure (HTTP {(int)ex.HttpStatusCode}).",
                ex);
        }

        activity?.SetTag(SecretStoreActivityTags.Outcome, "ok");
        IDictionary<string, object> data = secret.Data.Data;
        string? resolvedVersion = secret.Data.Metadata?.Version.ToString(CultureInfo.InvariantCulture);
        DateTimeOffset? createdAt = TryParseIso(secret.Data.Metadata?.CreatedTime);

        if (data.TryGetValue(BinaryKey, out object? binaryRaw) && binaryRaw is string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return SecretDescriptor.FromBinary(
                request.Name,
                bytes,
                new SecretMetadata(
                    Version: resolvedVersion,
                    ContentType: "application/octet-stream",
                    CreatedAt: createdAt));
        }

        string stringValue = data.TryGetValue(ValueKey, out object? valueRaw) && valueRaw is not null
            ? valueRaw.ToString() ?? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(data);

        return SecretDescriptor.FromString(
            request.Name,
            stringValue,
            new SecretMetadata(Version: resolvedVersion, CreatedAt: createdAt));
    }

    private static int? ParseVersion(SecretRequest request)
    {
        if (request.Version is null)
        {
            return null;
        }

        if (!int.TryParse(request.Version.Identifier, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
        {
            throw new SecretVaultConfigurationException(
                "Vault:Secret:InvalidVersion",
                $"HashiCorp KV v2 expects a positive integer version identifier but received '{request.Version.Identifier}'.");
        }

        return parsed;
    }

    private static DateTimeOffset? TryParseIso(string? iso) =>
        DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed)
            ? parsed
            : null;

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "HashiCorp Vault denied access to secret '{SecretName}'.")]
    private static partial void LogAccessDenied(ILogger logger, string secretName);
}
