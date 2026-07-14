using Granit.DataExchange.Import.Identity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Identity;

public sealed class EntityKeyTests
{
    [Fact]
    public void Equality_SameSingleComponent_AreEqual()
    {
        var a = new EntityKey("123456");
        var b = new EntityKey("123456");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentComponent_AreNotEqual()
    {
        var a = new EntityKey("123456");
        var b = new EntityKey("654321");

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_IsOrdinal_ForStrings()
    {
        var a = new EntityKey("Alice");
        var b = new EntityKey("alice");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_CompositeKey_ComparesAllComponents()
    {
        var a = new EntityKey("Alice", "alice@test.com");
        var b = new EntityKey("Alice", "alice@test.com");
        var c = new EntityKey("Alice", "different@test.com");

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact]
    public void Equality_DifferentComponentCount_AreNotEqual()
    {
        var a = new EntityKey("Alice");
        var b = new EntityKey("Alice", "alice@test.com");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_NullComponents_AreHandled()
    {
        var a = new EntityKey(null, "x");
        var b = new EntityKey(null, "x");
        var c = new EntityKey("y", "x");

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact]
    public void Equality_GuidComponent_Works()
    {
        var id = Guid.NewGuid();
        var a = new EntityKey(id);
        var b = new EntityKey(id);
        var c = new EntityKey(Guid.NewGuid());

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    [Fact]
    public void ToString_IncludesComponents()
    {
        var key = new EntityKey("Alice", null);

        string text = key.ToString();

        text.ShouldContain("Alice");
        text.ShouldContain("null");
    }

    [Fact]
    public void UsableAsDictionaryKey()
    {
        Dictionary<EntityKey, string> map = [];
        var key = new EntityKey("123456");
        map[key] = "value";

        map.TryGetValue(new EntityKey("123456"), out string? value).ShouldBeTrue();
        value.ShouldBe("value");
    }
}
