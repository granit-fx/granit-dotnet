using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Identity;

/// <summary>
/// The resolver never touches a <see cref="Microsoft.EntityFrameworkCore.DbContext"/> — every
/// row resolves to Upsert (the executor prefetches and decides insert vs. update), or Ambiguous
/// for a missing key component, or Skip for a key already seen earlier in the same batch/job.
/// </summary>
public sealed class BusinessKeyResolverTests
{
    private static BusinessKeyResolver<TestEntity, TestAppDbContext> CreateResolver() =>
        new(new TestImportDefinition());

    [Fact]
    public async Task ResolveBatchAsync_returns_upsert_with_business_key()
    {
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity entity = new() { Niss = "123456" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        RecordIdentity result = results.ShouldHaveSingleItem();
        result.Operation.ShouldBe(RecordOperation.Upsert);
        result.KeyKind.ShouldBe(EntityKeyKind.BusinessKey);
        result.Key.ShouldBe(new EntityKey("123456"));
    }

    [Fact]
    public async Task ResolveBatchAsync_missing_key_component_is_ambiguous()
    {
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity entity = new() { Niss = null };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        RecordIdentity result = results.ShouldHaveSingleItem();
        result.Operation.ShouldBe(RecordOperation.Ambiguous);
        result.ReasonCodes.ShouldContain(IdentityReasonCodes.MissingKeyComponent);
    }

    [Fact]
    public async Task ResolveBatchAsync_empty_key_component_is_ambiguous()
    {
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity entity = new() { Niss = "" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [entity], TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem().Operation.ShouldBe(RecordOperation.Ambiguous);
    }

    [Fact]
    public async Task ResolveBatchAsync_duplicate_key_in_same_batch_is_skipped()
    {
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity first = new() { Niss = "123456", Name = "Alice" };
        TestEntity second = new() { Niss = "123456", Name = "Alice Duplicate" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [first, second], TestContext.Current.CancellationToken);

        results[0].Operation.ShouldBe(RecordOperation.Upsert);
        results[1].Operation.ShouldBe(RecordOperation.Skip);
        results[1].ReasonCodes.ShouldContain(IdentityReasonCodes.DuplicateKeyInFile);
    }

    [Fact]
    public async Task ResolveBatchAsync_duplicate_key_across_batches_is_skipped()
    {
        // The resolver is scoped per import — state (seen keys) persists across calls.
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity first = new() { Niss = "123456" };
        TestEntity second = new() { Niss = "123456" };

        await resolver.ResolveBatchAsync([first], TestContext.Current.CancellationToken);
        IReadOnlyList<RecordIdentity> secondBatch = await resolver.ResolveBatchAsync(
            [second], TestContext.Current.CancellationToken);

        secondBatch.ShouldHaveSingleItem().Operation.ShouldBe(RecordOperation.Skip);
    }

    [Fact]
    public async Task ResolveBatchAsync_preserves_row_order()
    {
        BusinessKeyResolver<TestEntity, TestAppDbContext> resolver = CreateResolver();
        TestEntity a = new() { Niss = "1" };
        TestEntity b = new() { Niss = "2" };
        TestEntity c = new() { Niss = "3" };

        IReadOnlyList<RecordIdentity> results = await resolver.ResolveBatchAsync(
            [a, b, c], TestContext.Current.CancellationToken);

        results.Count.ShouldBe(3);
        results[0].Key.ShouldBe(new EntityKey("1"));
        results[1].Key.ShouldBe(new EntityKey("2"));
        results[2].Key.ShouldBe(new EntityKey("3"));
    }
}
