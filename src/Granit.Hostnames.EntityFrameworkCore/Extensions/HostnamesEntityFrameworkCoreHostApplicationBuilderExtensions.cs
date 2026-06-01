using Granit.Hostnames.Contracts;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Hostnames.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit managed hostnames.
/// </summary>
public static class HostnamesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit managed hostnames.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <c>HostnamesDbContext</c>, <c>EfManagedHostnameStore</c> (as both
    /// <see cref="IManagedHostnameReader"/> and <see cref="IManagedHostnameWriter"/>),
    /// and <c>EfHostnameResolver</c> as <see cref="IHostnameResolver"/>.
    /// </para>
    /// <para>
    /// Must be called after <c>AddGranitHostnames()</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core options configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitHostnamesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<HostnamesDbContext>(configure);

        builder.Services.TryAddScoped<EfManagedHostnameStore>();
        builder.Services.TryAddScoped<IManagedHostnameReader>(
            sp => sp.GetRequiredService<EfManagedHostnameStore>());
        builder.Services.TryAddScoped<IManagedHostnameWriter>(
            sp => sp.GetRequiredService<EfManagedHostnameStore>());

        builder.Services.TryAddScoped<IHostnameResolver, EfHostnameResolver>();

        return builder;
    }
}
