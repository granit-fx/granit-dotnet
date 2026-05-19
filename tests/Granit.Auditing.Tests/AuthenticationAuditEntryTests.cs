using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests;

public sealed class AuthenticationAuditEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateSuccess_PopulatesEntryAsPrivilegedAccess()
    {
        var tenantId = Guid.NewGuid();

        AuditEntry entry = AuthenticationAuditEntry.CreateSuccess(
            Now,
            userId: "user-42",
            userName: "Jane Doe",
            method: "password",
            tenantId: tenantId,
            ipAddress: "10.0.0.1",
            userAgent: "Mozilla/5.0",
            correlationId: "trace-abc");

        entry.Category.ShouldBe(AuditCategory.PrivilegedAccess);
        entry.Timestamp.ShouldBe(Now);
        entry.UserId.ShouldBe("user-42");
        entry.UserName.ShouldBe("Jane Doe");
        entry.TenantId.ShouldBe(tenantId);
        entry.IpAddress.ShouldBe("10.0.0.1");
        entry.UserAgent.ShouldBe("Mozilla/5.0");
        entry.CorrelationId.ShouldBe("trace-abc");

        entry.EntityChanges.ShouldHaveSingleItem();
        AuditEntityChange change = entry.EntityChanges.Single();
        change.EntityType.ShouldBe(AuthenticationAuditEntry.SyntheticEntityType);
        change.EntityId.ShouldBe("user-42");
        change.ChangeType.ShouldBe(AuditChangeType.Created);

        change.PropertyChanges
            .Select(p => p.PropertyName)
            .ShouldBe(["Method", "Outcome"], ignoreOrder: true);
        change.PropertyChanges.First(p => p.PropertyName == "Method").NewValue.ShouldBe("password");
        change.PropertyChanges.First(p => p.PropertyName == "Outcome").NewValue.ShouldBe("success");
    }

    [Fact]
    public void CreateFailure_PopulatesEntryAsAccessDenied_WithReason()
    {
        AuditEntry entry = AuthenticationAuditEntry.CreateFailure(
            Now,
            userId: "user-42",
            userName: null,
            method: "password",
            reason: "invalid_credentials",
            tenantId: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null);

        entry.Category.ShouldBe(AuditCategory.AccessDenied);
        entry.UserId.ShouldBe("user-42");
        entry.UserName.ShouldBeNull();
        entry.TenantId.ShouldBeNull();

        AuditEntityChange change = entry.EntityChanges.ShouldHaveSingleItem();
        change.EntityId.ShouldBe("user-42");

        change.PropertyChanges
            .Select(p => p.PropertyName)
            .ShouldBe(["Method", "Outcome", "Reason"], ignoreOrder: true);
        change.PropertyChanges.First(p => p.PropertyName == "Outcome").NewValue.ShouldBe("failure");
        change.PropertyChanges.First(p => p.PropertyName == "Reason").NewValue.ShouldBe("invalid_credentials");
    }

    [Fact]
    public void CreateFailure_WithNullUserId_FallsBackToUnknownSentinel()
    {
        AuditEntry entry = AuthenticationAuditEntry.CreateFailure(
            Now,
            userId: null,
            userName: null,
            method: "password",
            reason: "user_not_found",
            tenantId: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null);

        entry.UserId.ShouldBe(AuthenticationAuditEntry.UnknownUserSentinel);
        entry.EntityChanges.Single().EntityId.ShouldBe(AuthenticationAuditEntry.UnknownUserSentinel);
    }

    [Fact]
    public void CreateFailure_WithEmptyUserId_FallsBackToUnknownSentinel()
    {
        AuditEntry entry = AuthenticationAuditEntry.CreateFailure(
            Now,
            userId: string.Empty,
            userName: null,
            method: "oidc",
            reason: "issuer_mismatch",
            tenantId: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null);

        entry.UserId.ShouldBe(AuthenticationAuditEntry.UnknownUserSentinel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateSuccess_RejectsMissingUserId(string? userId) =>
        Should.Throw<ArgumentException>(() => AuthenticationAuditEntry.CreateSuccess(
            Now, userId!, userName: null, method: "password",
            tenantId: null, ipAddress: null, userAgent: null, correlationId: null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateSuccess_RejectsMissingMethod(string? method) =>
        Should.Throw<ArgumentException>(() => AuthenticationAuditEntry.CreateSuccess(
            Now, userId: "u", userName: null, method: method!,
            tenantId: null, ipAddress: null, userAgent: null, correlationId: null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateFailure_RejectsMissingReason(string? reason) =>
        Should.Throw<ArgumentException>(() => AuthenticationAuditEntry.CreateFailure(
            Now, userId: "u", userName: null, method: "password", reason: reason!,
            tenantId: null, ipAddress: null, userAgent: null, correlationId: null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateFailure_RejectsMissingMethod(string? method) =>
        Should.Throw<ArgumentException>(() => AuthenticationAuditEntry.CreateFailure(
            Now, userId: "u", userName: null, method: method!, reason: "x",
            tenantId: null, ipAddress: null, userAgent: null, correlationId: null));
}
