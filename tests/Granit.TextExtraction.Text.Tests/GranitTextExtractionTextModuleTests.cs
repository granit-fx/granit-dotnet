using System.Diagnostics.Metrics;
using System.Text;
using Granit.Modularity;
using Granit.TextExtraction.Text.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Text.Tests;

public sealed class GranitTextExtractionTextModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitTextExtractionTextModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitTextExtractionTextModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTextExtractionModule()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitTextExtractionTextModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);

        attrs.SelectMany(a => a.DependedTypes).ShouldContain(typeof(GranitTextExtractionModule));
    }

    [Fact]
    public async Task Pipeline_routes_html_and_markdown_after_AddGranitTextExtractionText()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IMeterFactory, NoopMeterFactory>();
        services.AddSingleton(MEOptions.Create(new ExtractionOptions()));
        services.AddGranitTextExtractionText();

        await using ServiceProvider sp = services.BuildServiceProvider();
        ITextExtractionPipeline pipeline = sp.GetRequiredService<ITextExtractionPipeline>();

        await using MemoryStream html = new(Encoding.UTF8.GetBytes("<p>Hello</p>"));
        TextExtractionResult htmlResult =
            await pipeline.ExtractAsync(html, "text/html", TestContext.Current.CancellationToken);
        htmlResult.ExtractorName.ShouldBe(HtmlTextExtractor.ExtractorName);

        await using MemoryStream md = new(Encoding.UTF8.GetBytes("**Bold**"));
        TextExtractionResult mdResult =
            await pipeline.ExtractAsync(md, "text/markdown", TestContext.Current.CancellationToken);
        mdResult.ExtractorName.ShouldBe(MarkdownTextExtractor.ExtractorName);
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
