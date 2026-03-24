using Granit.Modularity;
using Granit.Querying.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Querying;

/// <summary>
/// Granit module for declarative query building.
/// </summary>
/// <remarks>
/// Registers the core querying infrastructure: query definition descriptors
/// and null-object defaults for optional services (saved view store).
/// <para>
/// For the EF Core query engine, add <c>Granit.Querying.EntityFrameworkCore</c>.
/// For REST endpoints, add <c>Granit.Querying.Endpoints</c>.
/// </para>
/// </remarks>
public sealed class GranitQueryingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitQuerying();
}
