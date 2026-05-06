namespace Granit.Taxonomy.Options;

/// <summary>
/// Configuration options for the Granit.Taxonomy module.
/// </summary>
/// <remarks>
/// Phase T1 placeholder — options surface for host opt-in configuration.
/// Concrete settings (default scope, orphan-cleanup schedule, autocomplete result cap)
/// are added in later stories as the module matures.
/// </remarks>
public sealed class TaxonomyOptions
{
    /// <summary>Configuration section name in appsettings.</summary>
    public const string SectionName = "Taxonomy";
}
