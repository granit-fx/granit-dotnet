using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.MultiTenancy.Auditing;
using Granit.MultiTenancy.Authorization;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Auditing.Tests;

public sealed class AuditingHostImpersonationAuditWriterTests
{
    private static ClaimsPrincipal Principal(string sub = "host-1", string? name = "Host Operator") =>
        new(new ClaimsIdentity(
            name is null
                ? new[] { new Claim("sub", sub) }
                : new[] { new Claim("sub", sub), new Claim("name", name) },
            "test"));

    private static IClock FixedClock(DateTimeOffset at)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(at);
        return clock;
    }

    [Fact]
    public async Task WriteAsync_AllowedDecision_PersistsAuditEntry()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.Zero)));
        var tenantId = Guid.NewGuid();

        await sut.WriteAsync(
            Principal(), tenantId, HostImpersonationDecision.Allow,
            "HeaderTenantResolver", "10.0.0.1", "test-agent/1.0", "trace-abc",
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.UserId.ShouldBe("host-1");
        captured.UserName.ShouldBe("Host Operator");
        captured.TenantId.ShouldBe(tenantId);
        captured.Category.ShouldBe(AuditCategory.AccessDenied);
        captured.IpAddress.ShouldBe("10.0.0.1");
        captured.UserAgent.ShouldBe("test-agent/1.0");
        captured.CorrelationId.ShouldBe("trace-abc");
    }

    [Fact]
    public async Task WriteAsync_DeniedDecision_AlsoPersists()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(DateTimeOffset.UnixEpoch));

        await sut.WriteAsync(
            Principal(), Guid.NewGuid(),
            new HostImpersonationDecision(false, "HostImpersonation.PermissionDenied"),
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.Category == AuditCategory.AccessDenied),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_PrincipalWithoutSubClaim_FallsBackToNameIdentifier()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(DateTimeOffset.UnixEpoch));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "host-via-nameid")], "test"));

        await sut.WriteAsync(
            principal, Guid.NewGuid(), HostImpersonationDecision.Allow,
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        captured!.UserId.ShouldBe("host-via-nameid");
    }

    [Fact]
    public async Task WriteAsync_PrincipalWithNoIdentityClaims_FallsBackToUnknown()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(DateTimeOffset.UnixEpoch));

        await sut.WriteAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            Guid.NewGuid(), HostImpersonationDecision.Allow,
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        captured!.UserId.ShouldBe("unknown");
    }

    [Fact]
    public async Task WriteAsync_TimestampComesFromInjectedClock()
    {
        var at = new DateTimeOffset(2026, 5, 19, 12, 30, 0, TimeSpan.Zero);
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(at));

        await sut.WriteAsync(
            Principal(), Guid.NewGuid(), HostImpersonationDecision.Allow,
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        captured!.Timestamp.ShouldBe(at);
    }
}
