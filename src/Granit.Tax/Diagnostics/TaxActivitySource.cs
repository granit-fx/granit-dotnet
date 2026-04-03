using System.Diagnostics;

namespace Granit.Tax.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Tax distributed tracing.
/// </summary>
internal static class TaxActivitySource
{
    internal const string Name = "Granit.Tax";

    internal static readonly ActivitySource Source = new(Name);

    internal const string Calculate = "tax.calculate";
    internal const string ValidateId = "tax.validate_id";
    internal const string ViesLookup = "tax.vies_lookup";
}
