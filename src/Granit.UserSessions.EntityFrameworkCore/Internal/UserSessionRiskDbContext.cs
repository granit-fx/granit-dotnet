using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.UserSessions.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.UserSessions.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext for the durable session risk store. The single entity is not multi-tenant (verdicts are
/// keyed by user and session), but the context inherits <see cref="GranitDbContext"/> for the standard Granit
/// conventions.
/// </summary>
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
