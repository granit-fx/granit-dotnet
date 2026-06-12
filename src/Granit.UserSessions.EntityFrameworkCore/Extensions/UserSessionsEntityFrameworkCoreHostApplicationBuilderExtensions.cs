using System.Diagnostics.CodeAnalysis;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.UserSessions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Granit.UserSessions.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for the session risk store.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class UserSessionsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the isolated <see cref="UserSessionRiskDbContext"/> for durable session risk persistence. The
    /// module (<see cref="GranitUserSessionsEntityFrameworkCoreModule"/>) registers the store itself.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitUserSessionsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddGranitDbContext<UserSessionRiskDbContext>(configure);

        return builder;
    }
}
