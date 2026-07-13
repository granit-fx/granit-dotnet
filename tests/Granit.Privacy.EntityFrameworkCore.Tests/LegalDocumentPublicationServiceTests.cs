using Granit.Encryption;
using Granit.Events;
using Granit.MultiTenancy;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Granit.Privacy.LegalAgreements.Domain;
using Granit.Privacy.LegalAgreements.Exceptions;
using Granit.Testing.Fakes;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

public sealed class LegalDocumentPublicationServiceTests
{
    [Fact]
    public async Task PublishAsync_UnknownDocument_ThrowsLegalDocumentNotFound()
    {
        await using var h = Harness.Create();
        var service = new LegalDocumentPublicationService(h.Factory, Substitute.For<IDistributedEventBus>());

        var unknownId = Guid.NewGuid();
        LegalDocumentNotFoundException ex = await Should.ThrowAsync<LegalDocumentNotFoundException>(
            () => service.PublishAsync(unknownId, TestContext.Current.CancellationToken));

        ex.DocumentId.ShouldBe(unknownId);
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublishedDocument_ThrowsLegalDocumentNotPublishable()
    {
        await using var h = Harness.Create();

        var doc = LegalDocument.Create(Guid.NewGuid(), "tos", "Terms", null, null);
        doc.Publish();
        await h.SeedAsync(doc, TestContext.Current.CancellationToken);

        var service = new LegalDocumentPublicationService(h.Factory, Substitute.For<IDistributedEventBus>());

        LegalDocumentNotPublishableException ex = await Should.ThrowAsync<LegalDocumentNotPublishableException>(
            () => service.PublishAsync(doc.Id, TestContext.Current.CancellationToken));

        ex.DocumentId.ShouldBe(doc.Id);
        ex.Status.ShouldBe(WorkflowLifecycleStatus.Published);
    }

    [Fact]
    public void LegalDocument_Create_should_set_properties()
    {
        var id = Guid.NewGuid();
        var doc = LegalDocument.Create(id, "privacy-policy", "Privacy Policy", "Initial version", "Legal.PrivacyPolicy");

        doc.Id.ShouldBe(id);
        doc.DocumentId.ShouldBe("privacy-policy");
        doc.DisplayName.ShouldBe("Privacy Policy");
        doc.Description.ShouldBe("Initial version");
        doc.TemplateName.ShouldBe("Legal.PrivacyPolicy");
        doc.DocumentBlobId.ShouldBeNull();
    }

    [Fact]
    public void LegalDocument_UpdateDraft_should_update_metadata()
    {
        var doc = LegalDocument.Create(Guid.NewGuid(), "tos", "Terms", null, null);

        doc.UpdateDraft("Updated Terms", "Changed clause 3", "Legal.ToS", Guid.NewGuid());

        doc.DisplayName.ShouldBe("Updated Terms");
        doc.Description.ShouldBe("Changed clause 3");
        doc.TemplateName.ShouldBe("Legal.ToS");
        doc.DocumentBlobId.ShouldNotBeNull();
    }

    [Fact]
    public void LegalDocument_AttachDocument_should_set_blob_id()
    {
        var doc = LegalDocument.Create(Guid.NewGuid(), "tos", "Terms");
        var blobId = Guid.NewGuid();

        doc.AttachDocument(blobId);

        doc.DocumentBlobId.ShouldBe(blobId);
    }

    /// <summary>
    /// In-memory <see cref="PrivacyDbContext"/> harness. The publish exception paths query by
    /// primary key and check lifecycle state before touching any tenant/publishable filter, so
    /// the EF InMemory provider (which ignores query filters) is sufficient here.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        private static readonly IStringEncryptionService Encryption = new PassthroughEncryption();
        private static readonly ICurrentTenant Tenant = new FakeCurrentTenant();

        private Harness(TestFactory factory) => Factory = factory;

        public TestFactory Factory { get; }

        public static Harness Create()
        {
            DbContextOptions<PrivacyDbContext> options = new DbContextOptionsBuilder<PrivacyDbContext>()
                .UseInMemoryDatabase($"privacy-legaldoc-{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new Harness(new TestFactory(options));
        }

        public async Task SeedAsync(LegalDocument document, CancellationToken ct)
        {
            await using PrivacyDbContext db = await Factory.CreateDbContextAsync(ct);
            db.LegalDocuments.Add(document);
            await db.SaveChangesAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            await using PrivacyDbContext db = await Factory.CreateDbContextAsync();
            await db.Database.EnsureDeletedAsync();
        }

        public sealed class TestFactory(DbContextOptions<PrivacyDbContext> options)
            : IDbContextFactory<PrivacyDbContext>
        {
            public PrivacyDbContext CreateDbContext() => new(options, Encryption, Tenant);
            public Task<PrivacyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new PrivacyDbContext(options, Encryption, Tenant));
        }

        private sealed class PassthroughEncryption : IStringEncryptionService
        {
            public string Encrypt(string plainText) => plainText;
            public string? Decrypt(string cipherText) => cipherText;
        }
    }
}
