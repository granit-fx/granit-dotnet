using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Identity;

public sealed class RecordIdentityTests
{
    [Fact]
    public void Insert_HasInsertOperationAndNoKey()
    {
        var identity = RecordIdentity.Insert();

        identity.Operation.ShouldBe(RecordOperation.Insert);
        identity.Key.ShouldBeNull();
        identity.ExternalId.ShouldBeNull();
    }

    [Fact]
    public void Insert_CarriesExternalId()
    {
        var identity = RecordIdentity.Insert("EXT-1");

        identity.Operation.ShouldBe(RecordOperation.Insert);
        identity.ExternalId.ShouldBe("EXT-1");
    }

    [Fact]
    public void Update_HasUpdateOperationAndKey()
    {
        var key = new EntityKey("123456");

        var identity = RecordIdentity.Update(key, EntityKeyKind.BusinessKey);

        identity.Operation.ShouldBe(RecordOperation.Update);
        identity.Key.ShouldBe(key);
        identity.KeyKind.ShouldBe(EntityKeyKind.BusinessKey);
    }

    [Fact]
    public void Upsert_HasUpsertOperationAndKey()
    {
        var key = new EntityKey("Alice", "alice@test.com");

        var identity = RecordIdentity.Upsert(key, EntityKeyKind.BusinessKey);

        identity.Operation.ShouldBe(RecordOperation.Upsert);
        identity.Key.ShouldBe(key);
    }

    [Fact]
    public void Skip_CarriesReasonCodes()
    {
        var identity = RecordIdentity.Skip(IdentityReasonCodes.DuplicateKeyInFile);

        identity.Operation.ShouldBe(RecordOperation.Skip);
        identity.ReasonCodes.ShouldContain(IdentityReasonCodes.DuplicateKeyInFile);
    }

    [Fact]
    public void Ambiguous_CarriesReasonCodes()
    {
        var identity = RecordIdentity.Ambiguous(IdentityReasonCodes.MissingKeyComponent);

        identity.Operation.ShouldBe(RecordOperation.Ambiguous);
        identity.ReasonCodes.ShouldContain(IdentityReasonCodes.MissingKeyComponent);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var key = new EntityKey("123456");

        var a = RecordIdentity.Update(key, EntityKeyKind.BusinessKey);
        var b = RecordIdentity.Update(key, EntityKeyKind.BusinessKey);

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentOperations_AreNotEqual()
    {
        var a = RecordIdentity.Insert();
        var b = RecordIdentity.Update(new EntityKey("1"), EntityKeyKind.BusinessKey);

        a.ShouldNotBe(b);
    }
}
