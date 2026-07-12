using Granit.Auditing.Domain;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Options;

/// <summary>
/// Validates <see cref="AuditingOptions"/> at startup: every audit category's effective
/// retention (configured or default) must satisfy the regulatory floor
/// (<see cref="AuditingOptions.MinimumRetention"/>), and cache TTLs must be positive.
/// </summary>
/// <remarks>
/// The floor applies to the <em>effective</em> retention of every <see cref="AuditCategory"/>
/// value — including categories absent from <see cref="AuditingOptions.Retention"/> — so a
/// misconfigured or forgotten category can never silently purge its trail.
/// </remarks>
internal sealed class AuditingOptionsValidator
    : IValidateOptions<AuditingOptions>
{
    private static readonly TimeSpan AbsoluteMinimumRetention = TimeSpan.FromDays(1);

    public ValidateOptionsResult Validate(string? name, AuditingOptions options)
    {
        List<string> failures = [];

        if (options.MinimumRetention < AbsoluteMinimumRetention)
        {
            failures.Add(
                $"{nameof(AuditingOptions.MinimumRetention)} must be at least {AbsoluteMinimumRetention.TotalDays} day(s), got {options.MinimumRetention.TotalDays:F2} day(s).");
        }

        foreach (AuditCategory category in Enum.GetValues<AuditCategory>())
        {
            TimeSpan retention = options.GetRetention(category);
            if (retention < options.MinimumRetention)
            {
                failures.Add(
                    $"Retention for {category} must be at least {options.MinimumRetention.TotalDays:F0} day(s) ({nameof(AuditingOptions.MinimumRetention)}), got {retention.TotalDays:F2} day(s). Lower {nameof(AuditingOptions.MinimumRetention)} explicitly to accept a documented compliance deviation.");
            }
        }

        ValidatePositiveTimeSpan(failures, options.CacheEntryTtl, nameof(AuditingOptions.CacheEntryTtl), TimeSpan.FromSeconds(1));
        ValidatePositiveTimeSpan(failures, options.CacheEntityQueryTtl, nameof(AuditingOptions.CacheEntityQueryTtl), TimeSpan.FromSeconds(1));

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
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
