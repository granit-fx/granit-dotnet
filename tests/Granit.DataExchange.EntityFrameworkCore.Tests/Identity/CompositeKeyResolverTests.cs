using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Identity;

public sealed class CompositeKeyResolverTests
{
    private static CompositeKeyResolver<TestEntity, TestAppDbContext> CreateResolver() =>
        new(new TestCompositeKeyImportDefinition());

    [Fact]
    public async Task ResolveBatchAsync_returns_upsert_with_composite_key()
    {
        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity entity = new() { Name = "Alice", Email = "alice@test.com" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        RecordIdentity result = results.ShouldHaveSingleItem();
        result.Operation.ShouldBe(RecordOperation.Upsert);
        result.KeyKind.ShouldBe(EntityKeyKind.BusinessKey);
        result.Key.ShouldBe(new EntityKey("Alice", "alice@test.com"));
    }

    [Fact]
    public async Task ResolveBatchAsync_missing_component_is_ambiguous()
    {
        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity entity = new() { Name = "Alice", Email = null };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem().Operation.ShouldBe(RecordOperation.Ambiguous);
    }

    [Fact]
    public async Task ResolveBatchAsync_duplicate_composite_key_is_skipped()
    {
        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity first = new() { Name = "Alice", Email = "alice@test.com" };
        TestEntity second = new() { Name = "Alice", Email = "alice@test.com" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [first, second], TestContext.Current.CancellationToken);

        results[0].Operation.ShouldBe(RecordOperation.Upsert);
        results[1].Operation.ShouldBe(RecordOperation.Skip);
    }

    [Fact]
    public async Task ResolveBatchAsync_different_component_combination_is_not_duplicate()
    {
        CompositeKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity first = new() { Name = "Alice", Email = "alice@test.com" };
        TestEntity second = new() { Name = "Alice", Email = "different@test.com" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [first, second], TestContext.Current.CancellationToken);

        results[0].Operation.ShouldBe(RecordOperation.Upsert);
        results[1].Operation.ShouldBe(RecordOperation.Upsert);
    }
}
