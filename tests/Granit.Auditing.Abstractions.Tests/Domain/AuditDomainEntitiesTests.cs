using Granit.Auditing.Attributes;
using Granit.Auditing.Domain;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Abstractions.Tests.Domain;

/// <summary>
/// Guards that audit domain entities carry [AuditIgnore] so they are skipped
/// by AuditingChangeTrackingInterceptor when AuditingDbContext saves them.
/// Without this attribute, saving an AuditEntry triggers the interceptor again,
/// which stages another AuditEntry through the persistence pipeline — infinite recursion.
/// </summary>
public sealed class AuditDomainEntitiesTests
{
    [Fact]
    public void AuditEntry_HasAuditIgnoreAttribute() =>
        typeof(AuditEntry).IsDefined(typeof(AuditIgnoreAttribute), inherit: false).ShouldBeTrue();

    [Fact]
    public void AuditEntityChange_HasAuditIgnoreAttribute() =>
        typeof(AuditEntityChange).IsDefined(typeof(AuditIgnoreAttribute), inherit: false).ShouldBeTrue();

    [Fact]
    public void AuditEntityChange_IsMultiTenant() =>
        typeof(IMultiTenant).IsAssignableFrom(typeof(AuditEntityChange)).ShouldBeTrue();

    [Fact]
    public void AuditPropertyChange_HasAuditIgnoreAttribute() =>
        typeof(AuditPropertyChange).IsDefined(typeof(AuditIgnoreAttribute), inherit: false).ShouldBeTrue();
}
