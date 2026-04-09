using System.Diagnostics.CodeAnalysis;
using Granit.AI.EntityFrameworkCore.Internal;
using Granit.AI.Workspaces;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.AI.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit AI.
/// </summary>
/// <remarks>Not dead code — called by host applications to wire EF Core persistence for the AI module.</remarks>
[ExcludeFromCodeCoverage]
public static class AIEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit AI (workspaces, usage tracking, audit).
    /// </summary>
    /// <remarks>
    /// Registers <see cref="AIDbContext"/> via <c>AddGranitDbContext</c> with interceptor DI,
    /// and replaces the null store implementations from <c>Granit.AI</c> with EF Core-backed stores.
    /// <para>
    /// <c>AuditedEntityInterceptor</c> and <c>SoftDeleteInterceptor</c> are added automatically.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<AIDbContext>(configure);
        builder.Services.AddHostInternalDbContextEnsurer<AIDbContext>();

        builder.Services.AddScoped<EfAIWorkspaceStore>();
        builder.Services.AddScoped<IAIWorkspaceStoreReader>(sp => sp.GetRequiredService<EfAIWorkspaceStore>());
        builder.Services.AddScoped<IAIWorkspaceStoreWriter>(sp => sp.GetRequiredService<EfAIWorkspaceStore>());

        builder.Services.AddScoped<IAIUsageTracker, EfAIUsageStore>();
        builder.Services.AddScoped<IAIUsageQueryableProvider, EfAIUsageQueryableProvider>();

        return builder;
    }
}
