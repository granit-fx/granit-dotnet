namespace Granit.Webhooks.Exceptions;

/// <summary>
/// Thrown by <see cref="Handlers.SendWebhookHandler"/> when a retriable HTTP error occurs
/// (5xx, 429, or network timeout).
/// </summary>
/// <remarks>
/// <para>
/// This exception intentionally does NOT implement <see cref="Granit.Exceptions.IHasErrorCode"/>
/// because it is an internal retry signal for Wolverine, not a user-facing error.
/// It should never reach the HTTP boundary or <c>GranitExceptionHandler</c>.
/// </para>
/// <para>
/// Wolverine catches this exception and retries the <see cref="Messages.SendWebhookCommand"/>
/// according to the exponential backoff policy configured in <c>AddGranitWebhooks()</c>
/// (30 s → 2 min → 10 min → 30 min → 2 h → 12 h). After all retries are exhausted,
/// the command moves to the Dead-Letter Queue for manual investigation.
/// </para>
/// <para>
/// Non-retriable HTTP errors (4xx deterministic failures) are handled gracefully inside
/// the handler — they do not throw this exception and do not trigger retries.
/// </para>
/// </remarks>
public sealed class WebhookDeliveryException : Exception
{
    /// <summary>Initializes a new instance with the specified error message.</summary>
    public WebhookDeliveryException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the specified error message and inner exception.</summary>
    public WebhookDeliveryException(string message, Exception innerException)
        : base(message, innerException) { }
}
