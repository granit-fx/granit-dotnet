using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.AwsSns.Sms.Options;

/// <summary>Configuration for AWS SNS SMS sending.</summary>
public sealed class AwsSnsSmsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:AwsSns:Sms";

    /// <summary>AWS region (e.g. "eu-west-1"). Required.</summary>
    [Required]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Default sender ID displayed on the recipient's device.
    /// Must be alphanumeric, 1-11 characters (not supported in all countries).
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// SNS message type: "Transactional" or "Promotional". Default: "Transactional".
    /// </summary>
    public string SmsType { get; set; } = "Transactional";

    /// <summary>Optional origination number in E.164 format (e.g. "+15551234567").</summary>
    public string? OriginationNumber { get; set; }

    /// <summary>Optional AWS access key. When null, default credential chain is used.</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>Optional AWS secret key. Required when <see cref="AccessKeyId"/> is set. Source from Vault, never from plaintext appsettings.</summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>API call timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
