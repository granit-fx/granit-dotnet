using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Settings.EntityFrameworkCore.Internal;
using Granit.Settings.EntityFrameworkCore.Options;
using Granit.Settings.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Settings.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit settings.
/// </summary>
public static class SettingsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default <c>InMemorySettingStore</c> with the EF Core implementation
    /// backed by the dedicated <c>SettingsDbContext</c>.
    /// </summary>
    /// <remarks>
    /// Migration ownership: the host runs migrations against its own
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> by folding the model via
    /// <see cref="ModelBuilderExtensions.ConfigureSettingsModule"/> — per the standard
    /// Granit convention (framework packages NEVER ship EF migrations).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for
    /// <see cref="SettingsEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or
    /// <paramref name="configure"/> is <c>null</c>.</exception>
    public static IHostApplicationBuilder AddGranitSettingsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<SettingsEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        SettingsEntityFrameworkCoreOptions options = new();
        configure(options);

        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "SettingsEntityFrameworkCoreOptions.Configure must be set. " +
                "Provide an Action<DbContextOptionsBuilder> that configures the EF Core " +
                "provider and connection string for SettingsDbContext.");
        }

        builder.Services.AddGranitDbContext<SettingsDbContext>(options.Configure);

        builder.Services.Replace(ServiceDescriptor.Scoped<ISettingStoreReader, EfCoreSettingStore>());
        builder.Services.Replace(ServiceDescriptor.Scoped<ISettingStoreWriter, EfCoreSettingStore>());

        return builder;
    }
}
