using Azure.Communication.Sms;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Internal;

/// <summary>
/// Thin abstraction over <see cref="SmsClient"/>
/// to allow unit testing without a real ACS endpoint.
/// </summary>
internal interface IAcsSmsTransport
{
    /// <summary>Sends an SMS using the ACS SMS API.</summary>
    Task<SmsSendResult> SendAsync(
        string from,
        string to,
        string message,
        CancellationToken cancellationToken = default);
}
