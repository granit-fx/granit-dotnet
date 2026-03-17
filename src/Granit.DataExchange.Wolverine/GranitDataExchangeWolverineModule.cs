using Granit.Core.Modularity;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DataExchange.Wolverine;

/// <summary>
/// Granit module that replaces the default Channel-based dispatchers with
/// Outbox-backed <c>IMessageBus</c> implementations for durable import/export dispatch.
/// </summary>
/// <remarks>
/// <para>
/// This module depends on <see cref="GranitDataExchangeModule"/> (core data exchange services)
/// and <see cref="GranitWolverineModule"/> (Wolverine infrastructure).
/// </para>
/// <para>
/// When loaded, it replaces both <see cref="IImportCommandDispatcher"/> and
/// <see cref="IExportCommandDispatcher"/> with Wolverine-backed implementations
/// and registers the corresponding Wolverine handlers.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitDataExchangeModule),
    typeof(GranitWolverineModule))]
public sealed class GranitDataExchangeWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace the default Channel-based dispatchers with Wolverine's IMessageBus.
        context.Services.Replace(ServiceDescriptor
            .Singleton<IImportCommandDispatcher, WolverineImportCommandDispatcher>());
        context.Services.Replace(ServiceDescriptor
            .Singleton<IExportCommandDispatcher, WolverineExportCommandDispatcher>());
    }
}
