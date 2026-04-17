using System.Diagnostics;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Granit.Vault.Aws.Diagnostics;
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
            throw new SecretNotFoundException(request.Name, request.Version?.Identifier, ex);
        }
        catch (AmazonSecretsManagerException ex) when (IsAccessDenied(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "denied");
            LogAccessDenied(logger, request.Name);
            throw new SecretAccessDeniedException(request.Name, ex);
        }
        catch (AmazonSecretsManagerException ex) when (IsTransient(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "transient");
            throw new SecretVaultTransientException(
                request.Name,
                $"Transient AWS Secrets Manager failure ({ex.ErrorCode}).",
                ex);
        }

        DateTimeOffset? createdAt = response.CreatedDate is { } created
            ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc))
            : null;

        if (response.SecretBinary is { } binaryStream)
        {
            byte[] bytes = ToArray(binaryStream);
            return SecretDescriptor.FromBinary(
                request.Name,
                bytes,
                version: response.VersionId,
                createdAt: createdAt);
        }

        string stringValue = response.SecretString ?? string.Empty;
        return SecretDescriptor.FromString(
            request.Name,
            stringValue,
            version: response.VersionId,
            createdAt: createdAt);
    }

    private static byte[] ToArray(MemoryStream stream)
    {
        if (stream.Position != 0 && stream.CanSeek)
        {
            stream.Position = 0;
        }

        return stream.ToArray();
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
