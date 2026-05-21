using System.ComponentModel.DataAnnotations;

namespace Granit.Identity.Federated.Cognito.Options;

/// <summary>
/// Configuration options for the AWS Cognito Admin API used by
/// <see cref="Internal.CognitoIdentityProvider"/>.
/// </summary>
/// <remarks>
/// Credentials should be loaded from Vault or IAM roles — never stored in plain text.
/// </remarks>
public sealed class CognitoAdminOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Federated:Cognito";

    /// <summary>AWS region (e.g. <c>eu-west-1</c>).</summary>
    [Required]
    public string Region { get; set; } = string.Empty;

    /// <summary>Cognito User Pool ID (e.g. <c>eu-west-1_XXXXXXXXX</c>).</summary>
    [Required]
    public string UserPoolId { get; set; } = string.Empty;

    /// <summary>
    /// App Client ID used for credential verification via <c>ADMIN_USER_PASSWORD_AUTH</c>.
    /// Required for <see cref="IIdentityCredentialVerifier.VerifyUserCredentialsAsync"/>.
    /// </summary>
    public string? AppClientId { get; set; }

    /// <summary>
    /// Optional AWS access key. When empty, IAM roles or environment credentials are used.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// Optional AWS secret key. Must be paired with <see cref="AccessKeyId"/>.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>HTTP request timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
