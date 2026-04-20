using Granit.Modularity;
using Granit.MyModule.Extensions;

namespace Granit.MyModule;

/// <summary>
/// Granit module for MyModule.
/// </summary>
/// <remarks>
/// Follow ADR-022: keep domain modules free of messaging-infrastructure types. Choose
/// the abstraction by intent, not by technical capability:
/// <list type="bullet">
///   <item><c>Granit.Commands.ICommandSender</c> — CQRS command, single handler, domain intent.</item>
///   <item><c>Granit.Events.IDistributedEventBus</c> / <c>ILocalEventBus</c> — pub-sub fan-out for <c>*Event</c> / <c>*Eto</c>.</item>
///   <item><c>Granit.BackgroundJobs.Abstractions.IBackgroundJobDispatcher</c> — operational/system work, scheduled or recurring jobs.</item>
/// </list>
/// Place message handlers in <c>Handlers/</c> using the convention: public class + public
/// static <c>HandleAsync</c>. Wolverine (or any other provider) discovers them from the
/// module assembly — no <c>using Wolverine;</c> needed in this module.
/// </remarks>
public sealed class GranitMyModuleModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitMyModule();
}
