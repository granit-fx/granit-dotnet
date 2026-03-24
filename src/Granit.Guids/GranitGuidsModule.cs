using Granit.Guids.Extensions;
using Granit.Modularity;
using Granit.Timing;

namespace Granit.Guids;

/// <summary>
/// Granit module for <see cref="IGuidGenerator"/>.
/// </summary>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitGuidsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitGuids();
}
