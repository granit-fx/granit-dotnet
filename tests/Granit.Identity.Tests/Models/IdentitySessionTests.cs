using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Models;

public sealed class IdentitySessionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow.AddHours(-2);
        DateTimeOffset lastAccess = DateTimeOffset.UtcNow;
        List<string> clients = ["app-1", "app-2"];

        var session = new IdentitySession(
            SessionId: "sess-1",
            IpAddress: "192.168.1.1",
            StartedAt: started,
            LastAccess: lastAccess,
            RememberMe: true,
            Clients: clients);

        session.SessionId.ShouldBe("sess-1");
        session.IpAddress.ShouldBe("192.168.1.1");
        session.StartedAt.ShouldBe(started);
        session.LastAccess.ShouldBe(lastAccess);
        session.RememberMe.ShouldBeTrue();
        session.Clients.ShouldBe(clients);
    }

    [Fact]
    public void Constructor_AllowsNullIpAddress()
    {
        var session = new IdentitySession(
            SessionId: "sess-2",
            IpAddress: null,
            StartedAt: DateTimeOffset.UtcNow,
            LastAccess: DateTimeOffset.UtcNow,
            RememberMe: false,
            Clients: []);

        session.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<string> clients = ["app-1"];
        var session1 = new IdentitySession("id", "1.2.3.4", now, now, false, clients);
        var session2 = new IdentitySession("id", "1.2.3.4", now, now, false, clients);

        session1.ShouldBe(session2);
    }

    [Fact]
    public void Equality_DifferentSessionId_AreNotEqual()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<string> clients = ["app-1"];
        var session1 = new IdentitySession("id-1", "1.2.3.4", now, now, false, clients);
        var session2 = new IdentitySession("id-2", "1.2.3.4", now, now, false, clients);

        session1.ShouldNotBe(session2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var original = new IdentitySession("id", "1.2.3.4", now, now, false, []);

        IdentitySession modified = original with { RememberMe = true };

        modified.RememberMe.ShouldBeTrue();
        original.RememberMe.ShouldBeFalse();
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var session = new IdentitySession("sess-abc", "1.2.3.4", now, now, false, []);

        string str = session.ToString();

        str.ShouldContain("IdentitySession");
        str.ShouldContain("sess-abc");
    }
}
