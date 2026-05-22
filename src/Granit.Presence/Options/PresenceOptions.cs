using Microsoft.Extensions.Options;

namespace Granit.Presence.Options;

/// <summary>
/// Configuration options for the Granit.Presence module.
/// Bound from the <c>"Presence"</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class PresenceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Presence";

    /// <summary>
    /// Maximum age of the last heartbeat before a user is considered offline. Default 90 s.
    /// </summary>
    public TimeSpan OfflineThreshold { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Maximum age of the last reported user activity before connectivity transitions
    /// from <see cref="Domain.PresenceConnectivity.Online"/> to
    /// <see cref="Domain.PresenceConnectivity.Away"/>. Default 3 min.
    /// </summary>
    public TimeSpan AwayThreshold { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Time-to-live applied to a user's heartbeat entry in the presence cache.
    /// Should be slightly larger than <see cref="OfflineThreshold"/> so a brief
    /// network blip does not flip the user to Offline. Default 120 s.
    /// </summary>
    public TimeSpan HeartbeatCacheTtl { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Maximum batch size accepted by the batch presence query endpoint. Default 200.
    /// </summary>
    public int MaxBatchSize { get; set; } = 200;

    /// <summary>
    /// Maximum duration accepted for <c>OverrideUntilUtc</c> relative to <c>now</c>.
    /// Defends against runaway overrides set with absurdly far-future expirations.
    /// Default 7 days.
    /// </summary>
    public TimeSpan MaxOverrideDuration { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Validates <see cref="PresenceOptions"/> at startup.
/// </summary>
internal sealed class PresenceOptionsValidator : IValidateOptions<PresenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PresenceOptions options)
    {
        List<string> errors = [];

        if (options.OfflineThreshold <= TimeSpan.Zero || options.OfflineThreshold > TimeSpan.FromMinutes(30))
        {
            errors.Add(
                $"{nameof(PresenceOptions.OfflineThreshold)} must be positive and not exceed 30 minutes (got {options.OfflineThreshold}).");
        }

        if (options.AwayThreshold <= TimeSpan.Zero || options.AwayThreshold > TimeSpan.FromHours(2))
        {
            errors.Add(
                $"{nameof(PresenceOptions.AwayThreshold)} must be positive and not exceed 2 hours (got {options.AwayThreshold}).");
        }

        if (options.HeartbeatCacheTtl < options.OfflineThreshold)
        {
            errors.Add(
                $"{nameof(PresenceOptions.HeartbeatCacheTtl)} ({options.HeartbeatCacheTtl}) must be >= {nameof(PresenceOptions.OfflineThreshold)} ({options.OfflineThreshold}).");
        }

        if (options.MaxBatchSize is < 1 or > 1000)
        {
            errors.Add(
                $"{nameof(PresenceOptions.MaxBatchSize)} must be between 1 and 1000 (got {options.MaxBatchSize}).");
        }

        if (options.MaxOverrideDuration <= TimeSpan.Zero || options.MaxOverrideDuration > TimeSpan.FromDays(30))
        {
            errors.Add(
                $"{nameof(PresenceOptions.MaxOverrideDuration)} must be positive and not exceed 30 days (got {options.MaxOverrideDuration}).");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
