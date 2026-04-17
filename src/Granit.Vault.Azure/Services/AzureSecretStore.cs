using System.Diagnostics;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Granit.Vault.Azure.Diagnostics;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Logging;

namespace Granit.Vault.Azure.Services;

/// <summary>
/// <see cref="ISecretStore"/> backed by Azure Key Vault.
/// </summary>
/// <remarks>
/// Azure Key Vault secrets are always strings. Binary payloads are represented by Base64-
/// encoded strings with <c>ContentType = "application/octet-stream"</c>; this store
/// decodes them into <see cref="SecretDescriptor.BinaryValue"/> automatically.
/// </remarks>
internal sealed partial class AzureSecretStore(
    SecretClient secretClient,
    ILogger<AzureSecretStore> logger) : ISecretStore
{
    private const string OctetStreamContentType = "application/octet-stream";

    /// <inheritdoc />
    public async Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        using Activity? activity = VaultAzureActivitySource.Source
            .StartActivity(VaultAzureActivitySource.Operations.AkvGetSecret);
        activity?.SetTag(VaultAzureActivitySource.Tags.KeyName, request.Name);

        Response<KeyVaultSecret> response;
        try
        {
            response = await secretClient
                .GetSecretAsync(request.Name, request.Version?.Identifier, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "not_found");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "not_found");
            throw new SecretNotFoundException(request.Name, request.Version?.Identifier, ex);
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "denied");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "denied");
            LogAccessDenied(logger, request.Name);
            throw new SecretAccessDeniedException(request.Name, ex);
        }
        catch (RequestFailedException ex) when (IsTransient(ex.Status))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "transient");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "transient");
            throw new SecretVaultTransientException(
                request.Name,
                $"Transient Azure Key Vault failure (HTTP {ex.Status}).",
                ex);
        }

        activity?.SetTag(SecretStoreActivityTags.Outcome, "ok");
        KeyVaultSecret secret = response.Value;
        SecretProperties props = secret.Properties;
        IReadOnlyDictionary<string, string>? tags = props.Tags is { Count: > 0 }
            ? new Dictionary<string, string>(props.Tags)
            : null;

        var metadata = new SecretMetadata(
            Version: props.Version,
            ContentType: props.ContentType,
            CreatedAt: props.CreatedOn,
            ExpiresOn: props.ExpiresOn,
            NotBefore: props.NotBefore,
            Tags: tags);

        if (string.Equals(props.ContentType, OctetStreamContentType, StringComparison.OrdinalIgnoreCase))
        {
            byte[] bytes = Convert.FromBase64String(secret.Value);
            return SecretDescriptor.FromBinary(request.Name, bytes, metadata);
        }

        return SecretDescriptor.FromString(request.Name, secret.Value, metadata);
    }

    private static bool IsTransient(int status) =>
        status is 408 or 429 or 500 or 502 or 503 or 504;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Azure Key Vault denied access to secret '{SecretName}'.")]
    private static partial void LogAccessDenied(ILogger logger, string secretName);
}
