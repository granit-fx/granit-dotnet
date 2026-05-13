namespace Granit.Browsing.Pages;

/// <summary>
/// Scrubs sensitive headers, cookies, and credential-like values from a HAR JSON
/// document before it is exposed to a caller. Implementations MUST be idempotent —
/// running them twice MUST yield the same output.
/// </summary>
/// <remarks>
/// The framework registers <c>DefaultHarScrubber</c> by default. Override the registration
/// to plug in a domain-specific redaction policy (e.g. masking customer identifiers in
/// request bodies).
/// </remarks>
public interface IHarScrubber
{
    /// <summary>Returns <paramref name="harJson"/> with sensitive values redacted.</summary>
    string Scrub(string harJson);
}
