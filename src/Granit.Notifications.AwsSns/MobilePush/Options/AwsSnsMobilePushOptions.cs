using System.ComponentModel.DataAnnotations;

namespace Granit.Notifications.AwsSns.MobilePush.Options;

/// <summary>Configuration for AWS SNS mobile push sending.</summary>
public sealed class AwsSnsMobilePushOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:AwsSns:MobilePush";

    /// <summary>AWS region (e.g. "eu-west-1"). Required.</summary>
    [Required]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// SNS Platform Application ARN for the target platform (APNs or FCM).
    /// Required. Created in the AWS console or via CloudFormation.
    /// </summary>
    [Required]
    public string PlatformApplicationArn { get; set; } = string.Empty;

    /// <summary>Optional AWS access key. When null, default credential chain is used.</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>Optional AWS secret key. Required when <see cref="AccessKeyId"/> is set.</summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>API call timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
