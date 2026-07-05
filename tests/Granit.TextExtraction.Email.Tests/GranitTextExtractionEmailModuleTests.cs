using System.Diagnostics.Metrics;
using Granit.Modularity;
using Granit.TextExtraction.Email.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Email.Tests;

public sealed class GranitTextExtractionEmailModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionEmailModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionEmailModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionEmailModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public async Task Pipeline_routes_message_rfc822_after_AddGranitTextExtractionEmail()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IMeterFactory, NoopMeterFactory>();
        services.AddSingleton(MEOptions.Create(new ExtractionOptions()));
        services.AddLogging();
        services.AddGranitTextExtractionEmail();

        await using ServiceProvider sp = services.BuildServiceProvider();
        ITextExtractionPipeline pipeline = sp.GetRequiredService<ITextExtractionPipeline>();

        byte[] eml = EmlFixtures.PlainTextOnly(subject: "DI smoke", body: "DI smoke body");
        await using MemoryStream stream = new(eml);

        TextExtractionResult result =
            await pipeline.ExtractAsync(stream, "message/rfc822", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe(EmailTextExtractor.ExtractorName);
        result.Content.ShouldContain("Subject: DI smoke");
        result.Content.ShouldContain("DI smoke body");
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
