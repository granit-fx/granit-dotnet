using System.Threading.Channels;
using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Diagnostics;
using Granit.AuditLog.EntityFrameworkCore.Interceptors;
using Granit.AuditLog.EntityFrameworkCore.Internal;
using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Granit.AuditLog.Messages;
using Granit.AuditLog.Options;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.AuditLog.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit audit logging.
/// </summary>
public static class AuditLogEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers EF Core persistence for the Granit audit log module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the <see cref="AuditLogDbContext"/>, persistence workers,
    /// <see cref="IAuditLogReader"/>, <see cref="IAuditLogWriter"/>, and the
    /// <see cref="AuditLogChangeTrackingInterceptor"/>.
    /// </para>
    /// <para>
    /// The interceptor must be added to the host application's DbContext separately
    /// via <see cref="DbContextOptionsBuilderAuditLogExtensions.UseGranitAuditLogInterceptor"/>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAuditLogEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        // Isolated DbContext (no audit interceptor — prevents recursion).
        builder.Services.AddGranitDbContext<AuditLogDbContext>(configure);

        // Metrics.
        builder.Services.TryAddSingleton<AuditLogMetrics>();

        // Publisher: async (Channel) or strict (synchronous).
        builder.Services.AddSingleton(Channel.CreateUnbounded<AuditLogBatch>());
        builder.Services.AddScoped<ChannelAuditLogPublisher>();
        builder.Services.AddScoped<StrictAuditLogPublisher>();
        builder.Services.AddScoped<IAuditLogEntryPublisher>(sp =>
        {
            AuditLogOptions options = sp.GetRequiredService<IOptions<AuditLogOptions>>().Value;
            return options.PersistenceMode == AuditPersistenceMode.Strict
                ? sp.GetRequiredService<StrictAuditLogPublisher>()
                : sp.GetRequiredService<ChannelAuditLogPublisher>();
        });

        // Background workers.
        builder.Services.AddHostedService<AuditLogPersistenceWorker>();
        builder.Services.AddHostedService<AuditLogCleanupWorker>();

        // CQRS services.
        builder.Services.AddScoped<IAuditLogReader, EfCoreAuditLogReader>();
        builder.Services.AddScoped<IAuditLogWriter, EfCoreAuditLogWriter>();

        // Interceptor + capture service (scoped — host DbContext resolves from its SP).
        builder.Services.AddScoped<AuditLogChangeTrackingInterceptor>();
        builder.Services.AddScoped<ChangeTrackingCaptureService>();

        return builder;
    }
}
