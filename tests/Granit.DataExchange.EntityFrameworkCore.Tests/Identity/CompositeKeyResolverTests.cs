using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Identity;

public sealed class CompositeKeyResolverTests
{
    private static string NewDb() => Guid.NewGuid().ToString();

    private static CompositeKeyResolver<TestEntity, TestAppDbContext> CreateResolver(string dbName) =>
        new(new InMemoryAppContextFactory(dbName), new TestCompositeKeyImportDefinition());

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
        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity entity = new() { Name = "Alice", Email = "alice@test.com" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            entity, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }

    [Fact]
    public async Task ResolveAsync_returns_update_when_composite_key_matches()
    {
        // Arrange
        string dbName = NewDb();
        TestEntity existing = new()
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Email = "alice@test.com",
        };
        await SeedEntityAsync(dbName, existing);

        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity incoming = new() { Name = "Alice", Email = "alice@test.com", Niss = "updated" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            incoming, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Update);
        result.ExistingEntity.ShouldNotBeNull();
        result.ExistingEntity.Id.ShouldBe(existing.Id);
    }

    [Fact]
    public async Task ResolveAsync_returns_insert_when_partial_key_matches()
    {
        // Arrange
        string dbName = NewDb();
        TestEntity existing = new()
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Email = "alice@test.com",
        };
        await SeedEntityAsync(dbName, existing);

        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver(dbName);
        TestEntity incoming = new() { Name = "Alice", Email = "different@test.com" };

        // Act
        RecordIdentity<TestEntity> result = await resolver.ResolveAsync(
            incoming, TestContext.Current.CancellationToken);

        // Assert
        result.Operation.ShouldBe(RecordOperation.Insert);
    }
}
