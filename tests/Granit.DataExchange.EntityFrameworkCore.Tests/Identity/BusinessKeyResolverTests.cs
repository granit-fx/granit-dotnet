using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Identity;

public sealed class BusinessKeyResolverTests
{
    private static string NewDb() => Guid.NewGuid().ToString();

    private static BusinessKeyResolver<TestEntity, TestAppDbContext> CreateResolver(string dbName) =>
        new(new InMemoryAppContextFactory(dbName), new TestImportDefinition());

    private static async Task SeedEntityAsync(string dbName, TestEntity entity)
    {
        InMemoryAppContextFactory factory = new(dbName);
        await using TestAppDbContext context = factory.CreateDbContext();
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ResolveAsync_returns_insert_when_no_match()
    {
        // Arrange
        string dbName = NewDb();
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity entity = new() { Niss = "123456" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            entity, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
        result.ExistingEntity.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_returns_update_when_match_found()
    {
        // Arrange
        string dbName = NewDb();
        TestEntity existing = new() { Id = Guid.NewGuid(), Name = "Alice", Niss = "123456" };
        await SeedEntityAsync(dbName, existing);

        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity incoming = new() { Name = "Alice Updated", Niss = "123456" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            incoming, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Update);
        result.ExistingEntity.ShouldNotBeNull();
        result.ExistingEntity.Id.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task ResolveAsync_different_key_value_returns_insert()
    {
        // Arrange
        string dbName = NewDb();
        TestEntity existing = new() { Id = Guid.NewGuid(), Name = "Alice", Niss = "111111" };
        await SeedEntityAsync(dbName, existing);

        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity incoming = new() { Name = "Bob", Niss = "222222" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            incoming, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveAsync_null_key_returns_insert()
    {
        // Arrange
        string dbName = NewDb();
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity entity = new() { Niss = null };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            entity, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }
}
