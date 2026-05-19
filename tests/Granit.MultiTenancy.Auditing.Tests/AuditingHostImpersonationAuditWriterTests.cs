using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
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
                ? [new Claim("sub", sub)]
                : [new Claim("sub", sub), new Claim("name", name)],
            "test"));

    private static IClock FixedClock(DateTimeOffset at)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(at);
        return clock;
    }

    [Fact]
    public async Task WriteAsync_AllowedDecision_PersistsAsPrivilegedAccess()
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
        captured.Category.ShouldBe(AuditCategory.PrivilegedAccess);
        captured.IpAddress.ShouldBe("10.0.0.1");
        captured.UserAgent.ShouldBe("test-agent/1.0");
        captured.CorrelationId.ShouldBe("trace-abc");
    }

    [Fact]
    public async Task WriteAsync_DeniedDecision_PersistsAsAccessDenied()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(DateTimeOffset.UnixEpoch));

        await sut.WriteAsync(
            Principal(), Guid.NewGuid(),
            new HostImpersonationDecision(false, "HostImpersonation.PermissionDenied"),
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Category.ShouldBe(AuditCategory.AccessDenied);
    }

    [Fact]
    public async Task WriteAsync_PersistsDecisionDetailsAsEntityChange()
    {
        // The audit row must be self-contained — investigators read decision detail
        // straight off the row rather than joining against OTEL / structured logs.
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(DateTimeOffset.UnixEpoch));
        var tenantId = Guid.NewGuid();

        await sut.WriteAsync(
            Principal(), tenantId,
            new HostImpersonationDecision(false, "HostImpersonation.PermissionDenied"),
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.EntityChanges.Count.ShouldBe(1);
        AuditEntityChange change = captured.EntityChanges.Single();
        change.EntityType.ShouldBe("HostImpersonation");
        change.EntityId.ShouldBe(tenantId.ToString("D"));
        change.ChangeType.ShouldBe(AuditChangeType.Created);

        change.PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Allowed" && p.NewValue == "false");
        change.PropertyChanges.ShouldContain(p =>
            p.PropertyName == "ResolverType" && p.NewValue == "HeaderTenantResolver");
        change.PropertyChanges.ShouldContain(p =>
            p.PropertyName == "DenyReasonCode" && p.NewValue == "HostImpersonation.PermissionDenied");
    }

    [Fact]
    public async Task WriteAsync_AllowedDecision_OmitsDenyReasonCodeFromEntityChanges()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditEntry? captured = null;
        await writer.WriteAsync(Arg.Do<AuditEntry>(e => captured = e), Arg.Any<CancellationToken>());

        var sut = new AuditingHostImpersonationAuditWriter(writer, FixedClock(DateTimeOffset.UnixEpoch));

        await sut.WriteAsync(
            Principal(), Guid.NewGuid(), HostImpersonationDecision.Allow,
            "HeaderTenantResolver", null, null, null,
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        AuditEntityChange change = captured.EntityChanges.Single();
        change.PropertyChanges.ShouldContain(p =>
            p.PropertyName == "Allowed" && p.NewValue == "true");
        change.PropertyChanges.ShouldNotContain(p => p.PropertyName == "DenyReasonCode");
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
    public async Task WriteAsync_PrincipalWithNoIdentityClaims_FallsBackToSentinel()
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

        // `<unknown>` (with delimiters) is grep-friendly and unlikely to collide with
        // any real OIDC `sub` claim, avoiding an indexed-column hotspot.
        captured!.UserId.ShouldBe("<unknown>");
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
