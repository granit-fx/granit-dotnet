using System.Diagnostics;
using System.Text;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using Granit.Vault.Diagnostics;
using Granit.Vault.Exceptions;
using Granit.Vault.GoogleCloud.Diagnostics;
using Granit.Vault.GoogleCloud.Options;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.GoogleCloud.Services;

/// <summary>
/// <see cref="ISecretStore"/> backed by Google Cloud Secret Manager.
/// </summary>
/// <remarks>
/// <para>
/// <b>Path resolution:</b> <see cref="SecretRequest.Name"/> is the secret id (e.g.
/// <c>mqtt-client-cert</c>). The project id is resolved from
/// <see cref="GoogleCloudVaultOptions.ProjectId"/>. Fully-qualified names
/// (<c>projects/.../secrets/...</c>) pass through unchanged.
/// </para>
/// <para>
/// <b>Binary handling:</b> Secret Manager payloads are native binary
/// (<c>ByteString</c>). When the payload is valid UTF-8, both
/// <see cref="SecretDescriptor.StringValue"/> and <see cref="SecretDescriptor.BinaryValue"/>
/// would be ambiguous — this store surfaces the payload as
/// <see cref="SecretDescriptor.BinaryValue"/> by default, matching the provider's native shape.
/// </para>
/// </remarks>
internal sealed partial class GoogleCloudSecretStore(
    SecretManagerServiceClient secretManagerClient,
    IOptions<GoogleCloudVaultOptions> options,
    ILogger<GoogleCloudSecretStore> logger) : ISecretStore
{
    private readonly string _projectId = options.Value.ProjectId;

    /// <inheritdoc />
    public async Task<SecretDescriptor> GetSecretAsync(
        SecretRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        SecretVersionName versionName = BuildVersionName(request);

        using Activity? activity = VaultGoogleCloudActivitySource.Source
            .StartActivity(VaultGoogleCloudActivitySource.Operations.SecretsObtain);
        activity?.SetTag(VaultGoogleCloudActivitySource.Tags.KeyName, request.Name);
        activity?.SetTag(VaultGoogleCloudActivitySource.Tags.ProjectId, versionName.ProjectId);

        AccessSecretVersionResponse response;
        try
        {
            response = await secretManagerClient
                .AccessSecretVersionAsync(versionName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "not_found");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "not_found");
            throw new SecretNotFoundException(request.Name, request.Version?.Identifier, ex);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.PermissionDenied or StatusCode.Unauthenticated)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "denied");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "denied");
            LogAccessDenied(logger, request.Name);
            throw new SecretAccessDeniedException(request.Name, ex);
        }
        catch (RpcException ex) when (IsTransient(ex.StatusCode))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "transient");
            activity?.SetTag(SecretStoreActivityTags.Outcome, "transient");
            throw new SecretVaultTransientException(
                request.Name,
                $"Transient GCP Secret Manager failure ({ex.StatusCode}).",
                ex);
        }

        activity?.SetTag(SecretStoreActivityTags.Outcome, "ok");
        ByteString payload = response.Payload.Data;
        var metadata = new SecretMetadata(Version: ExtractVersion(response.Name));

        // GCP payloads are always binary natively. When the body is valid UTF-8 and no binary
        // content type is hinted, surface the payload as StringValue so consumers of text
        // secrets don't have to call AsString(). Callers who explicitly want raw bytes
        // (certificates, binary blobs) use AsBytes() regardless of which facet is set.
        if (TryDecodeUtf8(payload, out string? text))
        {
            return SecretDescriptor.FromString(request.Name, text!, metadata);
        }

        return SecretDescriptor.FromBinary(request.Name, payload.ToByteArray(), metadata);
    }

    private static bool TryDecodeUtf8(ByteString payload, out string? text)
    {
        try
        {
            var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = decoder.GetString(payload.Span);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = null;
            return false;
        }
    }

    private SecretVersionName BuildVersionName(SecretRequest request)
    {
        string version = request.Version?.Identifier ?? "latest";

        if (request.Name.StartsWith("projects/", StringComparison.Ordinal))
        {
            // Caller passed a fully-qualified resource name — try to parse it directly.
            if (SecretVersionName.TryParse(request.Name, out SecretVersionName? parsed))
            {
                return parsed!;
            }

            throw new SecretVaultConfigurationException(
                "Vault:Secret:InvalidResourceName",
                $"'{request.Name}' is not a valid GCP Secret Manager resource name.");
        }

        if (string.IsNullOrWhiteSpace(_projectId))
        {
            throw new SecretVaultConfigurationException(
                "Vault:Secret:MissingProjectId",
                "Vault:GoogleCloud:ProjectId must be configured to resolve short secret names.");
        }

        return new SecretVersionName(_projectId, request.Name, version);
    }

    private static string? ExtractVersion(string? resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            return null;
        }

        int lastSlash = resourceName.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < resourceName.Length - 1
            ? resourceName[(lastSlash + 1)..]
            : null;
    }

    private static bool IsTransient(StatusCode code) =>
        code is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.ResourceExhausted
            or StatusCode.Internal;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "GCP Secret Manager denied access to secret '{SecretName}'.")]
    private static partial void LogAccessDenied(ILogger logger, string secretName);
}
