using System.Diagnostics;
using Amazon.SimpleNotificationService.Model;
using Granit.Diagnostics;
using Granit.Notifications.Sms.AwsSns.Diagnostics;
using Granit.Notifications.Sms.AwsSns.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Sms.AwsSns.Internal;

/// <summary>
/// <see cref="ISmsSender"/> implementation using AWS SNS.
/// Registered as Keyed Service with key "AwsSns".
/// </summary>
internal sealed partial class AwsSnsSmsSender(
    IOptionsMonitor<AwsSnsSmsOptions> options,
    ILogger<AwsSnsSmsSender> logger,
    IAwsSnsSmsTransport transport) : ISmsSender
{
    /// <inheritdoc />
    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        AwsSnsSmsOptions opts = options.CurrentValue;

        using Activity? activity = NotificationsSmsAwsSnsActivitySource.Source.StartActivity(
            NotificationsSmsAwsSnsActivitySource.Operations.SendSms);
        activity?.SetTag(NotificationsSmsAwsSnsActivitySource.Tags.Recipient, LogRedaction.HashPrefix(message.To));

        Dictionary<string, MessageAttributeValue> attributes = new()
        {
            ["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = opts.SmsType,
            },
        };

        string? senderId = message.SenderId ?? opts.SenderId;
        if (!string.IsNullOrEmpty(senderId))
        {
            attributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = senderId,
            };
        }

        if (!string.IsNullOrEmpty(opts.OriginationNumber))
        {
            attributes["AWS.MM.SMS.OriginationNumber"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = opts.OriginationNumber,
            };
        }

        PublishRequest request = new()
        {
            Message = message.Body,
            PhoneNumber = message.To,
            MessageAttributes = attributes,
        };

        PublishResponse response = await transport
            .PublishAsync(request, cancellationToken)
            .ConfigureAwait(false);

        LogSmsSent(LogRedaction.Phone(message.To), response.MessageId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SNS SMS sent to {RedactedRecipient}, messageId={MessageId}")]
    private partial void LogSmsSent(string redactedRecipient, string messageId);
}
