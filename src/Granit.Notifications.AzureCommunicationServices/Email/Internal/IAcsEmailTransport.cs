using Azure.Communication.Email;

namespace Granit.Notifications.AzureCommunicationServices.Email.Internal;

/// <summary>
/// Thin abstraction over <see cref="EmailClient"/>
/// to allow unit testing without a real ACS endpoint.
/// </summary>
internal interface IAcsEmailTransport
{
    /// <summary>Sends an email using the Azure Communication Services API.</summary>
    Task<EmailSendResult> SendAsync(
        Azure.Communication.Email.EmailMessage message,
        CancellationToken cancellationToken = default);
}
