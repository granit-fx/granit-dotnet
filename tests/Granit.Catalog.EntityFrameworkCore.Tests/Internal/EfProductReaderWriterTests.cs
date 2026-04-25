using Granit.Catalog;
using Granit.Catalog.Domain;
using Granit.Catalog.EntityFrameworkCore.Internal;
using Granit.Domain;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

// EF1001: the Internal namespace is intentional for friend-assembly test access
// to the catalog's internal Reader/Writer implementations. Suppress here.
#pragma warning disable EF1001

namespace Granit.Catalog.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// CRUD coverage for <see cref="EfProductReader"/> + <see cref="EfProductWriter"/>
/// against an isolated EF Core InMemory provider.
///
/// Each test uses a fresh database name to prevent state leakage. The InMemory
/// provider does not enforce relational constraints (unique indices, FKs), so
/// tests focus on round-trip behavior, lifecycle persistence, eager-loading
/// of <see cref="Product.ExternalMappings"/>, and metadata serialization.
/// Constraint enforcement is exercised by the integration tests in the
/// consuming app's PostgreSQL fixture.
/// </summary>
public sealed class EfProductReaderWriterTests
{
    private static (IProductReader Reader, IProductWriter Writer, IDbContextFactory<CatalogDbContext> Factory) BuildStores(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContextFactory<CatalogDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        ServiceProvider sp = services.BuildServiceProvider();
        IDbContextFactory<CatalogDbContext> factory = sp.GetRequiredService<IDbContextFactory<CatalogDbContext>>();

        IProductReader reader = new EfProductReader(factory);
        IProductWriter writer = new EfProductWriter(factory);
        return (reader, writer, factory);
    }

    private static Product NewProduct(string sku = "API-CALLS", string name = "API Calls", ProductType type = ProductType.Metered) =>
        Product.Create(Guid.NewGuid(), sku, name, type, "call");

    [Fact]
    public async Task AddAsync_ThenGetById_ShouldRoundTripAggregate()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(AddAsync_ThenGetById_ShouldRoundTripAggregate));
        Product product = NewProduct();

        await writer.AddAsync(product, TestContext.Current.CancellationToken);

        Product? loaded = await reader.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded!.Sku.ShouldBe("API-CALLS");
        loaded.Name.ShouldBe("API Calls");
        loaded.Type.ShouldBe(ProductType.Metered);
        loaded.Unit.ShouldBe("call");
        loaded.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
    }

    [Fact]
    public async Task GetBySku_ShouldReturn_MatchingProduct()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(GetBySku_ShouldReturn_MatchingProduct));
        await writer.AddAsync(NewProduct("STORAGE-GB", "Storage", ProductType.Metered), TestContext.Current.CancellationToken);
        await writer.AddAsync(NewProduct("SEAT", "Seat", ProductType.Service), TestContext.Current.CancellationToken);

        Product? found = await reader.GetBySkuAsync("SEAT", TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found!.Type.ShouldBe(ProductType.Service);
    }

    [Fact]
    public async Task GetBySku_WithUnknownSku_ShouldReturnNull()
    {
        (IProductReader reader, _, _) = BuildStores(nameof(GetBySku_WithUnknownSku_ShouldReturnNull));

        Product? found = await reader.GetBySkuAsync("DOES-NOT-EXIST", TestContext.Current.CancellationToken);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task GetByStatus_ShouldFilter_ByLifecycleStatus()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(GetByStatus_ShouldFilter_ByLifecycleStatus));

        Product draft = NewProduct("DRAFT-1");
        Product published = NewProduct("PUB-1");
        published.Publish();
        Product archived = NewProduct("ARCH-1");
        archived.Publish();
        archived.Archive();

        await writer.AddAsync(draft, TestContext.Current.CancellationToken);
        await writer.AddAsync(published, TestContext.Current.CancellationToken);
        await writer.AddAsync(archived, TestContext.Current.CancellationToken);

        IReadOnlyList<Product> drafts = await reader.GetByStatusAsync(WorkflowLifecycleStatus.Draft, TestContext.Current.CancellationToken);
        IReadOnlyList<Product> publishedList = await reader.GetByStatusAsync(WorkflowLifecycleStatus.Published, TestContext.Current.CancellationToken);
        IReadOnlyList<Product> archivedList = await reader.GetByStatusAsync(WorkflowLifecycleStatus.Archived, TestContext.Current.CancellationToken);

        drafts.ShouldHaveSingleItem();
        drafts[0].Sku.ShouldBe("DRAFT-1");
        publishedList.ShouldHaveSingleItem();
        publishedList[0].Sku.ShouldBe("PUB-1");
        archivedList.ShouldHaveSingleItem();
        archivedList[0].Sku.ShouldBe("ARCH-1");
    }

    [Fact]
    public async Task UpdateAsync_AfterPublish_ShouldPersistNewStatus()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(UpdateAsync_AfterPublish_ShouldPersistNewStatus));
        Product product = NewProduct();
        await writer.AddAsync(product, TestContext.Current.CancellationToken);

        product.Publish();
        await writer.UpdateAsync(product, TestContext.Current.CancellationToken);

        Product? loaded = await reader.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded!.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Published);
    }

    [Fact]
    public async Task UpdateAsync_WithAddedExternalMapping_ShouldInsertNew()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(UpdateAsync_WithAddedExternalMapping_ShouldInsertNew));
        Product product = NewProduct();
        await writer.AddAsync(product, TestContext.Current.CancellationToken);

        product.AddExternalMapping(ProductExternalMapping.Create(Guid.NewGuid(), "stripe", "prod_abc"));
        await writer.UpdateAsync(product, TestContext.Current.CancellationToken);

        Product? loaded = await reader.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded!.ExternalMappings.Count.ShouldBe(1);
        loaded.ExternalMappings[0].ProviderName.ShouldBe("stripe");
        loaded.ExternalMappings[0].ExternalId.ShouldBe("prod_abc");
    }

    [Fact]
    public async Task GetByExternalId_ShouldFind_MatchingProduct()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(GetByExternalId_ShouldFind_MatchingProduct));
        Product product = NewProduct();
        product.AddExternalMapping(ProductExternalMapping.Create(Guid.NewGuid(), "avalara", "PS080100"));
        await writer.AddAsync(product, TestContext.Current.CancellationToken);

        Product? found = await reader.GetByExternalIdAsync("avalara", "PS080100", TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found!.Id.ShouldBe(product.Id);
    }

    [Fact]
    public async Task UpdateAsync_WithMetadataChanges_ShouldRoundTrip()
    {
        (IProductReader reader, IProductWriter writer, _) = BuildStores(nameof(UpdateAsync_WithMetadataChanges_ShouldRoundTrip));
        Product product = NewProduct();
        product.ReplaceMetadata(new Dictionary<string, string> { ["region"] = "eu-west-1", ["channel"] = "saas" });
        await writer.AddAsync(product, TestContext.Current.CancellationToken);

        Product? loaded = await reader.GetByIdAsync(product.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded!.GetMetadataValue("region").ShouldBe("eu-west-1");
        loaded.GetMetadataValue("channel").ShouldBe("saas");
    }
}
