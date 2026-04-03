using Granit.Domain;
using Granit.Scheduling.Events;

namespace Granit.Scheduling.Domain;

/// <summary>
/// A one-shot action scheduled for execution at a specific future date.
/// </summary>
/// <remarks>
/// <para>
/// Unlike recurring background jobs (cron-based), a scheduled action executes
/// exactly once at <see cref="ExecuteAt"/> and transitions to a terminal status
/// (<see cref="ScheduledActionStatus.Executed"/>, <see cref="ScheduledActionStatus.Cancelled"/>,
/// or <see cref="ScheduledActionStatus.Failed"/>).
/// </para>
/// <para>
/// The typed payload is serialized as JSON in <see cref="PayloadJson"/> and delivered
/// to its Wolverine handler at execution time. Use <see cref="CorrelationId"/> to link
/// the action to a domain entity (e.g., <c>"subscription:{id}"</c>).
/// </para>
/// </remarks>
public sealed class ScheduledAction : AuditedAggregateRoot, IMultiTenant
{
    private const int MaxFailureReasonLength = 500;

    /// <summary>
    /// Private constructor for EF Core materialization.
    /// </summary>
    private ScheduledAction() { }

    /// <summary>
    /// Creates a new scheduled action in <see cref="ScheduledActionStatus.Pending"/> status.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="payloadType">The assembly-qualified CLR type name of the payload.</param>
    /// <param name="payloadJson">The JSON-serialized payload.</param>
    /// <param name="executeAt">The UTC date and time when the action should execute.</param>
    /// <param name="correlationId">Optional correlation identifier for domain entity linkage.</param>
    /// <returns>A new <see cref="ScheduledAction"/> in Pending status.</returns>
    public static ScheduledAction Create(
        Guid id,
        string payloadType,
        string payloadJson,
        DateTimeOffset executeAt,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        return new ScheduledAction
        {
            Id = id,
            PayloadType = payloadType,
            PayloadJson = payloadJson,
            ExecuteAt = executeAt,
            CorrelationId = correlationId,
            Status = ScheduledActionStatus.Pending,
        };
    }

    /// <summary>The assembly-qualified CLR type name of the payload.</summary>
    public string PayloadType { get; private set; } = string.Empty;

    /// <summary>The JSON-serialized payload delivered to the handler at execution time.</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>The UTC date and time when the action should execute.</summary>
    public DateTimeOffset ExecuteAt { get; private set; }

    /// <summary>
    /// Optional identifier linking this action to a domain entity
    /// (e.g., <c>"subscription:3fa85f64-5717-4562-b3fc-2c963f66afa6"</c>).
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>The current lifecycle status.</summary>
    public ScheduledActionStatus Status { get; private set; }

    /// <summary>The UTC timestamp when the action was executed (null if not yet executed).</summary>
    public DateTimeOffset? ExecutedAt { get; private set; }

    /// <summary>The user who cancelled the action (null if not cancelled).</summary>
    public string? CancelledBy { get; private set; }

    /// <summary>Truncated error message if the action failed (max 500 characters).</summary>
    public string? FailureReason { get; private set; }

    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>
    /// Cancels the scheduled action.
    /// </summary>
    /// <param name="cancelledBy">The user performing the cancellation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the action is not in <see cref="ScheduledActionStatus.Pending"/> status.
    /// </exception>
    public void Cancel(string? cancelledBy)
    {
        EnsurePending();
        Status = ScheduledActionStatus.Cancelled;
        CancelledBy = cancelledBy;
        AddDomainEvent(new ScheduledActionCancelledEvent(Id, CorrelationId, cancelledBy));
    }

    /// <summary>
    /// Reschedules the action to a new execution date.
    /// </summary>
    /// <param name="newExecuteAt">The new UTC execution date.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the action is not in <see cref="ScheduledActionStatus.Pending"/> status.
    /// </exception>
    public void Reschedule(DateTimeOffset newExecuteAt)
    {
        EnsurePending();
        ExecuteAt = newExecuteAt;
    }

    /// <summary>
    /// Marks the action as successfully executed.
    /// </summary>
    /// <param name="executedAt">The UTC timestamp of execution.</param>
    internal void MarkExecuted(DateTimeOffset executedAt)
    {
        EnsureProcessing();
        Status = ScheduledActionStatus.Executed;
        ExecutedAt = executedAt;
        AddDistributedEvent(new ScheduledActionExecutedEto(
            Id, PayloadType, CorrelationId, executedAt));
    }

    /// <summary>
    /// Marks the action as failed after exhausting retries.
    /// </summary>
    /// <param name="failureReason">The error message (truncated to 500 characters).</param>
    /// <param name="failedAt">The UTC timestamp of failure.</param>
    internal void MarkFailed(string failureReason, DateTimeOffset failedAt)
    {
        EnsureProcessing();
        Status = ScheduledActionStatus.Failed;
        FailureReason = failureReason.Length > MaxFailureReasonLength
            ? failureReason[..MaxFailureReasonLength]
            : failureReason;
        ExecutedAt = failedAt;
        AddDistributedEvent(new ScheduledActionFailedEto(
            Id, PayloadType, CorrelationId, FailureReason, failedAt));
    }

    private void EnsurePending()
    {
        if (Status != ScheduledActionStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Scheduled action '{Id}' is in '{Status}' status and cannot be modified. Only 'Pending' actions can be changed.");
        }
    }

    private void EnsureProcessing()
    {
        if (Status != ScheduledActionStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Scheduled action '{Id}' is in '{Status}' status. Only 'Processing' actions can be marked as executed or failed.");
        }
    }
}
