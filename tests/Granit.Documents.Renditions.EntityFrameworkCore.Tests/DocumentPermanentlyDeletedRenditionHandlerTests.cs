using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents;
using Granit.Documents.Events;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Tests;

public sealed class DocumentPermanentlyDeletedRenditionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_no_renditions_should_be_a_noop()
    {
        IRenditionStore store = Substitute.For<IRenditionStore>();
        store.ListForDocumentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();

        var evt = new DocumentPermanentlyDeletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Now, ReleasedBytes: 0);

        await DocumentPermanentlyDeletedRenditionHandler.HandleAsync(
            evt, store, blobStorage, quotas,
            NullLogger<DocumentPermanentlyDeletedRenditionHandler>.Instance,
            TestContext.Current.CancellationToken);

        await blobStorage.DidNotReceiveWithAnyArgs().DeleteAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().DeleteForDocumentAsync(evt.DocumentId, Arg.Any<CancellationToken>());
        await quotas.DidNotReceiveWithAnyArgs().DecrementRenditionAsync(
            Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_should_delete_blobs_drop_rows_and_decrement_counter()
    {
        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        IRenditionStore store = Substitute.For<IRenditionStore>();
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();

        var blob1 = Guid.NewGuid();
        var blob2 = Guid.NewGuid();

        DocumentRendition r1 = NewRendition(documentId, tenantId, RenditionType.Thumbnail, "image/png", blob1, sizeBytes: 1000);
        DocumentRendition r2 = NewRendition(documentId, tenantId, RenditionType.Web, "image/webp", blob2, sizeBytes: 5000);

        store.ListForDocumentAsync(documentId, Arg.Any<CancellationToken>())
            .Returns(new List<DocumentRendition> { r1, r2 });

        var evt = new DocumentPermanentlyDeletedEvent(documentId, tenantId, Now, ReleasedBytes: 0);

        await DocumentPermanentlyDeletedRenditionHandler.HandleAsync(
            evt, store, blobStorage, quotas,
            NullLogger<DocumentPermanentlyDeletedRenditionHandler>.Instance,
            TestContext.Current.CancellationToken);

        await blobStorage.Received(1).DeleteAsync(
            "document-renditions", blob1, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await blobStorage.Received(1).DeleteAsync(
            "document-renditions", blob2, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await store.Received(1).DeleteForDocumentAsync(documentId, Arg.Any<CancellationToken>());
        await quotas.Received(1).DecrementRenditionAsync(tenantId, 6000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_should_skip_quota_decrement_when_no_tenant()
    {
        var documentId = Guid.NewGuid();
        IRenditionStore store = Substitute.For<IRenditionStore>();
        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();

        store.ListForDocumentAsync(documentId, Arg.Any<CancellationToken>())
            .Returns(new List<DocumentRendition>
            {
                NewRendition(documentId, tenantId: null, RenditionType.Thumbnail, "image/png", Guid.NewGuid(), 1024),
            });

        var evt = new DocumentPermanentlyDeletedEvent(documentId, TenantId: null, Now, ReleasedBytes: 0);

        await DocumentPermanentlyDeletedRenditionHandler.HandleAsync(
            evt, store, blobStorage, quotas,
            NullLogger<DocumentPermanentlyDeletedRenditionHandler>.Instance,
            TestContext.Current.CancellationToken);

        await quotas.DidNotReceiveWithAnyArgs().DecrementRenditionAsync(
            Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private static DocumentRendition NewRendition(
        Guid documentId, Guid? tenantId, RenditionType type, string format,
        Guid blobDescriptorId, long sizeBytes)
    {
        var r = DocumentRendition.CreatePending(
            Guid.NewGuid(), tenantId, documentId, Guid.NewGuid(), type, format, Now);
        r.MarkGenerating();
        r.MarkReady(blobDescriptorId, sizeBytes, width: 100, height: 100, now: Now);
        return r;
    }
}
