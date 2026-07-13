using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Communication.Email;

namespace Granit.Notifications.AzureCommunicationServices.Email.Internal;

/// <summary>
/// Production wrapper around <see cref="EmailClient"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class AzureAcsEmailTransport(EmailClient client) : IAcsEmailTransport
{
    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(
        Azure.Communication.Email.EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        EmailSendOperation operation = await client
            .SendAsync(WaitUntil.Completed, message, cancellationToken)
            .ConfigureAwait(false);

        return operation.Value;
    }
}
