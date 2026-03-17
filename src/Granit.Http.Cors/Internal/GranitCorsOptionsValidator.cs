using Granit.Http.Cors.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cors.Internal;

/// <summary>
/// Validates <see cref="GranitCorsOptions"/> at startup.
/// Enforces ISO 27001-compliant CORS rules.
/// </summary>
internal sealed class GranitCorsOptionsValidator(
    IHostEnvironment environment) : IValidateOptions<GranitCorsOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GranitCorsOptions options)
    {
        List<string> errors = [];

        if (options.AllowedOrigins.Length == 0)
        {
            errors.Add(
                $"{nameof(GranitCorsOptions.AllowedOrigins)} must contain at least one origin.");
        }

        bool hasWildcard = options.AllowedOrigins.Contains("*");

        if (hasWildcard && !environment.IsDevelopment())
        {
            errors.Add(
                $"{nameof(GranitCorsOptions.AllowedOrigins)} must not contain wildcard ('*') " +
                "in non-development environments (ISO 27001 compliance).");
        }

        if (hasWildcard && options.AllowCredentials)
        {
            errors.Add(
                $"{nameof(GranitCorsOptions.AllowCredentials)} cannot be true when " +
                $"{nameof(GranitCorsOptions.AllowedOrigins)} contains wildcard ('*'). " +
                "This violates the CORS specification.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
