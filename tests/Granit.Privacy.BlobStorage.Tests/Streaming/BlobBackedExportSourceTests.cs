using Granit.Domain.ValueObjects;
using Granit.Privacy.BlobStorage.Streaming;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Exceptions;
using Granit.Privacy.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;
using ConfigOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Privacy.BlobStorage.Tests.Streaming;

public sealed class BlobBackedExportSourceTests
{
    private static readonly PrivacyExportContext Context = new(
        RequestId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        SubjectUserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        CallerUserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        TenantId: null,
        Regulation: "EU_GDPR");

    private static BlobBackedExportSource CreateSut() => new(
        new EphemeralExportHmacSigner(NullLogger<EphemeralExportHmacSigner>.Instance),
        ConfigOptions.Create(new GranitPrivacyOptions()),
        new FakeTimeProvider(DateTimeOffset.Parse("2026-05-26T12:00:00Z")));

    [Fact]
    public async Task StreamAsync_YieldsOneFragmentPerItem_PreservingOrder()
    {
        BlobBackedExportSource sut = CreateSut();
        var blobA = BlobReference.Create(Guid.NewGuid().ToString());
        var blobB = BlobReference.Create(Guid.NewGuid().ToString());

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in sut.StreamAsync(
            ToAsync(new BlobBackedExportItem[]
            {
                new("documents-content", blobA, "Documents/2024/a.pdf", "application/pdf", 100),
                new("documents-content", blobB, "Documents/2024/b.pdf", "application/pdf", 200),
            }),
            Context, "documents", TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.Count.ShouldBe(2);
        fragments[0].ShouldBeOfType<PassThroughExportFragment>();
        fragments[0].EntryPath.ShouldBe("Documents/2024/a.pdf");
        fragments[1].EntryPath.ShouldBe("Documents/2024/b.pdf");
    }

    [Fact]
    public async Task StreamAsync_SignsHmac_VerifiableByTheSameSigner()
    {
        EphemeralExportHmacSigner signer = new(NullLogger<EphemeralExportHmacSigner>.Instance);
        // Future date — EphemeralExportHmacSigner.Verify rejects tags whose ExpiresAt is in the
        // past relative to TimeProvider.System (wall clock).
        FakeTimeProvider clock = new(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
        BlobBackedExportSource sut = new(
            signer,
            ConfigOptions.Create(new GranitPrivacyOptions()),
            clock);

        var blob = BlobReference.Create(Guid.NewGuid().ToString());
        var fragment = (PassThroughExportFragment)await FirstAsync(sut.StreamAsync(
            ToAsync(new BlobBackedExportItem[]
            {
                new("documents-content", blob, "Documents/x.pdf", "application/pdf"),
            }),
            Context, "documents", TestContext.Current.CancellationToken));

        DateTimeOffset expectedExpiry =
            clock.GetUtcNow() + TimeSpan.FromMinutes(new GranitPrivacyOptions().ExportTimeoutMinutes * 4);

        signer.Verify(
            new ExportHmacParameters(
                Context.RequestId,
                Context.SubjectUserId,
                ProviderName: "documents",
                FragmentKind: "passthrough",
                SourceContainer: "documents-content",
                SourceBlobId: Guid.Parse(blob.Value),
                EntryPath: "Documents/x.pdf",
                ExpiresAt: expectedExpiry),
            fragment.IntegrityTag).ShouldBeTrue();
    }

    [Fact]
    public async Task StreamAsync_SanitizesEntryPath_RejectsZipSlip()
    {
        BlobBackedExportSource sut = CreateSut();
        var blob = BlobReference.Create(Guid.NewGuid().ToString());

        await Should.ThrowAsync<InvalidExportEntryPathException>(async () =>
        {
            await foreach (ExportFragment _ in sut.StreamAsync(
                ToAsync(new BlobBackedExportItem[]
                {
                    new("documents-content", blob, "../etc/passwd", "application/octet-stream"),
                }),
                Context, "documents", TestContext.Current.CancellationToken))
            {
                // Should not reach here.
            }
        });
    }

    [Fact]
    public async Task StreamAsync_RejectsNonGuidBlobReference()
    {
        BlobBackedExportSource sut = CreateSut();
        var blob = BlobReference.Create("not-a-guid");

        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await foreach (ExportFragment _ in sut.StreamAsync(
                ToAsync(new BlobBackedExportItem[]
                {
                    new("documents-content", blob, "doc.pdf", "application/pdf"),
                }),
                Context, "documents", TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public async Task StreamAsync_CarriesSourceContainerOntoFragment()
    {
        BlobBackedExportSource sut = CreateSut();
        var blob = BlobReference.Create(Guid.NewGuid().ToString());

        var fragment = (PassThroughExportFragment)await FirstAsync(sut.StreamAsync(
            ToAsync(new BlobBackedExportItem[]
            {
                new("attachments-bucket", blob, "Attachments/x.bin", "application/octet-stream", 42),
            }),
            Context, "attachments", TestContext.Current.CancellationToken));

        fragment.SourceContainer.ShouldBe("attachments-bucket");
        fragment.SourceBlob.ShouldBe(blob);
        fragment.KnownSizeBytes.ShouldBe(42);
        fragment.ContentType.ShouldBe("application/octet-stream");
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static async Task<ExportFragment> FirstAsync(IAsyncEnumerable<ExportFragment> source)
    {
        await foreach (ExportFragment f in source)
        {
            return f;
        }
        throw new InvalidOperationException("Empty sequence.");
    }
}
