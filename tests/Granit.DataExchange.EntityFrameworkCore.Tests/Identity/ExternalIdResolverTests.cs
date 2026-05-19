using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import.Identity;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Identity;

public sealed class ExternalIdResolverTests
{
    private static string NewDb() => Guid.NewGuid().ToString();

    private static ICurrentTenant CreateTenant(Guid? id = null)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(id.HasValue);
        tenant.Id.Returns(id);
        return tenant;
    }

    private static ExternalIdResolver<TestEntity, TestAppDbContext> CreateResolver(
        string importDbName, string appDbName, ICurrentTenant? tenant = null) =>
        new(
            new InMemoryDataExchangeContextFactory(importDbName),
            new InMemoryAppContextFactory(appDbName),
            new TestExternalIdImportDefinition(),
            tenant ?? CreateTenant());

    [Fact]
    public async Task ResolveAsync_returns_insert_when_no_external_id()
    {
        // Arrange
        string dbName = NewDb();
        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName, dbName);
        TestEntity entity = new() { Name = "Alice", ExternalId = null };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            entity, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveAsync_returns_insert_when_external_id_not_mapped()
    {
        // Arrange
        string dbName = NewDb();
        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName, dbName);
        TestEntity entity = new() { Name = "Alice", ExternalId = "EXT-001" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            entity, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveAsync_returns_update_when_external_id_is_mapped()
    {
        // Arrange
        string importDbName = NewDb();
        string appDbName = NewDb();
        var internalId = Guid.NewGuid();

        // Seed external ID mapping
        InMemoryDataExchangeContextFactory importFactory = new(importDbName);
        await using (DataExchangeDbContext importContext = importFactory.CreateDbContext())
        {
            importContext.ExternalIdMappings.Add(new ExternalIdMappingEntity
            {
                Id = Guid.NewGuid(),
                DefinitionName = "Test.ExternalIdImport",
                ExternalId = "EXT-001",
                InternalId = internalId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await importContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed the actual entity in app context
        InMemoryAppContextFactory appFactory = new(appDbName);
        await using (TestAppDbContext appContext = appFactory.CreateDbContext())
        {
            appContext.TestEntities.Add(new TestEntity
            {
                Id = internalId,
                Name = "Alice",
            });
            await appContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        ExternalIdResolver<TestEntity, TestAppDbContext> resolver =
            CreateResolver(importDbName, appDbName);
        TestEntity incoming = new() { Name = "Alice Updated", ExternalId = "EXT-001" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            incoming, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Update);
        result.ExistingEntity.ShouldNotBeNull();
        result.ExistingEntity.Id.ShouldBe(internalId);
    }

    [Fact]
    public async Task ResolveAsync_returns_insert_when_entity_deleted()
    {
        // Arrange
        string importDbName = NewDb();
        string appDbName = NewDb();
        var internalId = Guid.NewGuid();

        // Seed mapping but NOT the entity (simulating a deleted entity)
        InMemoryDataExchangeContextFactory importFactory = new(importDbName);
        await using (DataExchangeDbContext importContext = importFactory.CreateDbContext())
        {
            importContext.ExternalIdMappings.Add(new ExternalIdMappingEntity
            {
                Id = Guid.NewGuid(),
                DefinitionName = "Test.ExternalIdImport",
                ExternalId = "EXT-002",
                InternalId = internalId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await importContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        ExternalIdResolver<TestEntity, TestAppDbContext> resolver =
            CreateResolver(importDbName, appDbName);
        TestEntity incoming = new() { Name = "Ghost", ExternalId = "EXT-002" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            incoming, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveAsync_empty_external_id_returns_insert()
    {
        // Arrange
        string dbName = NewDb();
        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName, dbName);
        TestEntity entity = new() { Name = "Alice", ExternalId = "" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            entity, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }
}
