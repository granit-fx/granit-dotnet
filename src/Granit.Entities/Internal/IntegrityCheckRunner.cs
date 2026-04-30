using Granit.Entities.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Entities.Internal;

/// <summary>
/// Boot-time validator (story #1541) that walks every registered
/// <see cref="IEntityDefinitionDescriptor"/> and asserts each cited
/// <c>QueryDefinition</c> / <c>ExportDefinition</c> / <c>MetricDefinition</c> /
/// <c>DashboardDefinition</c> / <c>IWorkflowDefinition</c> CLR type is resolvable
/// through DI as one of the *registered* definitions.
/// </summary>
/// <remarks>
/// <para>
/// "Resolvable" here means: the cited concrete type appears as the runtime type of
/// at least one service registered in the host's <see cref="IServiceProvider"/>. This
/// is purposefully loose — we do not coerce a specific base interface, because
/// different definition families live in different abstractions packages with
/// different descriptor interfaces (`IQueryDefinitionDescriptor`,
/// `IDashboardDefinitionDescriptor`, etc.). The check verifies the citation
/// resolves to something; tighter validation lives in the family-specific
/// architecture tests.
/// </para>
/// <para>
/// Behavior on violation is governed by <see cref="EntitiesOptions.IntegrityCheck"/>:
/// <see cref="IntegrityCheckMode.Throw"/> (default) blocks host start;
/// <see cref="IntegrityCheckMode.Warn"/> logs a warning and continues;
/// <see cref="IntegrityCheckMode.Off"/> skips the check entirely.
/// </para>
/// </remarks>
internal sealed partial class IntegrityCheckRunner(
    IEntityDefinitionRegistry registry,
    IServiceProvider rootProvider,
    IOptions<EntitiesOptions> options,
    ILogger<IntegrityCheckRunner> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        EntitiesOptions opt = options.Value;
        if (opt.IntegrityCheck == IntegrityCheckMode.Off)
        {
            LogIntegrityCheckSkipped();
            return Task.CompletedTask;
        }

        // Snapshot the set of resolvable concrete types — for each "service" in DI
        // we record both the ServiceType and the ImplementationType (when available).
        // Cited types must appear in one of the two sets.
        HashSet<Type> resolvableTypes = SnapshotResolvableTypes(rootProvider);

        List<string> violations = [];

        foreach (IEntityDefinitionDescriptor definition in registry.All)
        {
            EntityDefinitionDescriptor descriptor = definition.Descriptor;

            CheckSingle(violations, definition.Name, "QueryDefinition", descriptor.QueryDefinitionType, resolvableTypes);
            CheckSingle(violations, definition.Name, "ExportDefinition", descriptor.ExportDefinitionType, resolvableTypes);
            CheckSingle(violations, definition.Name, "WorkflowDefinition", descriptor.WorkflowDefinitionType, resolvableTypes);

            foreach (Type metricType in descriptor.MetricDefinitionTypes)
            {
                CheckSingle(violations, definition.Name, "MetricDefinition", metricType, resolvableTypes);
            }

            foreach (Type dashboardType in descriptor.DashboardDefinitionTypes)
            {
                CheckSingle(violations, definition.Name, "DashboardDefinition", dashboardType, resolvableTypes);
            }
        }

        if (violations.Count == 0)
        {
            LogIntegrityCheckPassed(registry.All.Count);
            return Task.CompletedTask;
        }

        string detail = string.Join(Environment.NewLine, violations.Select(v => "  - " + v));

        if (opt.IntegrityCheck == IntegrityCheckMode.Throw)
        {
            throw new InvalidOperationException(
                "Granit.Entities integrity check failed — one or more EntityDefinition references could not be resolved through DI:"
                + Environment.NewLine + detail);
        }

        LogIntegrityCheckViolations(violations.Count, detail);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void CheckSingle(
        List<string> violations,
        string entityName,
        string family,
        Type? citedType,
        HashSet<Type> resolvableTypes)
    {
        if (citedType is null)
        {
            return;
        }

        if (!resolvableTypes.Contains(citedType))
        {
            violations.Add(
                $"Entity '{entityName}' references {family} '{citedType.FullName}' which is not registered in DI. "
                + $"Did you forget to call services.Add{family}<...>()?");
        }
    }

    private static HashSet<Type> SnapshotResolvableTypes(IServiceProvider rootProvider)
    {
        HashSet<Type> result = [];

        // The DI container itself doesn't expose its registrations. We walk the
        // descriptors registered in the root scope's IServiceCollection (when
        // available) — Microsoft.Extensions.DependencyInjection exposes this via
        // the engine, but not through a public API. The pragmatic alternative is
        // to require the IServiceCollection to be registered as a singleton at
        // setup time (we do this in EntitiesServiceCollectionExtensions).
        if (rootProvider.GetService<IServiceCollection>() is { } services)
        {
            foreach (ServiceDescriptor descriptor in services)
            {
                result.Add(descriptor.ServiceType);
                if (descriptor.ImplementationType is { } impl)
                {
                    result.Add(impl);
                }
                if (descriptor.ImplementationInstance is { } instance)
                {
                    result.Add(instance.GetType());
                }
            }
        }

        return result;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Granit.Entities integrity check skipped (mode = Off).")]
    private partial void LogIntegrityCheckSkipped();

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Granit.Entities integrity check passed for {EntityCount} entity definitions.")]
    private partial void LogIntegrityCheckPassed(int entityCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Granit.Entities integrity check found {ViolationCount} unresolved reference(s):\n{Detail}")]
    private partial void LogIntegrityCheckViolations(int violationCount, string detail);
}
