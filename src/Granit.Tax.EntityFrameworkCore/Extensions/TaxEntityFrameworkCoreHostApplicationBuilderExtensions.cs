using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Tax.EntityFrameworkCore.Internal;
using Granit.Timing;
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
        builder.Services.AddHostInternalDbContextEnsurer<TaxDbContext>();

        builder.Services.AddScoped<EfValidatedTaxIdStore>();
        builder.Services.TryAddScoped<IValidatedTaxIdReader>(sp => sp.GetRequiredService<EfValidatedTaxIdStore>());
        builder.Services.TryAddScoped<IValidatedTaxIdWriter>(sp => sp.GetRequiredService<EfValidatedTaxIdStore>());

        // EfTaxRateProvider decorates the config-based provider.
        // Capture the current ITaxRateProvider descriptor before replacing it
        // to avoid circular DI (EfTaxRateProvider depends on ITaxRateProvider as fallback).
        ServiceDescriptor? existingDescriptor = builder.Services
            .FirstOrDefault(d => d.ServiceType == typeof(ITaxRateProvider));

        builder.Services.Replace(ServiceDescriptor.Scoped<ITaxRateProvider>(sp =>
        {
            ITaxRateProvider fallback = existingDescriptor switch
            {
                { ImplementationFactory: { } factory } => (ITaxRateProvider)factory(sp),
                { ImplementationType: { } type } => (ITaxRateProvider)ActivatorUtilities.CreateInstance(sp, type),
                { ImplementationInstance: { } instance } => (ITaxRateProvider)instance,
                _ => throw new InvalidOperationException(
                    "No ITaxRateProvider fallback registered. Register Granit.Tax.Internal or a custom provider before calling AddGranitTaxEntityFrameworkCore."),
            };

            return new EfTaxRateProvider(
                sp.GetRequiredService<IDbContextFactory<TaxDbContext>>(),
                fallback,
                sp.GetRequiredService<IClock>());
        }));

        return builder;
    }
}
