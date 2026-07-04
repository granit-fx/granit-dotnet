using System.Collections.Frozen;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Options;

/// <summary>
/// Validates <see cref="GranitSecurityHeadersOptions"/> at startup to catch
/// misconfigurations that would silently degrade security header protection.
/// </summary>
internal sealed class GranitSecurityHeadersOptionsValidator
    : IValidateOptions<GranitSecurityHeadersOptions>
{
    private static readonly FrozenSet<string> s_validXFrameOptions =
        new[] { "DENY", "SAMEORIGIN" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_validReferrerPolicies =
        new[]
        {
            "no-referrer", "no-referrer-when-downgrade", "origin",
            "origin-when-cross-origin", "same-origin", "strict-origin",
            "strict-origin-when-cross-origin", "unsafe-url",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_validCrossOriginOpenerPolicies =
        new[] { "same-origin", "same-origin-allow-popups", "unsafe-none" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_validCrossOriginEmbedderPolicies =
        new[] { "require-corp", "credentialless", "unsafe-none" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_validCrossOriginResourcePolicies =
        new[] { "same-origin", "same-site", "cross-origin" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public ValidateOptionsResult Validate(string? name, GranitSecurityHeadersOptions options)
    {
        List<string>? failures = null;

        if (options.XFrameOptions is { } xfo && !s_validXFrameOptions.Contains(xfo))
        {
            (failures ??= []).Add(
                $"XFrameOptions '{xfo}' is not valid. Allowed: DENY, SAMEORIGIN, or null to disable.");
        }

        if (options.ReferrerPolicy is { Length: > 0 } rp && !s_validReferrerPolicies.Contains(rp))
        {
            (failures ??= []).Add(
                $"ReferrerPolicy '{rp}' is not valid. Allowed: {string.Join(", ", s_validReferrerPolicies.Order())}.");
        }

        if (options.CrossOriginOpenerPolicy is { Length: > 0 } coop
            && !s_validCrossOriginOpenerPolicies.Contains(coop))
        {
            (failures ??= []).Add(
                $"CrossOriginOpenerPolicy '{coop}' is not valid. Allowed: {string.Join(", ", s_validCrossOriginOpenerPolicies.Order())}.");
        }

        if (options.CrossOriginEmbedderPolicy is { } coep
            && !s_validCrossOriginEmbedderPolicies.Contains(coep))
        {
            (failures ??= []).Add(
                $"CrossOriginEmbedderPolicy '{coep}' is not valid. Allowed: {string.Join(", ", s_validCrossOriginEmbedderPolicies.Order())}.");
        }

        if (options.CrossOriginResourcePolicy is { Length: > 0 } corp
            && !s_validCrossOriginResourcePolicies.Contains(corp))
        {
            (failures ??= []).Add(
                $"CrossOriginResourcePolicy '{corp}' is not valid. Allowed: {string.Join(", ", s_validCrossOriginResourcePolicies.Order())}.");
        }

        // When HSTS is enabled, enforce the OWASP / RFC 6797 §12 minimum of
        // 6 months. A value of 0 emits `max-age=0`, which instructs browsers
        // to FORGET any previously cached HSTS policy — a silent downgrade
        // that a misconfigured appsettings.json should not trigger. Callers
        // that want HSTS off must set EnableHsts = false explicitly.
        const int MinimumHstsMaxAgeSeconds = 15_552_000; // 6 months
        if (options.EnableHsts && options.HstsMaxAgeSeconds < MinimumHstsMaxAgeSeconds)
        {
            (failures ??= []).Add(
                $"HstsMaxAgeSeconds must be >= {MinimumHstsMaxAgeSeconds} (6 months, OWASP minimum) " +
                "when EnableHsts=true. Set EnableHsts=false to disable HSTS entirely. " +
                $"Got {options.HstsMaxAgeSeconds}.");
        }
        else if (!options.EnableHsts && options.HstsMaxAgeSeconds < 0)
        {
            (failures ??= []).Add(
                $"HstsMaxAgeSeconds must be >= 0, got {options.HstsMaxAgeSeconds}.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
