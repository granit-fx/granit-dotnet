using Microsoft.Extensions.Options;

namespace Granit.Webhooks.Options;

/// <summary>
/// Configuration options for the Granit.Webhooks module.
/// </summary>
/// <remarks>
/// Bound from the <c>"Webhooks"</c> section of <c>appsettings.json</c>.
/// </remarks>
public sealed class WebhooksOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Webhooks";

    /// <summary>
    /// HTTP request timeout for webhook delivery, in seconds.
    /// Must be between 5 and 120. Default: 10.
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of <see cref="Messages.SendWebhookCommand"/> processed in parallel
    /// on the <c>webhook-delivery</c> local queue.
    /// Must be between 1 and 100. Default: 20.
    /// </summary>
    public int MaxParallelDeliveries { get; set; } = 20;

    /// <summary>
    /// When <c>true</c>, the serialized JSON body of each delivery attempt is persisted
    /// alongside the <see cref="Domain.WebhookDeliveryAttempt"/> record, enabling manual redelivery.
    /// Default: <c>false</c> (only the SHA-256 hash is stored — ISO 27001-safe minimum).
    /// </summary>
    /// <remarks>
    /// Enabling this option stores health data in clear text in the audit trail.
    /// Ensure encryption at rest is configured on the database and that your DPO has validated
    /// this setting against GDPR data-minimization requirements.
    /// </remarks>
    public bool StorePayload { get; set; }

    /// <summary>
    /// Grace period during which a previously-active <see cref="Domain.WebhookSigningKey"/>
    /// remains accepted in verification after a rotation. Default: 24 hours.
    /// </summary>
    /// <remarks>
    /// Override per-rotation via the <c>retiredKeyGracePeriod</c> argument on
    /// <see cref="Abstractions.IWebhookSigningKeyWriter.RotateSigningKeyAsync"/>.
    /// Must be greater than zero and not exceed 30 days.
    /// </remarks>
    public TimeSpan RetiredKeyGracePeriod { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Validates <see cref="WebhooksOptions"/> at startup.
/// </summary>
internal sealed class WebhooksOptionsValidator : IValidateOptions<WebhooksOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhooksOptions options)
    {
        List<string> errors = [];

        if (options.HttpTimeoutSeconds < 5 || options.HttpTimeoutSeconds > 120)
        {
            errors.Add($"{nameof(WebhooksOptions.HttpTimeoutSeconds)} must be between 5 and 120 (got {options.HttpTimeoutSeconds}).");
        }

        if (options.MaxParallelDeliveries < 1 || options.MaxParallelDeliveries > 100)
        {
            errors.Add($"{nameof(WebhooksOptions.MaxParallelDeliveries)} must be between 1 and 100 (got {options.MaxParallelDeliveries}).");
        }

        if (options.RetiredKeyGracePeriod <= TimeSpan.Zero || options.RetiredKeyGracePeriod > TimeSpan.FromDays(30))
        {
            errors.Add(
                $"{nameof(WebhooksOptions.RetiredKeyGracePeriod)} must be positive and not exceed 30 days (got {options.RetiredKeyGracePeriod}).");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
