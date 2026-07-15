using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.OpenIddict.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Granit.Bundle.OpenIddict;

/// <summary>
/// Full-stack imperative wiring for the Granit OpenIddict authority.
/// </summary>
public static class OpenIddictHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the complete Granit OpenIddict authority: EF Core persistence + ASP.NET Core
    /// Identity (via <c>Granit.OpenIddict.EntityFrameworkCore</c>) and the OpenIddict server +
    /// validation pipeline (via <c>Granit.OpenIddict.Server</c>).
    /// </summary>
    /// <remarks>
    /// This bundle composes the two halves so a host gets the whole authority in one call. Hosts
    /// that want only the data layer (e.g. a migration job) can call
    /// <c>AddGranitOpenIddictEntityFrameworkCore</c> directly, and DPoP sender-constraining is
    /// opt-in via the <c>Granit.OpenIddict.Server.DPoP</c> package.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(cs)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddict(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Persistence + identity (no server dependency).
        builder.AddGranitOpenIddictEntityFrameworkCore(configure);

        // OpenIddict server + validation pipeline.
        builder.AddGranitOpenIddictServer();

        return builder;
    }
}
