using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Events;
using Granit.Documents.Renditions.Diagnostics;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Exceptions;
using Granit.Documents.Renditions.Options;
using Granit.Documents.Renditions.Pipeline;
using Granit.Documents.Renditions.Wolverine;
using Granit.Documents.Renditions.Wolverine.Handlers;
using Granit.Documents.Renditions.Wolverine.Internal;
using Granit.Documents.Renditions.Wolverine.Policies;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Wolverine.Tests;

public sealed class DocumentVersionAddedRenditionsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 10, 0, 0, TimeSpan.Zero);

    private static IClock FixedClock(DateTimeOffset now)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(now);
        return clock;
    }

    private static IGuidGenerator GuidGen()
    {
        IGuidGenerator gen = Substitute.For<IGuidGenerator>();
        gen.Create().Returns(_ => Guid.NewGuid());
        return gen;
    }

    private static DocumentVersionAddedEvent Evt(string contentType) =>
        new(
            DocumentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            VersionId: Guid.NewGuid(),
            VersionNumber: 1,
            BlobDescriptorId: Guid.NewGuid(),
            ContentType: contentType,
            SizeBytes: 4096,
            UploadedByUserId: Guid.NewGuid());

    private static RenditionGenerationService BuildService(
        IRenditionStore store,
        IRenditionPipeline pipeline,
        IRenditionSourceFetcher fetcher,
        IRenditionResultUploader uploader,
        ITenantQuotaService quotas)
        => new(
            store, pipeline, fetcher, uploader, quotas,
            GuidGen(),
            FixedClock(Now),
            Microsoft.Extensions.Options.Options.Create(new GranitRenditionsOptions()),
            new RenditionsMetrics(new TestMeterFactory()),
            NullLogger<RenditionGenerationService>.Instance);

    [Fact]
    public async Task Unknown_mime_short_circuits_without_calling_store()
    {
        IRenditionStore store = Substitute.For<IRenditionStore>();
        IRenditionPipeline pipeline = Substitute.For<IRenditionPipeline>();
        IRenditionSourceFetcher fetcher = Substitute.For<IRenditionSourceFetcher>();
        IRenditionResultUploader uploader = Substitute.For<IRenditionResultUploader>();
        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();
        IRenditionTypePolicy policy = new DefaultRenditionTypePolicy(Microsoft.Extensions.Options.Options.Create(new GranitRenditionsOptions()));

        RenditionGenerationService svc = BuildService(store, pipeline, fetcher, uploader, quotas);

        await DocumentVersionAddedRenditionsHandler.HandleAsync(
            Evt("video/mp4"), policy, svc,
            NullLogger<DocumentVersionAddedRenditionsHandler>.Instance,
            CancellationToken.None);

        await store.DidNotReceiveWithAnyArgs().AddAsync(default!, Arg.Any<CancellationToken>());
        await pipeline.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Image_runs_pipeline_and_marks_each_rendition_ready()
    {
        IRenditionStore store = Substitute.For<IRenditionStore>();
        store.FindAsync(Arg.Any<Guid>(), Arg.Any<RenditionType>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DocumentRendition?)null);

        IRenditionPipeline pipeline = Substitute.For<IRenditionPipeline>();
        pipeline.ExecuteAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<RenditionTarget>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RenditionResult(new byte[100], ci.ArgAt<RenditionTarget>(2).TargetContentType, 200, 200));

        IRenditionSourceFetcher fetcher = Substitute.For<IRenditionSourceFetcher>();
        fetcher.OpenSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(new byte[10])));

        var blobId = Guid.NewGuid();
        IRenditionResultUploader uploader = Substitute.For<IRenditionResultUploader>();
        uploader.UploadAsync(Arg.Any<RenditionResult>(), Arg.Any<CancellationToken>()).Returns(blobId);

        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();

        IRenditionTypePolicy policy = new DefaultRenditionTypePolicy(Microsoft.Extensions.Options.Options.Create(new GranitRenditionsOptions()));
        RenditionGenerationService svc = BuildService(store, pipeline, fetcher, uploader, quotas);

        DocumentVersionAddedEvent evt = Evt("image/png");

        await DocumentVersionAddedRenditionsHandler.HandleAsync(
            evt, policy, svc,
            NullLogger<DocumentVersionAddedRenditionsHandler>.Instance,
            CancellationToken.None);

        await store.Received(2).AddAsync(Arg.Any<DocumentRendition>(), Arg.Any<CancellationToken>());
        await pipeline.Received(2).ExecuteAsync(Arg.Any<Stream>(), "image/png", Arg.Any<RenditionTarget>(), Arg.Any<CancellationToken>());
        await uploader.Received(2).UploadAsync(Arg.Any<RenditionResult>(), Arg.Any<CancellationToken>());
        await quotas.Received(2).IncrementRenditionAsync(evt.TenantId!.Value, 100, Arg.Any<CancellationToken>());

        List<DocumentRendition> updated = [];
        foreach (NSubstitute.Core.ICall call in store.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name == nameof(IRenditionStore.UpdateAsync))
            {
                updated.Add((DocumentRendition)call.GetArguments()[0]!);
            }
        }
        updated.Count.ShouldBe(4);
        updated[^1].Status.ShouldBe(RenditionStatus.Ready);
        updated[^1].BlobDescriptorId.ShouldBe(blobId);
    }

    [Fact]
    public async Task Pipeline_exception_marks_row_failed_and_skips_quota()
    {
        IRenditionStore store = Substitute.For<IRenditionStore>();
        store.FindAsync(Arg.Any<Guid>(), Arg.Any<RenditionType>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DocumentRendition?)null);

        IRenditionPipeline pipeline = Substitute.For<IRenditionPipeline>();
        pipeline.ExecuteAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<RenditionTarget>(), Arg.Any<CancellationToken>())
            .Returns<Task<RenditionResult>>(_ => throw new RenditionPipelineException("no provider chain"));

        IRenditionSourceFetcher fetcher = Substitute.For<IRenditionSourceFetcher>();
        fetcher.OpenSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(new byte[10])));

        IRenditionResultUploader uploader = Substitute.For<IRenditionResultUploader>();
        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();

        IRenditionTypePolicy policy = new DefaultRenditionTypePolicy(Microsoft.Extensions.Options.Options.Create(new GranitRenditionsOptions()));
        RenditionGenerationService svc = BuildService(store, pipeline, fetcher, uploader, quotas);

        await DocumentVersionAddedRenditionsHandler.HandleAsync(
            Evt("application/pdf"), policy, svc,
            NullLogger<DocumentVersionAddedRenditionsHandler>.Instance,
            CancellationToken.None);

        await uploader.DidNotReceiveWithAnyArgs().UploadAsync(default!, Arg.Any<CancellationToken>());
        await quotas.DidNotReceiveWithAnyArgs().IncrementRenditionAsync(default, default, Arg.Any<CancellationToken>());

        DocumentRendition? failed = null;
        foreach (NSubstitute.Core.ICall call in store.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name == nameof(IRenditionStore.UpdateAsync))
            {
                failed = (DocumentRendition)call.GetArguments()[0]!;
            }
        }
        failed.ShouldNotBeNull();
        failed.Status.ShouldBe(RenditionStatus.Failed);
        failed.FailureReason.ShouldBe("no provider chain");
    }

    private sealed class TestMeterFactory : IMeterFactory
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
