using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Execution;

public sealed class ValidatedRowTests
{
    private sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Constructor_WithIdentity_SetsIdentity()
    {
        TestEntity entity = new() { Name = "Bob" };
        RecordIdentity<TestEntity> identity = new()
        {
            Operation = RecordOperation.Update,
            ExistingEntity = new TestEntity { Name = "Existing" },
        };

        ValidatedRow<TestEntity> row = new(10, entity, identity);

        row.Identity.ShouldNotBeNull();
        row.Identity.Operation.ShouldBe(RecordOperation.Update);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        TestEntity entity = new() { Name = "Charlie" };

        ValidatedRow<TestEntity> a = new(1, entity);
        ValidatedRow<TestEntity> b = new(1, entity);

        a.ShouldBe(b);
    }
}
