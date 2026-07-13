using System.Diagnostics.CodeAnalysis;
using Azure.Communication.Sms;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Internal;

/// <summary>
/// Production wrapper around <see cref="SmsClient"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class AzureAcsSmsTransport(SmsClient client) : IAcsSmsTransport
{
    /// <inheritdoc />
    public async Task<SmsSendResult> SendAsync(
        string from,
        string to,
        string message,
        CancellationToken cancellationToken = default)
    {
        Azure.Response<SmsSendResult> response = await client
            .SendAsync(from, to, message, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Value;
    }
}
