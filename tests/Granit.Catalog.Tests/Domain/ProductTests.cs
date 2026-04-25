using Granit.Catalog.Domain;
using Granit.Catalog.Events;
using Granit.Domain;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Catalog.Tests.Domain;

public sealed class ProductTests
{
    private static Product NewDraftProduct(string sku = "API-CALLS", string name = "API Calls") =>
        Product.Create(Guid.NewGuid(), sku, name, ProductType.Metered, "call");

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldStartInDraftStatus()
    {
        Product product = NewDraftProduct();

        product.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
    }

    [Fact]
    public void Create_ShouldSetAllProvidedFields()
    {
        var id = Guid.NewGuid();

        var product = Product.Create(id, "STORAGE-GB", "Storage", ProductType.Metered, "GB", "Per-GB storage usage");

        product.Id.ShouldBe(id);
        product.Sku.ShouldBe("STORAGE-GB");
        product.Name.ShouldBe("Storage");
        product.Type.ShouldBe(ProductType.Metered);
        product.Unit.ShouldBe("GB");
        product.Description.ShouldBe("Per-GB storage usage");
    }

    [Fact]
    public void Create_ShouldInitializeEmptyMetadataAndExternalMappings()
    {
        Product product = NewDraftProduct();

        product.MetadataJson.ShouldBeNull();
        product.GetMetadata().ShouldBeEmpty();
        product.ExternalMappings.ShouldBeEmpty();
    }

    [Fact]
    public void Create_ShouldRaiseProductCreatedDomainEvent()
    {
        Product product = NewDraftProduct("SEAT", "Seat");

        product.DomainEvents
            .OfType<ProductCreatedEvent>()
            .ShouldContain(e => e.Sku == "SEAT" && e.Name == "Seat" && e.Type == ProductType.Metered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingSku_ShouldThrow(string? sku) =>
        Should.Throw<ArgumentException>(() => Product.Create(
            Guid.NewGuid(), sku!, "Name", ProductType.Service, "unit"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_ShouldThrow(string? name) =>
        Should.Throw<ArgumentException>(() => Product.Create(
            Guid.NewGuid(), "SKU", name!, ProductType.Service, "unit"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingUnit_ShouldThrow(string? unit) =>
        Should.Throw<ArgumentException>(() => Product.Create(
            Guid.NewGuid(), "SKU", "Name", ProductType.Service, unit!));

    // ── Update (Draft only) ─────────────────────────────────────────────────

    [Fact]
    public void Update_WhenDraft_ShouldChangeFields()
    {
        Product product = NewDraftProduct();

        product.Update("New Name", "New description", "request");

        product.Name.ShouldBe("New Name");
        product.Description.ShouldBe("New description");
        product.Unit.ShouldBe("request");
    }

    [Fact]
    public void Update_WhenPublished_ShouldThrow()
    {
        Product product = NewDraftProduct();
        product.Publish();

        Should.Throw<InvalidOperationException>(() => product.Update("New", null, "unit"));
    }

    [Fact]
    public void Update_WhenArchived_ShouldThrow()
    {
        Product product = NewDraftProduct();
        product.Publish();
        product.Archive();

        Should.Throw<InvalidOperationException>(() => product.Update("New", null, "unit"));
    }

    // ── Lifecycle transitions ───────────────────────────────────────────────

    [Fact]
    public void Publish_FromDraft_ShouldTransitionToPublished()
    {
        Product product = NewDraftProduct();

        product.Publish();

        product.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Published);
    }

    [Fact]
    public void Publish_ShouldRaiseProductPublishedDomainEvent()
    {
        Product product = NewDraftProduct("SEAT", "Seat");

        product.Publish();

        product.DomainEvents
            .OfType<ProductPublishedEvent>()
            .ShouldContain(e => e.Sku == "SEAT");
    }

    [Fact]
    public void Publish_FromAlreadyPublished_ShouldThrow()
    {
        Product product = NewDraftProduct();
        product.Publish();

        Should.Throw<InvalidOperationException>(() => product.Publish());
    }

    [Fact]
    public void Publish_FromArchived_ShouldThrow()
    {
        Product product = NewDraftProduct();
        product.Publish();
        product.Archive();

        Should.Throw<InvalidOperationException>(() => product.Publish());
    }

    [Fact]
    public void Archive_FromPublished_ShouldTransitionToArchived()
    {
        Product product = NewDraftProduct();
        product.Publish();

        product.Archive();

        product.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Archived);
    }

    [Fact]
    public void Archive_ShouldRaiseProductArchivedDomainEvent()
    {
        Product product = NewDraftProduct("SEAT", "Seat");
        product.Publish();

        product.Archive();

        product.DomainEvents
            .OfType<ProductArchivedEvent>()
            .ShouldContain(e => e.Sku == "SEAT");
    }

    [Fact]
    public void Archive_FromDraft_ShouldThrow()
    {
        Product product = NewDraftProduct();

        Should.Throw<InvalidOperationException>(() => product.Archive());
    }

    [Fact]
    public void Archive_FromAlreadyArchived_ShouldThrow()
    {
        Product product = NewDraftProduct();
        product.Publish();
        product.Archive();

        Should.Throw<InvalidOperationException>(() => product.Archive());
    }

    // ── External mappings ───────────────────────────────────────────────────

    [Fact]
    public void AddExternalMapping_ShouldAppendToCollection()
    {
        Product product = NewDraftProduct();
        var mapping = ProductExternalMapping.Create(Guid.NewGuid(), "stripe", "prod_abc123");

        product.AddExternalMapping(mapping);

        product.ExternalMappings.Count.ShouldBe(1);
        product.ExternalMappings[0].ProviderName.ShouldBe("stripe");
        product.ExternalMappings[0].ExternalId.ShouldBe("prod_abc123");
    }

    [Fact]
    public void AddExternalMapping_OnPublishedProduct_ShouldSucceed()
    {
        Product product = NewDraftProduct();
        product.Publish();

        var mapping = ProductExternalMapping.Create(Guid.NewGuid(), "avalara", "PS080100");
        product.AddExternalMapping(mapping);

        product.ExternalMappings.Count.ShouldBe(1);
    }

    [Fact]
    public void AddExternalMapping_WithNull_ShouldThrow() =>
        Should.Throw<ArgumentNullException>(() => NewDraftProduct().AddExternalMapping(null!));

    [Fact]
    public void RemoveExternalMapping_WithKnownId_ShouldRemove()
    {
        Product product = NewDraftProduct();
        var mapping = ProductExternalMapping.Create(Guid.NewGuid(), "stripe", "prod_abc");
        product.AddExternalMapping(mapping);

        bool removed = product.RemoveExternalMapping(mapping.Id);

        removed.ShouldBeTrue();
        product.ExternalMappings.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveExternalMapping_WithUnknownId_ShouldReturnFalse()
    {
        Product product = NewDraftProduct();

        bool removed = product.RemoveExternalMapping(Guid.NewGuid());

        removed.ShouldBeFalse();
    }

    // ── Extra properties (IHasMetadata) ──────────────────────────────

    [Fact]
    public void ReplaceMetadata_ShouldStoreAsJson()
    {
        Product product = NewDraftProduct();
        Dictionary<string, string> meta = new() { ["region"] = "eu-west-1", ["tier"] = "premium" };

        product.ReplaceMetadata(meta);

        product.MetadataJson.ShouldNotBeNull();
        product.GetMetadataValue("region").ShouldBe("eu-west-1");
        product.GetMetadataValue("tier").ShouldBe("premium");
    }

    [Fact]
    public void ReplaceMetadata_WithEmptyDictionary_ShouldClearJson()
    {
        Product product = NewDraftProduct();
        product.ReplaceMetadata(new Dictionary<string, string> { ["foo"] = "bar" });

        product.ReplaceMetadata(new Dictionary<string, string>());

        product.MetadataJson.ShouldBeNull();
    }

    [Fact]
    public void ReplaceMetadata_OnPublishedProduct_ShouldSucceed()
    {
        Product product = NewDraftProduct();
        product.Publish();

        product.ReplaceMetadata(new Dictionary<string, string> { ["channel"] = "marketplace" });

        product.GetMetadataValue("channel").ShouldBe("marketplace");
    }

    [Fact]
    public void ReplaceMetadata_WithNull_ShouldThrow() =>
        Should.Throw<ArgumentNullException>(() => NewDraftProduct().ReplaceMetadata(null!));

    [Fact]
    public void SetMetadataValue_ViaFrameworkExtension_ShouldRoundTrip()
    {
        Product product = NewDraftProduct();

        product.SetMetadataValue("env", "prod");

        product.GetMetadataValue("env").ShouldBe("prod");
        product.HasMetadataValue("env").ShouldBeTrue();
    }

    // ── IWorkflowStateful contract ──────────────────────────────────────────

    [Fact]
    public void IWorkflowStateful_StatusPropertyName_ShouldBeLifecycleStatus() =>
        StaticOf<Product>.StatusPropertyName.ShouldBe(nameof(Product.LifecycleStatus));

    [Fact]
    public void IWorkflowStateful_WorkflowEntityType_ShouldBeProduct() =>
        StaticOf<Product>.WorkflowEntityType.ShouldBe("Product");

    [Fact]
    public void GetWorkflowEntityId_ShouldReturnIdAsString()
    {
        Product product = NewDraftProduct();

        product.GetWorkflowEntityId().ShouldBe(product.Id.ToString());
    }

    // Helper to invoke C# 11 static abstract members via a generic constraint.
    private static class StaticOf<T> where T : IWorkflowStateful
    {
        public static string StatusPropertyName => T.StatusPropertyName;

        public static string WorkflowEntityType => T.WorkflowEntityType;
    }
}
