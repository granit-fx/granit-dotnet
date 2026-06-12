using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.UserSessions.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.UserSessions.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core-backed durable session risk persistence. Replaces the in-memory default
/// <see cref="IUserSessionRiskStore"/> from <c>Granit.UserSessions.Abstractions</c>.
/// </summary>
/// <remarks>
/// Register the DbContext via <c>AddGranitUserSessionsEntityFrameworkCore(configure)</c>; this module wires the
/// store. Verdicts then survive restarts and are shared across instances.
/// </remarks>
[DependsOn(
    typeof(GranitUserSessionsAbstractionsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitUserSessionsEntityFrameworkCoreModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddScoped<IUserSessionRiskStore, EfCoreUserSessionRiskStore>();
}
