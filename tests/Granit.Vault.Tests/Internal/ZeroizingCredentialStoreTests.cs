using Granit.Vault.Internal;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests.Internal;

public class ZeroizingCredentialStoreTests
{
    [Fact]
    public void IsReady_IsFalseBeforeFirstApply()
    {
        var store = new ZeroizingCredentialStore();

        store.IsReady.ShouldBeFalse();
        store.Username.ShouldBe(string.Empty);
        store.Password.ShouldBe(string.Empty);
    }

    [Fact]
    public void Apply_ExposesCredentialsAndMarksReady()
    {
        var store = new ZeroizingCredentialStore();

        store.Apply("alice", "s3cr3t");

        store.IsReady.ShouldBeTrue();
        store.Username.ShouldBe("alice");
        store.Password.ShouldBe("s3cr3t");
    }

    [Fact]
    public void Apply_OnRotation_ReturnsLatestCredentials()
    {
        var store = new ZeroizingCredentialStore();

        store.Apply("v1-user", "v1-pass");
        store.Apply("v2-user", "v2-pass");

        store.Username.ShouldBe("v2-user");
        store.Password.ShouldBe("v2-pass");
    }

    [Fact]
    public void Apply_ThrowsWhenUsernameIsNull()
    {
        var store = new ZeroizingCredentialStore();

        Should.Throw<ArgumentNullException>(() => store.Apply(null!, "p"));
    }

    [Fact]
    public void Apply_ThrowsWhenPasswordIsNull()
    {
        var store = new ZeroizingCredentialStore();

        Should.Throw<ArgumentNullException>(() => store.Apply("u", null!));
    }

    [Fact]
    public void Apply_AcceptsEmptyStrings()
    {
        var store = new ZeroizingCredentialStore();

        store.Apply(string.Empty, string.Empty);

        store.IsReady.ShouldBeFalse();
        store.Username.ShouldBe(string.Empty);
        store.Password.ShouldBe(string.Empty);
    }

    [Fact]
    public void Username_DecodesUtf8()
    {
        var store = new ZeroizingCredentialStore();

        store.Apply("ÆgirOdin", "Pøss");

        store.Username.ShouldBe("ÆgirOdin");
        store.Password.ShouldBe("Pøss");
    }
}
