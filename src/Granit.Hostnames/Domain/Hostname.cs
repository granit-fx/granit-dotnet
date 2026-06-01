using System.Text.RegularExpressions;
using Granit.Domain;

namespace Granit.Hostnames.Domain;

/// <summary>
/// A fully-qualified domain name (e.g. <c>www.acme.com</c>), normalised to lower case. Maximum 253
/// characters (RFC 1035). The single-value-object wrapper gives EF Core + JSON converters for free
/// via <c>ApplyGranitConventions</c> and <c>SingleValueObjectJsonConverterFactory</c>.
/// </summary>
public sealed partial class Hostname : SingleValueObject<string>
{
    private const int MaxLength = 253;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>Creates a validated, lower-cased <see cref="Hostname"/>.</summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 253 characters, or is not a valid FQDN.
    /// </exception>
    public static Hostname Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalised = value.Trim().ToLowerInvariant();
        if (normalised.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Hostname exceeds the maximum length of {MaxLength} characters.", nameof(value));
        }

        if (!FqdnPattern().IsMatch(normalised))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid fully-qualified domain name (e.g. www.example.com).", nameof(value));
        }

        return new Hostname { Value = normalised };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(Hostname host) => host.Value;

    /// <summary>Implicit conversion from <see cref="string"/> (validates + normalises).</summary>
    public static implicit operator Hostname(string value) => Create(value);

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*)+$",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FqdnPattern();
}
