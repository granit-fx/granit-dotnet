using System.Globalization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Scheduling.Endpoints.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Scheduling.Endpoints.Tests;

/// <summary>
/// Canary integration test that proves the explicit
/// <c>services.AddLocalizationResource&lt;SchedulingEndpointsLocalizationResource&gt;()</c>
/// wiring in <see cref="GranitSchedulingEndpointsModule"/> makes the workspace label
/// resolve in both <c>fr</c> and <c>en</c> without relying on auto-discovery.
///
/// Regression guard for the dev.2691 sidebar regression where the literal "Item" rendered
/// because <c>SchedulingEndpoints:Workspace.Item</c> failed to resolve.
/// </summary>
public sealed class SchedulingEndpointsLocalizationCanaryTests : IDisposable
{
    private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUICulture;

    [Theory]
    [InlineData("fr", "Actions planifiées")]
    [InlineData("en", "Scheduled actions")]
    public void Workspace_label_resolves_when_module_only_registers_explicitly(
        string culture, string expected)
    {
        // Arrange — emulate a host that loads only the endpoint module, with auto-discovery OFF
        // (so a resolution success proves explicit registration alone is sufficient).
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddOptions();
        builder.Services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));
        builder.Services.AddGranitLocalization(options => options.EnableAutoDiscovery = false);

        ServiceConfigurationContext context = new(
            builder.Services, builder.Configuration, builder);

        GranitSchedulingEndpointsModule module = new();
        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        IStringLocalizer<SchedulingEndpointsLocalizationResource> localizer =
            sp.GetRequiredService<IStringLocalizer<SchedulingEndpointsLocalizationResource>>();
        LocalizedString resolved = localizer["SchedulingEndpoints:Workspace.Item"];

        // Assert
        resolved.ResourceNotFound.ShouldBeFalse(
            $"SchedulingEndpoints:Workspace.Item must resolve in '{culture}' without auto-discovery.");
        resolved.Value.ShouldBe(expected);
    }
}
