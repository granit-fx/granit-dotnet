namespace Granit.Commands;

/// <summary>
/// Sends CQRS commands to their single handler within the application.
/// </summary>
/// <remarks>
/// <para>
/// Commands represent a <b>domain intent to change state</b> and are handled by exactly
/// one handler. Use <see cref="ICommandSender"/> wherever a module previously injected a
/// domain-specific dispatcher interface (e.g. <c>IPaymentCommandDispatcher</c>). Commands
/// supported by a module live in <c>Granit.{Module}.Commands</c>.
/// </para>
/// <para>
/// <b>Default provider:</b> none — implementations come from a messaging provider module.
/// <b>Wolverine provider:</b> <c>Granit.Wolverine</c> registers a bridge over <c>IMessageBus</c>.
/// </para>
/// <para>
/// <b>When to use what:</b>
/// <list type="bullet">
///   <item>
///     <see cref="ICommandSender"/> — <b>CQRS command, single handler, domain intent.</b>
///     Strongly typed (<c>SendAsync&lt;TCommand&gt;</c>). Example:
///     <c>sender.SendAsync(new InitiatePaymentCommand(...))</c> from an HTTP endpoint or
///     orchestrator.
///   </item>
///   <item>
///     <see cref="Granit.Events.ILocalEventBus"/> / <see cref="Granit.Events.IDistributedEventBus"/>
///     — publish an event to zero-or-more subscribers (fan-out). Use for
///     <c>*Event</c> / <c>*Eto</c> types, never for commands.
///   </item>
///   <item>
///     <c>Granit.BackgroundJobs.Abstractions.IBackgroundJobDispatcher</c> —
///     <b>operational / infrastructure work</b>: one-off jobs with audit headers, delayed
///     dispatch via <c>ScheduleAsync</c>, or recurring jobs declared with
///     <c>[RecurringJob("cron", "name")]</c>. Example: <c>OrphanBlobCleanupJob</c>.
///     Weakly typed (<c>PublishAsync(object)</c>).
///   </item>
/// </list>
/// The technical implementation often overlaps (all three eventually call Wolverine's
/// <c>IMessageBus</c>), but the chosen abstraction documents the <i>intent</i> in the
/// code: a reader can tell at a glance whether a piece of code is part of the domain
/// flow, a publish-subscribe integration, or a system work unit.
/// </para>
/// </remarks>
public interface ICommandSender
{
    /// <summary>
    /// Sends a command to its registered handler.
    /// </summary>
    /// <typeparam name="TCommand">The command type (any class).</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class;
}
