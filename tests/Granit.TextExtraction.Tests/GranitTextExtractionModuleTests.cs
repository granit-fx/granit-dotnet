using Granit.Diagnostics;
using Granit.Modularity;
using Granit.TextExtraction.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tests;

public sealed class GranitTextExtractionModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_declares_no_DependsOn()
    {
        // TextExtraction is a horizontal framework module with zero declared dependencies
        // (Granit is the implicit base). Adding a [DependsOn] later would couple all
        // consumers to that dependency tree, so the absence is part of the contract.
        object[] attrs = typeof(GranitTextExtractionModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);
        attrs.ShouldBeEmpty();
    }

    [Fact]
    public void ActivitySource_name_is_registered_globally()
    {
        // Module's ConfigureServices is what wires the registration. Until then, the static
        // registry may or may not contain the source. Trigger registration via the extension.
        ServiceCollectionRegistration.Register();

        GranitActivitySourceRegistry.GetRegisteredSources().ShouldContain(TextExtractionActivitySource.Name);
    }

    private static class ServiceCollectionRegistration
    {
        public static void Register() =>
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(
                new Microsoft.Extensions.DependencyInjection.ServiceCollection(),
                _ => "anchor")
                .AddGranitTextExtractionInternal();
    }
}

file static class ModuleRegistrationHook
{
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddGranitTextExtractionInternal(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        => Granit.TextExtraction.Extensions.ServiceCollectionExtensions.AddGranitTextExtraction(services);
}
