using System.Reflection;
using FluentValidation;
using Granit.Commands;
using Granit.Diagnostics;
using Granit.Modularity;
using Granit.Users;
using Granit.Wolverine.Behaviors;
using Granit.Wolverine.Diagnostics;
using Granit.Wolverine.Internal;
using Granit.Wolverine.Middleware;
using Granit.Wolverine.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;

namespace Granit.Wolverine.Extensions;

/// <summary>
/// Extension methods for registering Granit Wolverine core services.
/// </summary>
public static class WolverineHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit Wolverine core infrastructure (provider-agnostic).
    /// </summary>
    /// <remarks>
    /// Configures Wolverine with:
    /// <list type="bullet">
    ///   <item>Local routing for <see cref="Granit.Events.IDomainEvent"/> — never routed to external transports.</item>
    ///   <item>FluentValidation bus middleware — validates messages before handler execution via <see cref="RegistrationBehavior.DiscoverAndRegisterValidators"/>.</item>
    ///   <item>DLQ policy — <see cref="FluentValidation.ValidationException"/> is moved to the error queue immediately (no retry, deterministic failure).</item>
    ///   <item>Retry policy from <see cref="WolverineMessagingOptions"/> (default: 5 s / 30 s / 5 min) for all other exceptions.</item>
    ///   <item><see cref="OutgoingContextMiddleware"/> — injects <c>X-Tenant-Id</c> / <c>X-User-Id</c> / <c>traceparent</c> into outgoing envelopes.</item>
    ///   <item><see cref="TenantContextBehavior"/> — restores <c>ICurrentTenant</c> in background handlers.</item>
    ///   <item><see cref="UserContextBehavior"/> — restores <c>ICurrentUserService</c> in background handlers.</item>
    ///   <item><see cref="TraceContextBehavior"/> — restores W3C Trace Context in background handlers, linking Outbox spans to the originating HTTP request trace (source: <see cref="WolverineActivitySource.Name"/>).</item>
    ///   <item>No Outbox — add a provider module (e.g., <c>AddGranitWolverineWithPostgresql()</c>).</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="moduleAssemblies">
    /// Assemblies of all loaded Granit modules (from <see cref="Granit.Modularity.ServiceConfigurationContext.ModuleAssemblies"/>).
    /// Wolverine will scan these for handler methods. When <see langword="null"/>, falls back to
    /// <c>[assembly: WolverineHandlerModule]</c> discovery only.
    /// </param>
    /// <param name="configure">Optional additional Wolverine configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitWolverine(
        this IHostApplicationBuilder builder,
        IReadOnlyList<Assembly>? moduleAssemblies = null,
        Action<WolverineOptions>? configure = null)
    {
        GranitActivitySourceRegistry.Register(Diagnostics.WolverineActivitySource.Name);

        // Metrics: IMeterFactory-backed counters for message throughput, retries, claim checks.
        builder.Services.TryAddSingleton<WolverineMetrics>();

        // Shared helper for Singleton services that need to dispatch via scoped IMessageBus.
        builder.Services.TryAddSingleton<WolverineScopedSender>();

        // ICommandSender — generic command dispatch abstraction. Replaces per-module dispatcher
        // interfaces (IPaymentCommandDispatcher, IInvoiceCommandPublisher, etc.) with a single
        // provider-agnostic contract in the core Granit assembly. Named ICommandSender to
        // avoid collision with Wolverine.ICommandBus.
        builder.Services.TryAddScoped<ICommandSender, WolverineCommandSender>();

        // Bind and validate options at startup via DI.
        builder.Services
            .AddOptions<WolverineMessagingOptions>()
            .BindConfiguration(WolverineMessagingOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WolverineMessagingOptions>,
            WolverineMessagingOptionsValidator>();

        // Read options directly from IConfiguration: the DI container is not yet
        // built at this point, so IOptions<> is not resolvable inside UseWolverine().
        WolverineMessagingOptions messagingOptions = new();
        builder.Configuration
            .GetSection(WolverineMessagingOptions.SectionName)
            .Bind(messagingOptions);

        // WolverineCurrentUserService: AsyncLocal override + IHttpContextAccessor fallback.
        // Registered as the ICurrentUserService for Wolverine — restores user identity in
        // background handlers so EF Core audit interceptors record the correct ModifiedBy.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<WolverineCurrentUserService>();
        builder.Services.AddScoped<ICurrentUserService>(
            sp => sp.GetRequiredService<WolverineCurrentUserService>());
        builder.Services.AddScoped<IWolverineUserContextSetter>(
            sp => sp.GetRequiredService<WolverineCurrentUserService>());

        // FluentValidation — auto-discover validators from [WolverineHandlerModule] assemblies.
        // Modules without Wolverine handlers must still register manually.
        builder.Services.AddGranitValidatorsFromWolverineHandlerModules();

        builder.UseWolverine(opts =>
        {
            // Include all Granit module assemblies passed from the module system.
            // This is the primary discovery path — no timing dependency on DI registration.
            if (moduleAssemblies is not null)
            {
                foreach (Assembly assembly in moduleAssemblies)
                {
                    opts.Discovery.IncludeAssembly(assembly);
                }
            }

            // Also honor [assembly: WolverineHandlerModule] for non-Granit assemblies
            // (e.g. application handler modules that don't subclass GranitModule).
            foreach (Assembly handlerAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (handlerAssembly.GetCustomAttributes(typeof(global::Wolverine.Attributes.WolverineHandlerModuleAttribute), false).Length > 0)
                {
                    opts.Discovery.IncludeAssembly(handlerAssembly);
                }
            }

            // Always include the entry assembly so application handlers are discovered
            // without requiring [assembly: WolverineHandlerModule] in application code.
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly is not null)
            {
                opts.Discovery.IncludeAssembly(entryAssembly);
            }

            // IDomainEvent — force local routing, never forward to external transports.
            // IIntegrationEvent routing is configured by the provider package.
            opts.PublishMessage<Events.IDomainEvent>()
                .ToLocalQueue("domain-events");

            // FluentValidation bus middleware: validates incoming messages before handler
            // execution using IValidator<T> instances discovered from the DI container.
            opts.UseFluentValidation(RegistrationBehavior.DiscoverAndRegisterValidators);

            // DLQ policy for validation failures: deterministic failures must not be retried.
            // MUST be declared before OnAnyException() so it takes priority.
            opts.OnException<ValidationException>().MoveToErrorQueue();

            // Global retry policy — applied to all other unhandled exceptions.
            TimeSpan[] delays = messagingOptions.RetryDelays
                .Take(messagingOptions.MaxRetryAttempts)
                .ToArray();

            opts.OnAnyException().RetryWithCooldown(delays);

            // Context propagation middlewares — applied to all handler chains.
            opts.Policies.AddMiddleware<OutgoingContextMiddleware>();
            opts.Policies.AddMiddleware<TenantContextBehavior>();
            opts.Policies.AddMiddleware<UserContextBehavior>();
            opts.Policies.AddMiddleware<TraceContextBehavior>();

            configure?.Invoke(opts);

            // Expose the WolverineOptions instance for provider modules (PostgreSQL, SqlServer).
            // Provider modules call extension methods like PersistMessagesWithPostgresql()
            // directly on this instance during their ConfigureServices phase, when the
            // service collection is still writable. This avoids ConfigureWolverine() which
            // defers extension processing to DI resolution (Wolverine 3.0+ blocks service
            // modifications at that point).
            builder.Services.TryAddSingleton(new WolverineOptionsHolder(opts));
        });

        return builder;
    }
}
