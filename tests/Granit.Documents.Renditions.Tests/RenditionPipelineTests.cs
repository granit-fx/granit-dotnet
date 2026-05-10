using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Exceptions;
using Granit.Documents.Renditions.Options;
using Granit.Documents.Renditions.Pipeline;
using Granit.Documents.Renditions.Providers;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Tests;

public sealed class RenditionPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_should_run_a_single_provider_for_a_direct_match()
    {
        FakeProvider imaging = new("imaging", input: "image/", output: "image/webp");
        RenditionPipeline pipeline = BuildPipeline([imaging]);

        RenditionResult result = await pipeline.ExecuteAsync(
            new MemoryStream([1, 2, 3]),
            "image/png",
            new RenditionTarget(RenditionType.Thumbnail, "image/webp"),
            TestContext.Current.CancellationToken);

        result.ContentType.ShouldBe("image/webp");
        imaging.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_should_chain_providers_office_to_pdf_to_image()
    {
        FakeProvider office = new("office", input: "application/vnd.openxmlformats-officedocument.wordprocessingml.document", output: "application/pdf");
        FakeProvider pdf = new("pdf", input: "application/pdf", output: "image/png");
        RenditionPipeline pipeline = BuildPipeline([office, pdf]);

        RenditionResult result = await pipeline.ExecuteAsync(
            new MemoryStream([1]),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            new RenditionTarget(RenditionType.Thumbnail, "image/png"),
            TestContext.Current.CancellationToken);

        result.ContentType.ShouldBe("image/png");
        office.InvocationCount.ShouldBe(1);
        pdf.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_should_chain_three_hops_for_office_to_webp()
    {
        FakeProvider office = new("office", input: "application/vnd.openxmlformats-officedocument.wordprocessingml.document", output: "application/pdf");
        FakeProvider pdf = new("pdf", input: "application/pdf", output: "image/png");
        FakeProvider imaging = new("imaging", input: "image/", output: "image/webp");
        RenditionPipeline pipeline = BuildPipeline([office, pdf, imaging]);

        RenditionResult result = await pipeline.ExecuteAsync(
            new MemoryStream([1]),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            new RenditionTarget(RenditionType.Thumbnail, "image/webp"),
            TestContext.Current.CancellationToken);

        result.ContentType.ShouldBe("image/webp");
        office.InvocationCount.ShouldBe(1);
        pdf.InvocationCount.ShouldBe(1);
        imaging.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_should_throw_when_no_chain_bridges_the_request()
    {
        FakeProvider imaging = new("imaging", input: "image/", output: "image/webp");
        RenditionPipeline pipeline = BuildPipeline([imaging]);

        await Should.ThrowAsync<RenditionPipelineException>(async () => await pipeline.ExecuteAsync(
            new MemoryStream([1]),
            "application/pdf",
            new RenditionTarget(RenditionType.Thumbnail, "image/webp"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CanBridge_should_return_false_when_no_chain_exists()
    {
        RenditionPipeline pipeline = BuildPipeline([new FakeProvider("imaging", input: "image/", output: "image/png")]);
        pipeline.CanBridge("application/pdf", "image/png").ShouldBeFalse();
    }

    [Fact]
    public void CanBridge_should_return_true_for_a_direct_match()
    {
        RenditionPipeline pipeline = BuildPipeline([new FakeProvider("imaging", input: "image/", output: "image/png")]);
        pipeline.CanBridge("image/jpeg", "image/png").ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_should_wrap_provider_exceptions()
    {
        FakeProvider exploding = new("explosive", input: "image/", output: "image/png", throwOnGenerate: true);
        RenditionPipeline pipeline = BuildPipeline([exploding]);

        RenditionPipelineException ex = await Should.ThrowAsync<RenditionPipelineException>(async () => await pipeline.ExecuteAsync(
            new MemoryStream([1]),
            "image/jpeg",
            new RenditionTarget(RenditionType.Thumbnail, "image/png"),
            TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("explosive");
    }

    private static RenditionPipeline BuildPipeline(IRenditionProvider[] providers, int maxChainLength = 3)
    {
        IOptions<GranitRenditionsOptions> options = Microsoft.Extensions.Options.Options.Create(
            new GranitRenditionsOptions { MaxChainLength = maxChainLength });
        return new RenditionPipeline(providers, options);
    }

    private sealed class FakeProvider(string name, string input, string output, bool throwOnGenerate = false) : IRenditionProvider
    {
        public string Name { get; } = name;
        public string OutputContentType { get; } = output;
        public int InvocationCount { get; private set; }

        public bool CanHandle(string sourceContentType) =>
            input.EndsWith('/')
                ? sourceContentType.StartsWith(input, StringComparison.OrdinalIgnoreCase)
                : string.Equals(sourceContentType, input, StringComparison.OrdinalIgnoreCase);

        public Task<RenditionResult> GenerateAsync(
            Stream source,
            string sourceContentType,
            RenditionTarget target,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            if (throwOnGenerate)
            {
                throw new InvalidOperationException("provider boom");
            }
            return Task.FromResult(new RenditionResult([0xFE, 0xED], OutputContentType, target.Dimensions?.Width, target.Dimensions?.Height));
        }
    }
}
