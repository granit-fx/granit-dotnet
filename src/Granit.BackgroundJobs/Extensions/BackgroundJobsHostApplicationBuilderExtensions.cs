using System.Reflection;
using System.Threading.Channels;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Diagnostics;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Exports;
using Granit.BackgroundJobs.Internal;
using Granit.BackgroundJobs.Options;
using Granit.BackgroundJobs.Queries;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.BackgroundJobs.Extensions;

/// <summary>
/// Extension methods for registering Granit background jobs services.
/// </summary>
public static class BackgroundJobsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit background jobs infrastructure (provider-agnostic).
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, job dispatch uses an in-process <see cref="Channel{T}"/> with a
    /// <see cref="BackgroundService"/>-based scheduler. For durable, cluster-safe scheduling
    /// via Wolverine's Outbox and <c>SingularAgent</c>, add the
    /// <c>Granit.BackgroundJobs.Wolverine</c> package.
    /// </para>
    /// <para>
    /// Jobs declared in the entry assembly and in <paramref name="additionalAssemblies"/>
    /// are seeded into the store on startup. When composed through
    /// <c>GranitBackgroundJobsModule</c>, all loaded module assemblies are passed
    /// automatically — satellite <c>Granit.{Module}.BackgroundJobs</c> packages need
    /// no extra registration.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="additionalAssemblies">
    /// Additional assemblies to scan for <see cref="RecurringJobAttribute"/>.
    /// The entry assembly is always scanned automatically.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBackgroundJobs(
        this IHostApplicationBuilder builder,
        IEnumerable<Assembly>? additionalAssemblies = null)
    {
        GranitActivitySourceRegistry.Register(BackgroundJobsActivitySource.Name);

        builder.Services.TryAddSingleton<BackgroundJobsMetrics>();

        // Bind and validate options at startup.
        builder.Services
            .AddOptions<BackgroundJobsOptions>()
            .BindConfiguration(BackgroundJobsOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<BackgroundJobsOptions>,
            BackgroundJobsOptionsValidator>();

        // Register the InMemory store as the default. Granit.BackgroundJobs.EntityFrameworkCore
        // replaces this registration with the durable EfBackgroundJobStore.
        builder.Services.AddSingleton<InMemoryBackgroundJobStore>();
        builder.Services.AddSingleton<IBackgroundJobStoreReader>(sp => sp.GetRequiredService<InMemoryBackgroundJobStore>());
        builder.Services.AddSingleton<IBackgroundJobStoreWriter>(sp => sp.GetRequiredService<InMemoryBackgroundJobStore>());

        builder.Services.AddScoped<BackgroundJobManager>();
        builder.Services.AddScoped<IBackgroundJobReader>(sp => sp.GetRequiredService<BackgroundJobManager>());
        builder.Services.AddScoped<IBackgroundJobWriter>(sp => sp.GetRequiredService<BackgroundJobManager>());

        // Discover recurring jobs. Invalid cron expressions fail here — at startup — rather
        // than being silently skipped at scheduling time.
        IEnumerable<Assembly> scanAssemblies = new[] { Assembly.GetEntryAssembly()! }
            .Concat(additionalAssemblies ?? [])
            .Distinct();

        IReadOnlyList<RecurringJobRegistration> registrations =
            RecurringJobDiscovery.Discover(scanAssemblies);
        RecurringJobDiscovery.ValidateCronExpressions(registrations);

        // The seed service MUST be registered before the worker and the scheduler: hosted
        // services start sequentially in registration order, so the store is fully seeded
        // before the scheduler reads it. The store is resolved via a scope because
        // IBackgroundJobStoreWriter is Scoped when the EF Core provider is used.
        builder.Services.AddHostedService(sp =>
            new BackgroundJobsSeedService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                registrations));

        // In-process channel dispatch (default — replaced by Granit.BackgroundJobs.Wolverine)
        builder.Services.AddSingleton(Channel.CreateUnbounded<BackgroundJobEnvelope>());
        builder.Services.AddSingleton<IBackgroundJobDispatcher, ChannelBackgroundJobDispatcher>();
        builder.Services.AddSingleton<IDeadLetterQueueInspector, NullDeadLetterQueueInspector>();
        builder.Services.AddHostedService<BackgroundJobWorker>();
        builder.Services.AddHostedService<ChannelCronSchedulerService>();

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<BackgroundJobDefinition, BackgroundJobDefinitionQueryDefinition>();
        builder.Services.AddExportDefinition<BackgroundJobDefinition, BackgroundJobDefinitionExportDefinition>();

        return builder;
    }
}
