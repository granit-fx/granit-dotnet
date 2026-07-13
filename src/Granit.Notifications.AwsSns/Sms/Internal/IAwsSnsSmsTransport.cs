using Amazon.SimpleNotificationService.Model;

namespace Granit.Notifications.AwsSns.Sms.Internal;

/// <summary>
/// Abstraction over <see cref="Amazon.SimpleNotificationService.IAmazonSimpleNotificationService"/>
/// for testability.
/// </summary>
internal interface IAwsSnsSmsTransport
{
    /// <summary>Publishes an SMS via SNS.</summary>
    Task<PublishResponse> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default);
}
