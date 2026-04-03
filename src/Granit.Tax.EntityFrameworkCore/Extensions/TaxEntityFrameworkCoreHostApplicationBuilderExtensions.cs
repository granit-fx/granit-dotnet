using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Tax.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Tax.EntityFrameworkCore.Extensions;

/// <summary>Extension methods for registering Tax EF Core persistence.</summary>
public static class TaxEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit.Tax.</summary>
    public static IHostApplicationBuilder AddGranitTaxEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<TaxDbContext>(configure);
        builder.Services.AddInternalDbContextEnsurer<TaxDbContext>();

        builder.Services.AddScoped<EfValidatedTaxIdStore>();
        builder.Services.TryAddScoped<IValidatedTaxIdReader>(sp => sp.GetRequiredService<EfValidatedTaxIdStore>());
        builder.Services.TryAddScoped<IValidatedTaxIdWriter>(sp => sp.GetRequiredService<EfValidatedTaxIdStore>());

        // EfTaxRateProvider decorates the config-based provider
        builder.Services.AddScoped<EfTaxRateProvider>();
        builder.Services.Replace(ServiceDescriptor.Scoped<ITaxRateProvider>(sp =>
            sp.GetRequiredService<EfTaxRateProvider>()));

        return builder;
    }
}
