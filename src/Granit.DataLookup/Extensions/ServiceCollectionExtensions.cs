using Granit.DataLookup.Diagnostics;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace Granit.DataLookup.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DataLookup</c> runtime services and sources.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core data-lookup runtime (<see cref="ILookupRegistry"/>, metrics).
    /// Sources are added with <see cref="AddEnumLookup{TEnum}"/> or the adapter-specific
    /// extensions in other packages (e.g. Granit.DataLookup.EntityFrameworkCore).
    /// </summary>
    public static IServiceCollection AddGranitDataLookup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<DataLookupMetrics>();
        services.TryAddScoped<ILookupRegistry, LookupRegistry>();

        return services;
    }

    /// <summary>
    /// Registers an <see cref="EnumLookupSource{TEnum}"/> under <paramref name="name"/>.
    /// The source is resolved as scoped so the localizer honors the current request culture.
    /// </summary>
    /// <typeparam name="TEnum">The enum to expose.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Registry key (e.g. <c>"enum-aggregation-type"</c>).</param>
    /// <param name="requiredPermission">Optional permission required to invoke the source.</param>
    public static IServiceCollection AddEnumLookup<TEnum>(
        this IServiceCollection services,
        string name,
        string? requiredPermission = null)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.AddScoped<ILookupSource>(sp =>
        {
            IStringLocalizerFactory factory = sp.GetRequiredService<IStringLocalizerFactory>();
            IStringLocalizer localizer = factory.Create(typeof(TEnum));
            return new EnumLookupSource<TEnum>(name, localizer, requiredPermission);
        });

        return services;
    }
}
