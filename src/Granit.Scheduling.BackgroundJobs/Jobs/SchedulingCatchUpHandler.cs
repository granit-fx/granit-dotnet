using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Scheduling.Diagnostics;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Wolverine;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Scheduling.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="SchedulingCatchUpJob"/>. Detects overdue pending scheduled
/// actions and re-dispatches their payloads via Wolverine.
/// </summary>
internal static partial class SchedulingCatchUpHandler
{
    private static readonly TimeSpan OverdueThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Scans for overdue pending actions and re-dispatches them.
    /// </summary>
    public static async Task HandleAsync(
        SchedulingCatchUpJob _,
        IScheduledActionReader reader,
        IMessageBus messageBus,
        IClock clock,
        ScheduledPayloadTypeRegistry payloadTypeRegistry,
        SchedulingMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<SchedulingCatchUpJob> logger,
        CancellationToken cancellationToken)
    {
        DateTimeOffset threshold = clock.Now - OverdueThreshold;
        IReadOnlyList<ScheduledAction> overdueActions = await reader
            .GetOverduePendingAsync(threshold, cancellationToken)
            .ConfigureAwait(false);

        if (overdueActions.Count == 0)
        {
            return;
        }

        Log.OverdueActionsDetected(logger, overdueActions.Count);
        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;

        foreach (ScheduledAction action in overdueActions)
        {
            if (!payloadTypeRegistry.TryResolve(action.PayloadType, out Type? payloadType))
            {
                Log.PayloadTypeNotFound(logger, action.Id, action.PayloadType);
                continue;
            }

            object? payload = JsonSerializer.Deserialize(action.PayloadJson, payloadType);
            if (payload is null)
            {
                Log.PayloadDeserializationFailed(logger, action.Id, action.PayloadType);
                continue;
            }

            DeliveryOptions options = new();
            options.Headers[ScheduledActionStatusMiddleware.ActionIdHeader] = action.Id.ToString();
            await messageBus.PublishAsync(payload, options).ConfigureAwait(false);

            metrics.RecordCatchUpRedispatched(tenantId, payloadType.Name);
            Log.ActionReDispatched(logger, action.Id, action.PayloadType);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduling catch-up: detected {Count} overdue action(s)")]
        public static partial void OverdueActionsDetected(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Error, Message = "Scheduling catch-up: payload type '{PayloadType}' is not a registered IScheduledPayload for action {ActionId}")]
        public static partial void PayloadTypeNotFound(ILogger logger, Guid actionId, string payloadType);

        [LoggerMessage(Level = LogLevel.Error, Message = "Scheduling catch-up: failed to deserialize payload '{PayloadType}' for action {ActionId}")]
        public static partial void PayloadDeserializationFailed(ILogger logger, Guid actionId, string payloadType);

        [LoggerMessage(Level = LogLevel.Information, Message = "Scheduling catch-up: re-dispatched action {ActionId} (type: {PayloadType})")]
        public static partial void ActionReDispatched(ILogger logger, Guid actionId, string payloadType);
    }
}
