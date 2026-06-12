using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.MaxMind.Options;

/// <summary>
/// Validates <see cref="MaxMindIpGeolocationOptions"/> at startup (fail-fast on a missing database file).
/// </summary>
internal sealed class MaxMindIpGeolocationOptionsValidator : IValidateOptions<MaxMindIpGeolocationOptions>
{
    public ValidateOptionsResult Validate(string? name, MaxMindIpGeolocationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DatabasePath)} must be non-empty. " +
                "Point it to a MaxMind/DB-IP .mmdb file (e.g. /var/lib/geoip/GeoLite2-City.mmdb).");
        }

        if (!File.Exists(options.DatabasePath))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DatabasePath)} '{options.DatabasePath}' does not exist. " +
                "Provision the .mmdb database before startup.");
        }

        if (string.IsNullOrWhiteSpace(options.ProviderName))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.ProviderName)} must be non-empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
