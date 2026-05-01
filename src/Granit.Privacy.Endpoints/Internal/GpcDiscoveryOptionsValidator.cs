using Granit.Privacy.Endpoints.Discovery;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Endpoints.Internal;

/// <summary>
/// Validates <see cref="GpcDiscoveryOptions"/> at startup. Refuses
/// <c>Enabled = true</c> without a <see cref="GpcDiscoveryOptions.LastUpdate"/>
/// value — the GPC spec requires the field, and auto-defaulting would silently
/// move the legal anchor (see <see cref="GpcDiscoveryOptions.LastUpdate"/> remarks).
/// </summary>
internal sealed class GpcDiscoveryOptionsValidator : IValidateOptions<GpcDiscoveryOptions>
{
    public ValidateOptionsResult Validate(string? name, GpcDiscoveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.LastUpdate is null)
        {
            return ValidateOptionsResult.Fail(
                "GPC discovery is enabled but GpcDiscoveryOptions.LastUpdate is not set. "
                + "The GPC spec requires a stable lastUpdate date marking when the operator "
                + "committed to honoring GPC. Set it deliberately in configuration "
                + $"(section '{GpcDiscoveryOptions.SectionName}'); do not auto-default to "
                + "today/build date — this value is a legal anchor and must be deliberate.");
        }

        if (options.CacheMaxAgeSeconds < 0)
        {
            return ValidateOptionsResult.Fail(
                $"GpcDiscoveryOptions.CacheMaxAgeSeconds must be >= 0 (got {options.CacheMaxAgeSeconds}).");
        }

        return ValidateOptionsResult.Success;
    }
}
