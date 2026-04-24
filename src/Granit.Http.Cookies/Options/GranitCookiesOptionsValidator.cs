using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.Options;

/// <summary>
/// Validates <see cref="GranitCookiesOptions"/> at startup. Enforces RGPD
/// storage-limitation bounds on cookie retention (CNIL: 13 months max for
/// analytics/marketing cookies).
/// </summary>
internal sealed class GranitCookiesOptionsValidator : IValidateOptions<GranitCookiesOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitCookiesOptions options)
    {
        List<string>? failures = null;

        if (options.DefaultRetentionDays <= 0)
        {
            (failures ??= []).Add(
                $"{nameof(GranitCookiesOptions.DefaultRetentionDays)} must be > 0. " +
                $"Got {options.DefaultRetentionDays}.");
        }
        else if (options.DefaultRetentionDays > GranitCookiesOptions.MaxRetentionDays)
        {
            (failures ??= []).Add(
                $"{nameof(GranitCookiesOptions.DefaultRetentionDays)} must be <= " +
                $"{GranitCookiesOptions.MaxRetentionDays} (CNIL 13-month hard cap — RGPD Art. 5(1)(e) " +
                $"storage limitation). Got {options.DefaultRetentionDays}. For cookies that need a " +
                "longer lifetime, declare an explicit CookieDefinition.RetentionDays with an explicit " +
                "legal basis documented in your data-protection impact assessment.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
