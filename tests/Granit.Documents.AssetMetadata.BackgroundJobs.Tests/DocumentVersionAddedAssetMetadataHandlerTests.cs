using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.BackgroundJobs;
using Granit.Documents.AssetMetadata.BackgroundJobs.Handlers;
using Granit.Documents.AssetMetadata.BackgroundJobs.Internal;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.Exceptions;
using Granit.Documents.AssetMetadata.Options;
using Granit.Documents.AssetMetadata.Pipeline;
using Granit.Documents.Events;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.BackgroundJobs.Tests;

public sealed class DocumentVersionAddedAssetMetadataHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

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

    private static AssetMetadataGenerationService BuildService(
        IAssetMetadataStore store,
        IAssetMetadataPipeline pipeline,
        IAssetMetadataSourceFetcher fetcher,
        IDocumentService? documentService = null)
        => new(
            store, pipeline, fetcher,
            documentService ?? Substitute.For<IDocumentService>(),
            GuidGen(),
            FixedClock(Now),
            Microsoft.Extensions.Options.Options.Create(new GranitAssetMetadataOptions()),
            NullLogger<AssetMetadataGenerationService>.Instance);

    [Fact]
    public async Task Happy_path_marks_row_ready_with_merged_extraction()
    {
        IAssetMetadataStore store = Substitute.For<IAssetMetadataStore>();
        store.GetByVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DocumentAssetMetadata?)null);

        IAssetMetadataPipeline pipeline = Substitute.For<IAssetMetadataPipeline>();
        pipeline.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<AssetMetadataResult>
            {
                new("exif", new Dictionary<string, string?> { ["Make"] = "Canon" })
                {
                    CameraMake = "Canon", Width = 4000, Height = 3000,
                },
            });

        IAssetMetadataSourceFetcher fetcher = Substitute.For<IAssetMetadataSourceFetcher>();
        fetcher.OpenSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(new byte[10])));

        AssetMetadataGenerationService svc = BuildService(store, pipeline, fetcher);

        DocumentVersionAddedEvent evt = Evt("image/jpeg");
        await DocumentVersionAddedAssetMetadataHandler.HandleAsync(evt, svc, CancellationToken.None);

        await store.Received(1).AddAsync(Arg.Any<DocumentAssetMetadata>(), Arg.Any<CancellationToken>());
        await pipeline.Received(1).ExtractAsync(Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>());

        DocumentAssetMetadata? lastUpdate = null;
        foreach (NSubstitute.Core.ICall call in store.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name == nameof(IAssetMetadataStore.UpdateAsync))
            {
                lastUpdate = (DocumentAssetMetadata)call.GetArguments()[0]!;
            }
        }
        lastUpdate.ShouldNotBeNull();
        lastUpdate.Status.ShouldBe(AssetMetadataStatus.Ready);
        lastUpdate.CameraMake.ShouldBe("Canon");
        lastUpdate.ExtractorCount.ShouldBe(1);
    }

    [Fact]
    public async Task Empty_extractor_chain_still_marks_row_ready()
    {
        IAssetMetadataStore store = Substitute.For<IAssetMetadataStore>();
        store.GetByVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DocumentAssetMetadata?)null);

        IAssetMetadataPipeline pipeline = Substitute.For<IAssetMetadataPipeline>();
        pipeline.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AssetMetadataResult>());

        IAssetMetadataSourceFetcher fetcher = Substitute.For<IAssetMetadataSourceFetcher>();
        fetcher.OpenSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(new byte[10])));

        AssetMetadataGenerationService svc = BuildService(store, pipeline, fetcher);

        await DocumentVersionAddedAssetMetadataHandler.HandleAsync(
            Evt("application/pdf"), svc, CancellationToken.None);

        DocumentAssetMetadata? lastUpdate = null;
        foreach (NSubstitute.Core.ICall call in store.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name == nameof(IAssetMetadataStore.UpdateAsync))
            {
                lastUpdate = (DocumentAssetMetadata)call.GetArguments()[0]!;
            }
        }
        lastUpdate.ShouldNotBeNull();
        lastUpdate.Status.ShouldBe(AssetMetadataStatus.Ready);
        lastUpdate.ExtractorCount.ShouldBe(0);
    }

    [Fact]
    public async Task Pipeline_exception_marks_row_failed()
    {
        IAssetMetadataStore store = Substitute.For<IAssetMetadataStore>();
        store.GetByVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DocumentAssetMetadata?)null);

        IAssetMetadataPipeline pipeline = Substitute.For<IAssetMetadataPipeline>();
        pipeline.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<AssetMetadataResult>>>(_ =>
                throw new AssetMetadataExtractionException(
                    "exif", "image/jpeg", new InvalidOperationException("decoder blew up")));

        IAssetMetadataSourceFetcher fetcher = Substitute.For<IAssetMetadataSourceFetcher>();
        fetcher.OpenSourceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(new byte[10])));

        AssetMetadataGenerationService svc = BuildService(store, pipeline, fetcher);

        await DocumentVersionAddedAssetMetadataHandler.HandleAsync(
            Evt("image/jpeg"), svc, CancellationToken.None);

        DocumentAssetMetadata? lastUpdate = null;
        foreach (NSubstitute.Core.ICall call in store.ReceivedCalls())
        {
            if (call.GetMethodInfo().Name == nameof(IAssetMetadataStore.UpdateAsync))
            {
                lastUpdate = (DocumentAssetMetadata)call.GetArguments()[0]!;
            }
        }
        lastUpdate.ShouldNotBeNull();
        lastUpdate.Status.ShouldBe(AssetMetadataStatus.Failed);
        lastUpdate.FailureReason!.ShouldContain("exif");
    }

    [Fact]
    public async Task Ready_row_is_skipped_idempotently()
    {
        var existing = DocumentAssetMetadata.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", Now);
        existing.MarkExtracting();
        existing.MarkReady(Now.AddSeconds(1));

        IAssetMetadataStore store = Substitute.For<IAssetMetadataStore>();
        store.GetByVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(existing);

        IAssetMetadataPipeline pipeline = Substitute.For<IAssetMetadataPipeline>();
        IAssetMetadataSourceFetcher fetcher = Substitute.For<IAssetMetadataSourceFetcher>();

        AssetMetadataGenerationService svc = BuildService(store, pipeline, fetcher);

        await DocumentVersionAddedAssetMetadataHandler.HandleAsync(
            Evt("image/jpeg"), svc, CancellationToken.None);

        await store.DidNotReceiveWithAnyArgs().UpdateAsync(default!, Arg.Any<CancellationToken>());
        await pipeline.DidNotReceiveWithAnyArgs().ExtractAsync(default!, default!, Arg.Any<CancellationToken>());
        await fetcher.DidNotReceiveWithAnyArgs().OpenSourceAsync(default, Arg.Any<CancellationToken>());
    }
}
