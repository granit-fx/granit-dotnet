using Granit.Http.Cors.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cors.Internal;

/// <summary>
/// Validates <see cref="GranitCorsOptions"/> at startup.
/// Enforces ISO 27001-compliant CORS rules and rejects malformed origin values
/// that would silently break cross-origin requests in runtime.
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

        // Format-check each non-wildcard origin AFTER the benign trailing slash
        // is trimmed. Reject scheme/path/query/fragment malformations that
        // otherwise silently fail CORS at runtime (and tempt operators to
        // "just use '*'" to unblock the app).
        foreach (string? origin in options.AllowedOrigins)
        {
            if (origin is null || origin == "*")
            {
                continue;
            }

            string normalized = origin.TrimEnd('/');

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri)
                || uri.Scheme is not ("http" or "https")
                || uri.AbsolutePath is not ("" or "/")
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || uri.Port == 0)
            {
                errors.Add(
                    $"CORS origin '{origin}' is not a valid origin. " +
                    "Expected form: 'https://host[:port]' " +
                    "(scheme must be http/https; no path, query, or fragment). " +
                    "A trailing slash is accepted and normalized automatically.");
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
