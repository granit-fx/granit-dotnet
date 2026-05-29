using Microsoft.Extensions.Options;

namespace Granit.Auditing.Options;

/// <summary>
/// Validates <see cref="AuditingOptions"/> at startup to reject negative or zero
/// <see cref="TimeSpan"/> values for retention periods and cache TTLs.
/// </summary>
internal sealed class AuditingOptionsValidator
    : IValidateOptions<AuditingOptions>
{
    private static readonly TimeSpan MinimumRetention = TimeSpan.FromDays(1);

    public ValidateOptionsResult Validate(string? name, AuditingOptions options)
    {
        List<string> failures = [];

        ValidateRetention(failures, options.ConfigurationChangeRetention, nameof(AuditingOptions.ConfigurationChangeRetention));
        ValidateRetention(failures, options.DataMutationRetention, nameof(AuditingOptions.DataMutationRetention));
        ValidateRetention(failures, options.DataAccessRetention, nameof(AuditingOptions.DataAccessRetention));
        ValidateRetention(failures, options.AccessDeniedRetention, nameof(AuditingOptions.AccessDeniedRetention));

        ValidatePositiveTimeSpan(failures, options.CacheEntryTtl, nameof(AuditingOptions.CacheEntryTtl), TimeSpan.FromSeconds(1));
        ValidatePositiveTimeSpan(failures, options.CacheEntityQueryTtl, nameof(AuditingOptions.CacheEntityQueryTtl), TimeSpan.FromSeconds(1));

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRetention(List<string> failures, TimeSpan value, string propertyName)
    {
        if (value < MinimumRetention)
        {
            failures.Add(
                $"{propertyName} must be at least {MinimumRetention.TotalDays} day(s), got {value.TotalDays:F2} day(s).");
        }
    }

    private static void ValidatePositiveTimeSpan(List<string> failures, TimeSpan value, string propertyName, TimeSpan minimum)
    {
        if (value < minimum)
        {
            failures.Add(
                $"{propertyName} must be at least {minimum}, got {value}.");
        }
    }
}
