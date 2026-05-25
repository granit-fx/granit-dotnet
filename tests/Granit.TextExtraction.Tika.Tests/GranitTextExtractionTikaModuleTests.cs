using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tika.Tests;

public sealed class GranitTextExtractionTikaModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionTikaModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionTikaModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionTikaModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public void Module_does_not_auto_register_services()
    {
        // The module is a marker — the host must opt in via AddTikaSidecarExtractor.
        // ConfigureServices is the empty default.
        GranitTextExtractionTikaModule module = new();
        bool overridesConfigureServices = module.GetType()
            .GetMethod("ConfigureServices")!
            .DeclaringType != typeof(GranitModule);

        overridesConfigureServices.ShouldBeFalse(
            "GranitTextExtractionTikaModule must NOT override ConfigureServices — Tika " +
            "is opt-in and requires explicit AddTikaSidecarExtractor(...) by the host " +
            "(because the AllowedHosts allowlist has no safe default).");
    }
}
