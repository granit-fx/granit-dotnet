using System.Text.Json;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Scheduling.Diagnostics;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Users;
using Wolverine;

namespace Granit.Scheduling.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IScheduler"/>.
/// Persists a <see cref="ScheduledAction"/> and schedules the payload
/// for future delivery via <see cref="IMessageBus.ScheduleAsync{T}"/>.
/// </summary>
internal sealed class WolverineScheduler(
    IMessageBus messageBus,
    IScheduledActionReader actionReader,
    IScheduledActionWriter actionWriter,
    IGuidGenerator guidGenerator,
    ScheduledPayloadTypeRegistry payloadTypeRegistry,
    SchedulingMetrics metrics,
    ICurrentUserService currentUserService,
    ICurrentTenant currentTenant) : IScheduler
{
    /// <inheritdoc/>
    public async Task<ScheduledActionId> ScheduleAsync<TPayload>(
        TPayload payload,
        DateTimeOffset executeAt,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        where TPayload : IScheduledPayload
    {
        ArgumentNullException.ThrowIfNull(payload);

        Guid id = guidGenerator.Create();
        string payloadType = typeof(TPayload).AssemblyQualifiedName!;
        string payloadJson = JsonSerializer.Serialize(payload);

        var action = ScheduledAction.Create(id, payloadType, payloadJson, executeAt, correlationId);

        await actionWriter.AddAsync(action, cancellationToken).ConfigureAwait(false);

        var options = new DeliveryOptions { ScheduledTime = executeAt };
        options.Headers[ScheduledActionStatusMiddleware.ActionIdHeader] = id.ToString();
        await messageBus.PublishAsync(payload, options).ConfigureAwait(false);

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordScheduled(tenantId, typeof(TPayload).Name);

        return ScheduledActionId.Create(id);
    }

    /// <inheritdoc/>
    public async Task CancelAsync(
        ScheduledActionId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        ScheduledAction action = await GetRequiredActionAsync(id, cancellationToken).ConfigureAwait(false);
        action.Cancel(cancelledBy: currentUserService.UserName);
        await actionWriter.UpdateAsync(action, cancellationToken).ConfigureAwait(false);

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordCancelled(tenantId);
    }

    /// <inheritdoc/>
    public async Task RescheduleAsync(
        ScheduledActionId id,
        DateTimeOffset newExecuteAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        ScheduledAction action = await GetRequiredActionAsync(id, cancellationToken).ConfigureAwait(false);
        action.Reschedule(newExecuteAt);
        await actionWriter.UpdateAsync(action, cancellationToken).ConfigureAwait(false);

        // Re-dispatch with new schedule. The original Wolverine message will be a no-op
        // if it fires (the middleware checks status before executing).
        Type payloadType = payloadTypeRegistry.Resolve(action.PayloadType);
        object payload = JsonSerializer.Deserialize(action.PayloadJson, payloadType)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for action '{id.Value}' (type: '{action.PayloadType}').");

        var options = new DeliveryOptions { ScheduledTime = newExecuteAt };
        options.Headers[ScheduledActionStatusMiddleware.ActionIdHeader] = action.Id.ToString();
        await messageBus.PublishAsync(payload, options).ConfigureAwait(false);

        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        metrics.RecordRescheduled(tenantId);
    }

    private async Task<ScheduledAction> GetRequiredActionAsync(
        ScheduledActionId id,
        CancellationToken cancellationToken)
    {
        ScheduledAction? action = await actionReader.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return action ?? throw new InvalidOperationException(
            $"Scheduled action '{id.Value}' not found.");
    }
}
