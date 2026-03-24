using Granit.Modularity;
using Granit.Timing.Extensions;

namespace Granit.Timing;

/// <summary>
/// Granit module for IClock, ICurrentTimezoneProvider and TimeProvider.
/// No dependency on other Granit modules.
/// </summary>
public sealed class GranitTimingModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTiming();
}
