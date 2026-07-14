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
        string importDbName, ICurrentTenant? tenant = null) =>
        new(
            new InMemoryDataExchangeContextFactory(importDbName),
            new TestExternalIdImportDefinition(),
            tenant ?? CreateTenant());

    [Fact]
    public async Task ResolveBatchAsync_returns_insert_when_no_external_id()
    {
        string dbName = NewDb();
        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity entity = new() { Name = "Alice", ExternalId = null };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem().Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveBatchAsync_returns_insert_with_external_id_when_not_mapped()
    {
        string dbName = NewDb();
        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity entity = new() { Name = "Alice", ExternalId = "EXT-001" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        RecordIdentity result = results.ShouldHaveSingleItem();
        result.Operation.ShouldBe(RecordOperation.Insert);
        result.ExternalId.ShouldBe("EXT-001");
    }

    [Fact]
    public async Task ResolveBatchAsync_returns_update_with_primary_key_when_mapped()
    {
        string importDbName = NewDb();
        var internalId = Guid.NewGuid();

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

        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(importDbName);
        TestEntity incoming = new() { Name = "Alice Updated", ExternalId = "EXT-001" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [incoming], TestContext.Current.CancellationToken);

        RecordIdentity result = results.ShouldHaveSingleItem();
        result.Operation.ShouldBe(RecordOperation.Update);
        result.KeyKind.ShouldBe(EntityKeyKind.PrimaryKey);
        result.Key.ShouldBe(new EntityKey(internalId));
    }

    [Fact]
    public async Task ResolveBatchAsync_empty_external_id_returns_insert()
    {
        string dbName = NewDb();
        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity entity = new() { Name = "Alice", ExternalId = "" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem().Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveBatchAsync_issues_one_query_for_the_whole_batch()
    {
        string importDbName = NewDb();
        var mappedId = Guid.NewGuid();

        InMemoryDataExchangeContextFactory importFactory = new(importDbName);
        await using (DataExchangeDbContext importContext = importFactory.CreateDbContext())
        {
            importContext.ExternalIdMappings.Add(new ExternalIdMappingEntity
            {
                Id = Guid.NewGuid(),
                DefinitionName = "Test.ExternalIdImport",
                ExternalId = "EXT-MAPPED",
                InternalId = mappedId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await importContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        ExternalIdResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(importDbName);

        TestEntity mapped = new() { Name = "Mapped", ExternalId = "EXT-MAPPED" };
        TestEntity unmapped = new() { Name = "Unmapped", ExternalId = "EXT-UNMAPPED" };
        TestEntity noExternalId = new() { Name = "NoExternalId", ExternalId = null };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [mapped, unmapped, noExternalId], TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results[0].Operation.ShouldBe(RecordOperation.Update);
        results[0].Key.ShouldBe(new EntityKey(mappedId));
        results[1].Operation.ShouldBe(RecordOperation.Insert);
        results[1].ExternalId.ShouldBe("EXT-UNMAPPED");
        results[2].Operation.ShouldBe(RecordOperation.Insert);
        results[2].ExternalId.ShouldBeNull();
    }
}
