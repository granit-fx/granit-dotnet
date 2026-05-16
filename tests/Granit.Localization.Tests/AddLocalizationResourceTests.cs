using System.Globalization;
using Granit.Localization.Extensions;
using Granit.Localization.Internal;
using Granit.Localization.Options;
using Granit.Localization.Tests.TestResources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

/// <summary>
/// Tests for <c>LocalizationServiceCollectionExtensions.AddLocalizationResource&lt;T&gt;</c>
/// and the diagnostic-warning path in <see cref="JsonStringLocalizerFactory"/> when a resource
/// type is requested without explicit registration.
/// </summary>
public sealed class AddLocalizationResourceTests : IDisposable
{
    private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUICulture;

    [Fact]
    public void AddLocalizationResource_RegistersResourceAndJsonSource()
    {
        // Arrange — emulate the prefix convention this helper expects:
        // {AssemblyName}.Localization.{ResourceName}. Use a test resource whose embedded JSON
        // already follows this convention by aliasing the prefix via a manual entry, then verify
        // that AddLocalizationResource registers the type into the options.
        ServiceCollection services = new();
        services.AddOptions();

        // Act
        services.AddLocalizationResource<RegisteredEndpointsLocalizationResource>();

        using ServiceProvider sp = services.BuildServiceProvider();
        GranitLocalizationOptions options =
            sp.GetRequiredService<IOptions<GranitLocalizationOptions>>().Value;

        // Assert
        options.Resources.TryGetValue(typeof(RegisteredEndpointsLocalizationResource), out LocalizationResourceInfo? info)
            .ShouldBeTrue();
        info.ShouldNotBeNull();
        info!.ResourceType.ShouldBe(typeof(RegisteredEndpointsLocalizationResource));
    }

    [Fact]
    public void AddLocalizationResource_TypeWithoutAttribute_Throws()
    {
        ServiceCollection services = new();
        services.AddOptions();

        // Act + Assert — call must throw; Configure delegates run on first Value access
        // but the AddLocalizationResource helper validates eagerly via reflection on T.
        Should.Throw<InvalidOperationException>(() =>
            services.AddLocalizationResource<UndecoratedResource>());
    }

    [Fact]
    public void Factory_ResolvingUnregisteredResource_LogsWarningWithActionableMessage()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddOptions();
        ServiceProvider sp = services.BuildServiceProvider();
        IOptions<GranitLocalizationOptions> opts =
            sp.GetRequiredService<IOptions<GranitLocalizationOptions>>();

        ILogger<JsonStringLocalizerFactory> logger =
            Substitute.For<ILogger<JsonStringLocalizerFactory>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);

        JsonStringLocalizerFactory factory = new(opts, overrideStore: null, logger);

        // Act — UnregisteredResource has [LocalizationResourceName] but isn't in options
        IStringLocalizer localizer = factory.Create(typeof(UnregisteredResource));

        // Assert: fallback still works (key suffix)
        localizer["Foo:Bar"].ResourceNotFound.ShouldBeTrue();

        // Assert: a warning was logged mentioning the type, resource name, owning assembly,
        // and the AddLocalizationResource<> snippet.
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString()!.Contains(typeof(UnregisteredResource).FullName!)
                && state.ToString()!.Contains("Unregistered")
                && state.ToString()!.Contains(typeof(UnregisteredResource).Assembly.GetName().Name!)
                && state.ToString()!.Contains("AddLocalizationResource")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Factory_ResolvingRegisteredResource_DoesNotLogWarning()
    {
        // Arrange
        ServiceCollection services = new();
        services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<TestResource>("fr")
                .AddJson(
                    typeof(JsonStringLocalizerFactoryTests).Assembly,
                    "Granit.Localization.Tests.TestResources.Localization.Test");
        });
        ServiceProvider sp = services.BuildServiceProvider();
        IOptions<GranitLocalizationOptions> opts =
            sp.GetRequiredService<IOptions<GranitLocalizationOptions>>();

        ILogger<JsonStringLocalizerFactory> logger =
            Substitute.For<ILogger<JsonStringLocalizerFactory>>();

        JsonStringLocalizerFactory factory = new(opts, overrideStore: null, logger);

        // Act
        factory.Create(typeof(TestResource));

        // Assert
        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void AutoDiscovery_DoesNotOverwriteExplicitRegistration()
    {
        // Regression guard: when EnableAutoDiscovery=true, an existing explicit entry must
        // be preserved (the TryGetValue guard in LocalizationAutoDiscovery is intact).
        // Arrange
        ServiceCollection services = new();
        services.Configure<GranitLocalizationOptions>(options =>
        {
            options.EnableAutoDiscovery = true;
            // Explicitly register TestResource pointing at the real Test.* embedded JSON
            options.Resources
                .Add<TestResource>("fr")
                .AddJson(
                    typeof(JsonStringLocalizerFactoryTests).Assembly,
                    "Granit.Localization.Tests.TestResources.Localization.Test");
        });
        ServiceProvider sp = services.BuildServiceProvider();
        IOptions<GranitLocalizationOptions> opts =
            sp.GetRequiredService<IOptions<GranitLocalizationOptions>>();

        // Act — instantiate factory, which triggers auto-discovery
        _ = new JsonStringLocalizerFactory(opts);
        GranitLocalizationOptions afterDiscovery = opts.Value;

        // Assert — explicit entry preserved (Test JSON resolves), not overwritten
        afterDiscovery.Resources.TryGetValue(typeof(TestResource), out LocalizationResourceInfo? info).ShouldBeTrue();
        info!.JsonSources.Count.ShouldBeGreaterThan(0);
    }
}

// Test marker: lives in this assembly so the convention prefix
// "Granit.Localization.Tests.Localization.RegisteredEndpoints" would be expected.
// The helper builds it from the assembly + [LocalizationResourceName]. We only assert
// registration here, not key resolution (no JSON is embedded for this marker).
[LocalizationResourceName("RegisteredEndpoints")]
public sealed class RegisteredEndpointsLocalizationResource;

// Test marker missing the [LocalizationResourceName] attribute — used to assert the helper's
// validation path.
public sealed class UndecoratedResource;
