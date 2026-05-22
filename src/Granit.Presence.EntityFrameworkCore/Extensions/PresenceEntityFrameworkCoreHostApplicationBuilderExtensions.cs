using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Presence.Abstractions;
using Granit.Presence.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Presence.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Presence.
/// </summary>
public static class PresenceEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default in-memory store with a durable EF Core implementation.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitPresence()</c>. Registers:
    /// <list type="bullet">
    ///   <item><see cref="Internal.PresenceDbContext"/> via <c>IDbContextFactory</c> for thread-safe usage.</item>
    ///   <item><see cref="Internal.EfPresenceStore"/> as <see cref="IPresenceStore"/>.</item>
    /// </list>
    /// </remarks>
    public static IHostApplicationBuilder AddGranitPresenceEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<PresenceDbContext>(configure);

        builder.Services.Replace(
            ServiceDescriptor.Singleton<IPresenceStore, EfPresenceStore>());

        return builder;
    }
}
