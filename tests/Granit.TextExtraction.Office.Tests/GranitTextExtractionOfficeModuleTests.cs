using System.Diagnostics.Metrics;
using Granit.Modularity;
using Granit.TextExtraction.Office.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Office.Tests;

public sealed class GranitTextExtractionOfficeModuleTests
{
    private const string Docx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionOfficeModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionOfficeModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionOfficeModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public async Task Pipeline_routes_office_mimes_after_AddGranitTextExtractionOffice()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IMeterFactory, NoopMeterFactory>();
        services.AddSingleton(MEOptions.Create(new ExtractionOptions()));
        services.AddLogging();
        services.AddGranitTextExtractionOffice();

        await using ServiceProvider sp = services.BuildServiceProvider();
        ITextExtractionPipeline pipeline = sp.GetRequiredService<ITextExtractionPipeline>();

        byte[] docx = OfficeFixtures.Docx("DI smoke");
        await using MemoryStream stream = new(docx);

        TextExtractionResult result =
            await pipeline.ExtractAsync(stream, Docx, TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe(WordTextExtractor.ExtractorName);
        result.Content.ShouldContain("DI smoke");
    }

    private sealed class NoopMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }
}
