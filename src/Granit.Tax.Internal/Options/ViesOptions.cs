namespace Granit.Tax.Internal.Options;

/// <summary>
/// Configuration for the EU VIES (VAT Information Exchange System) REST API.
/// </summary>
public sealed class ViesOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Tax:Internal:Vies";

    /// <summary>
    /// Base URL of the VIES REST API.
    /// Defaults to the official European Commission endpoint.
    /// </summary>
    public Uri BaseUrl { get; set; } = new("https://ec.europa.eu/taxation_customs/vies/rest-api/");
}
