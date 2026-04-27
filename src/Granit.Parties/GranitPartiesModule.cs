using Granit.Modularity;
using Granit.Parties.Extensions;

namespace Granit.Parties;

/// <summary>Granit module for the central <c>Party</c> aggregate.</summary>
public sealed class GranitPartiesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitParties();
}
