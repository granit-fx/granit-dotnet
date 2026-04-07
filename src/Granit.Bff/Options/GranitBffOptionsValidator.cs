using Microsoft.Extensions.Options;

namespace Granit.Bff.Options;

/// <summary>
/// Validates <see cref="GranitBffOptions"/> at startup to catch
/// misconfigurations that would cause cryptic runtime failures.
/// </summary>
internal sealed class GranitBffOptionsValidator
    : IValidateOptions<GranitBffOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitBffOptions options)
    {
        List<string> failures = [];

        if (options.Authority is null)
        {
            failures.Add(
                "Authority must not be null — set the OIDC authority URL in the 'Bff' configuration section.");
        }

        if (!string.IsNullOrEmpty(options.CsrfHmacKey))
        {
            byte[]? decoded = null;
            try
            {
                decoded = Convert.FromBase64String(options.CsrfHmacKey);
            }
            catch (FormatException)
            {
                failures.Add(
                    "CsrfHmacKey is not valid base64. Provide a 32-byte key encoded as base64 (44 characters).");
            }

            if (decoded is not null && decoded.Length != 32)
            {
                failures.Add(
                    $"CsrfHmacKey must decode to exactly 32 bytes (256 bits), got {decoded.Length} bytes.");
            }
        }

        for (int i = 0; i < options.Frontends.Count; i++)
        {
            BffFrontendOptions frontend = options.Frontends[i];

            if (!string.IsNullOrEmpty(frontend.ClientUrl))
            {
                if (!Uri.TryCreate(frontend.ClientUrl, UriKind.Absolute, out Uri? uri)
                    || (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    failures.Add(
                        $"Frontends[{i}].ClientUrl '{frontend.ClientUrl}' must be an absolute HTTP(S) URL.");
                }

                if (frontend.ClientUrl.EndsWith('/'))
                {
                    failures.Add(
                        $"Frontends[{i}].ClientUrl must not end with a trailing slash.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
