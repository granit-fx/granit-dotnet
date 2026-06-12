using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.UserSessions.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.UserSessions.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the durable session risk store.
/// </summary>
/// <remarks>
/// <para>
/// The single entity (<see cref="UserSessionRiskEntity"/>) is intentionally <b>not</b>
/// <see cref="Granit.Domain.IMultiTenant"/>: a verdict is keyed by <c>(UserId, SessionId)</c> and is global,
/// not tenant-scoped. The Granit convention (no tenant entity → inherit <see cref="DbContext"/> directly) exists
/// to avoid a closure-captured tenant constant leaking across requests through the tenant query filter. That
/// hazard cannot arise here — with no <see cref="Granit.Domain.IMultiTenant"/> entity there is no tenant filter
/// to parameterise — so inheriting
/// <see cref="GranitDbContext"/> is a deliberate, harmless choice kept for one reason only:
/// </para>
/// <para>
/// it applies <c>ApplyGranitConventions</c> (with <c>currentTenant: null</c>, identical to what a direct call
/// would pass) so the <see cref="UserSessionRiskEntity.Level"/> enum persists as its PascalCase string in a
/// <c>varchar</c> column per the framework's enum-persistence convention, plus the standard soft-delete/active
/// conventions, without this context having to wire them by hand.
/// </para>
/// </remarks>
internal sealed class UserSessionRiskDbContext(
    DbContextOptions<UserSessionRiskDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<UserSessionRiskEntity> UserSessionRisks => Set<UserSessionRiskEntity>();

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureUserSessionsModule();
}
