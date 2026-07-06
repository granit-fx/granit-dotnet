using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Identity;

public sealed class RecordIdentityTests
{
    private sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Update_HasUpdateOperationAndExistingEntity()
    {
        TestEntity existing = new() { Name = "Alice" };

        RecordIdentity<TestEntity> identity = new()
        {
            Operation = RecordOperation.Update,
            ExistingEntity = existing,
        };

        identity.Operation.ShouldBe(RecordOperation.Update);
        identity.ExistingEntity.ShouldBeSameAs(existing);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        TestEntity entity = new() { Name = "Bob" };

        RecordIdentity<TestEntity> a = new() { Operation = RecordOperation.Update, ExistingEntity = entity };
        RecordIdentity<TestEntity> b = new() { Operation = RecordOperation.Update, ExistingEntity = entity };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentOperations_AreNotEqual()
    {
        RecordIdentity<TestEntity> a = new() { Operation = RecordOperation.Insert };
        RecordIdentity<TestEntity> b = new() { Operation = RecordOperation.Update };

        a.ShouldNotBe(b);
    }
}
