using System.Diagnostics;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Granit.Vault.Aws.Diagnostics;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Logging;

namespace Granit.Vault.Aws.Services;

/// <summary>
/// <see cref="ISecretStore"/> backed by AWS Secrets Manager.
/// </summary>
/// <remarks>
/// AWS Secrets Manager natively distinguishes text (<c>SecretString</c>) and binary
/// (<c>SecretBinary</c>) payloads. No Base64 round-trip is needed.
/// </remarks>
internal sealed partial class AwsSecretStore(
    IAmazonSecretsManager secretsManager,
    ILogger<AwsSecretStore> logger) : ISecretStore
{
    /// <inheritdoc />
    public async Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        using Activity? activity = VaultAwsActivitySource.Source
            .StartActivity(VaultAwsActivitySource.Operations.SecretsObtain);
        activity?.SetTag(VaultAwsActivitySource.Tags.KeyName, request.Name);

        GetSecretValueRequest sdkRequest = new()
        {
            SecretId = request.Name,
            VersionId = request.Version?.Identifier,
        };

        GetSecretValueResponse response;
        try
        {
            response = await secretsManager
                .GetSecretValueAsync(sdkRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ResourceNotFoundException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "not_found");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "not_found");
            throw new SecretNotFoundException(request.Name, request.Version?.Identifier, ex);
        }
        catch (AmazonSecretsManagerException ex) when (IsAccessDenied(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "denied");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "denied");
            LogAccessDenied(logger, request.Name);
            throw new SecretAccessDeniedException(request.Name, ex);
        }
        catch (AmazonSecretsManagerException ex) when (IsTransient(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "transient");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "transient");
            throw new SecretVaultTransientException(
                request.Name,
                $"Transient AWS Secrets Manager failure ({ex.ErrorCode}).",
                ex);
        }

        activity?.SetTag(SecretStoreActivityTags.Outcome, "ok");
        DateTimeOffset? createdAt = response.CreatedDate is { } created
            ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc))
            : null;

        var metadata = new SecretMetadata(Version: response.VersionId, CreatedAt: createdAt);

        if (response.SecretBinary is { } binaryStream)
        {
            // MemoryStream is always seekable; rewind defensively in case the SDK returned a
            // stream that was already consumed by a deserializer up the call chain.
            if (binaryStream.Position != 0)
            {
                binaryStream.Position = 0;
            }

            return SecretDescriptor.FromBinary(request.Name, binaryStream.ToArray(), metadata);
        }

        string stringValue = response.SecretString ?? string.Empty;
        return SecretDescriptor.FromString(request.Name, stringValue, metadata);
    }

    private static bool IsAccessDenied(AmazonSecretsManagerException ex) =>
        string.Equals(ex.ErrorCode, "AccessDeniedException", StringComparison.Ordinal)
            || ex.StatusCode == System.Net.HttpStatusCode.Forbidden
            || ex.StatusCode == System.Net.HttpStatusCode.Unauthorized;

    private static bool IsTransient(AmazonSecretsManagerException ex) =>
        ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.InternalServerError
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.GatewayTimeout
            || string.Equals(ex.ErrorCode, "ThrottlingException", StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AWS Secrets Manager denied access to secret '{SecretName}'.")]
    private static partial void LogAccessDenied(ILogger logger, string secretName);
}
