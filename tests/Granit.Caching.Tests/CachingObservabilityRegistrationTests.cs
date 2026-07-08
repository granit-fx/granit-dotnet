using Granit.Caching.Extensions;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Caching.Tests;

/// <summary>
/// Guards the OTel wiring of the caching module: FusionCache emits its signals under
/// the ZiggyCreatures namespace, which the "Granit.*" wildcard never subscribes —
/// these registrations are the only reason cache metrics/traces are exported at all.
/// </summary>
public sealed class CachingObservabilityRegistrationTests
{
    [Fact]
    public void AddGranitCaching_RegistersFusionCacheActivitySources()
    {
        new ServiceCollection().AddGranitCaching();

        IReadOnlyCollection<string> sources = GranitActivitySourceRegistry.GetRegisteredSources();
        sources.ShouldContain(FusionCacheDiagnostics.ActivitySourceName);
        sources.ShouldContain(FusionCacheDiagnostics.ActivitySourceNameDistributedLevel);
    }

    [Fact]
    public void AddGranitCaching_RegistersFusionCacheMeter()
    {
        new ServiceCollection().AddGranitCaching();

        GranitMeterRegistry.GetRegisteredMeters().ShouldContain(FusionCacheDiagnostics.MeterName);
    }
}
