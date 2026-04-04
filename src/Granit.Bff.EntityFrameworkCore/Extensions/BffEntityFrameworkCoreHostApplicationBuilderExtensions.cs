using System.Diagnostics.CodeAnalysis;
using Granit.Bff.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Granit.Bff.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit BFF sessions.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class BffEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="BffDbContext"/> for EF Core-backed BFF session persistence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method to register the isolated <see cref="BffDbContext"/> when using
    /// EF Core as the BFF session store instead of <c>IDistributedCache</c>.
    /// </para>
    /// <para>
    /// The module (<see cref="GranitBffEntityFrameworkCoreModule"/>) registers
    /// <c>IBffTokenStore</c> automatically — this method only handles the DbContext.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBffEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<BffDbContext>(configure);
        builder.Services.AddInternalDbContextEnsurer<BffDbContext>();

        return builder;
    }
}
