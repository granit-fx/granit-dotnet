using Granit.Parties.Privacy.DataExport;
using Granit.Privacy;

namespace Granit.Parties.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the parties privacy provider.
/// </summary>
public static class PrivacyBuilderPartiesExtensions
{
    /// <summary>
    /// Registers <see cref="PartiesPrivacyDataProvider"/> and adds <c>"parties"</c>
    /// to the scatter-gather registry.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. Requires
    /// <see cref="GranitPartiesPrivacyModule"/> to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitPartiesPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDataProvider<PartiesPrivacyDataProvider>();
    }
}
