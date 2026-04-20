namespace Granit.BackgroundJobs.Abstractions;

/// <summary>
/// Dispatches background job messages for execution — infrastructure / operational work,
/// not domain commands.
/// </summary>
/// <remarks>
/// <para>
/// A background job is a <b>unit of system work</b>: periodic maintenance, deferred
/// cleanup, scheduled reporting, externally-triggered side effects. Jobs are typically
/// declared as <c>IBackgroundJob</c> records, may carry <c>[RecurringJob("cron", "name")]</c>,
/// and benefit from <see cref="ScheduleAsync"/> for delayed dispatch.
/// </para>
/// <para>
/// The default implementation writes to an in-process <see cref="System.Threading.Channels.Channel{T}"/>.
/// When <c>Granit.BackgroundJobs.Wolverine</c> is loaded, this is replaced with an
/// <c>IMessageBus</c>-backed implementation for durable, transactional dispatch.
/// </para>
/// <para>
/// <b>When to use what:</b>
/// <list type="bullet">
///   <item>
///     <see cref="IBackgroundJobDispatcher"/> — <b>operational / infrastructure work.</b>
///     One-off with audit headers, delayed via <see cref="ScheduleAsync"/>, or recurring via
///     <c>[RecurringJob]</c>. Weakly typed (<c>PublishAsync(object)</c>).
///   </item>
///   <item>
///     <c>Granit.Commands.ICommandSender</c> — <b>CQRS command, single handler, domain intent.</b>
///     Strongly typed (<c>SendAsync&lt;TCommand&gt;</c>). Example:
///     <c>sender.SendAsync(new InitiatePaymentCommand(...))</c> from an HTTP endpoint.
///   </item>
///   <item>
///     <c>Granit.Events.ILocalEventBus</c> / <c>Granit.Events.IDistributedEventBus</c> —
///     publish-subscribe fan-out. Use for <c>*Event</c> / <c>*Eto</c> types.
///   </item>
/// </list>
/// The technical implementation often overlaps (all three eventually call Wolverine's
/// <c>IMessageBus</c>), but the chosen abstraction documents the <i>intent</i> in the
/// code — a reader can tell at a glance whether a piece of code is part of the domain
/// flow, a publish-subscribe integration, or a system work unit.
/// </para>
/// </remarks>
public interface IBackgroundJobDispatcher
{
    /// <summary>
    /// Publishes a message for immediate processing.
    /// </summary>
    /// <param name="message">The job message instance.</param>
    /// <param name="headers">Optional headers (e.g. <c>X-Triggered-By</c> for audit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(object message, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a message for delayed processing at the specified time.
    /// </summary>
    /// <param name="message">The job message instance.</param>
    /// <param name="scheduledTime">The UTC time at which the message should be processed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ScheduleAsync(object message, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default);
}
