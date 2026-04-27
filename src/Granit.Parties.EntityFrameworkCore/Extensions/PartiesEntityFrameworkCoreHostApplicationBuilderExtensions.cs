using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Parties.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering EF Core persistence for Granit.Parties.</summary>
public static class PartiesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers the <c>PartiesDbContext</c> + <c>EfPartyStore</c> + queryable source.</summary>
    public static IHostApplicationBuilder AddGranitPartiesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<PartiesDbContext>(configure);

        builder.Services.AddScoped<EfPartyStore>();
        builder.Services.TryAddScoped<IPartyReader>(sp => sp.GetRequiredService<EfPartyStore>());
        builder.Services.TryAddScoped<IPartyWriter>(sp => sp.GetRequiredService<EfPartyStore>());
        builder.Services.TryAddScoped<IDefaultPartyResolver, EfDefaultPartyResolver>();
        builder.Services.TryAddScoped<IDefaultPartySeeder, EfDefaultPartySeeder>();

        builder.Services.AddScoped<IQueryableSource<Domain.Party>, EfPartyQueryableSource>();

        // Tier-1 deterministic duplicate detection — populate canonical projections on save.
        // Registered as both concrete + IGranitAutoInterceptor (pattern from
        // AuditingChangeTrackingInterceptor): the concrete registration lets unit tests
        // resolve it; IGranitAutoInterceptor wires it onto every Granit DbContext.
        builder.Services.AddScoped<PartyCanonicalisationInterceptor>();
        builder.Services.AddScoped<IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<PartyCanonicalisationInterceptor>());

        // Tier-2 / Tier-3 thresholds — bound from the "Granit:Parties:Deduplication"
        // configuration section so apps can tune per-environment without recompiling.
        // Defaults baked into the options class (NameSimilarity 0.7, CompanySimilarity 0.6)
        // apply when the section is absent. Consumed by the Tier-2 pg_trgm scan and the
        // Tier-3 fuzzy scorer (#1299).
        builder.Services.AddOptions<PartyDeduplicationOptions>()
            .Bind(builder.Configuration.GetSection(PartyDeduplicationOptions.SectionName));

        return builder;
    }
}
