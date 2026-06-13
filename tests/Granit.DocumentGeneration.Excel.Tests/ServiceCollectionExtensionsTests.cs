using Granit.DocumentGeneration.Excel.Extensions;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Excel.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // AddGranitDocumentGenerationExcel
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitDocumentGenerationExcel_Registers_ITemplateEngine_Singleton()
    {
        ServiceCollection services = new();
        services.AddGranitDocumentGenerationExcel();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitDocumentGenerationExcel_CalledTwice_RegistersEngineOnce()
    {
        // TryAddEnumerable registration: additive across DIFFERENT engines (coexists with
        // Scriban, etc.) but deduped on repeat self-registration.
        ServiceCollection services = new();
        services.AddGranitDocumentGenerationExcel();
        services.AddGranitDocumentGenerationExcel();

        services.Count(d => d.ServiceType == typeof(ITemplateEngine))
            .ShouldBe(1, "TryAddEnumerable must dedupe the Excel engine on repeat registration");
    }
}
