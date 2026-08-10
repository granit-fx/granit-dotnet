using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.EntityFrameworkCore.Internal;
using Granit.Http.Cookies.Ledger;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Http.Cookies.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for the Granit cookie-consent ledger.
/// </summary>
public static class CookiesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for the Granit cookie-consent ledger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the isolated <c>CookiesDbContext</c>, replaces the base module's no-op
    /// <c>NullConsentLedger</c> with the durable <see cref="EfCoreConsentLedger"/>, and wires
    /// the GDPR erasure primitive (<see cref="ICookieConsentEraser"/>) plus the queryable
    /// source for <c>MapGranitQuery&lt;CookieConsentRecord&gt;</c>.
    /// </para>
    /// <para>
    /// The framework ships no migrations — the application owns them (generate against a
    /// context mapping <c>ConfigureCookiesModule()</c>).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitCookiesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Isolated DbContext (scoped factory + Granit auto-interceptors).
        builder.Services.AddGranitDbContext<CookiesDbContext>(configure);

        // Durable ledger replaces the base module's NullConsentLedger. RemoveAll + Add is
        // deterministic in both wiring orders: registered first, the base TryAdd no-ops,
        // and registered second, the Null default is removed here.
        builder.Services.RemoveAll<IConsentLedger>();
        builder.Services.AddScoped<IConsentLedger, EfCoreConsentLedger>();

        // GDPR Art. 17 erasure primitive (see ICookieConsentEraser for the wiring contract).
        builder.Services.TryAddScoped<ICookieConsentEraser, EfCoreCookieConsentEraser>();

        // Queryable source for MapGranitQuery (host bypasses tenant filter for cross-tenant review).
        builder.Services.TryAddScoped<IQueryableSource<CookieConsentRecord>, EfCookieConsentRecordQueryableSource>();

        return builder;
    }
}
