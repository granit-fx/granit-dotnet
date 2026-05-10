using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;

namespace Granit.Notifications.Internal;

/// <summary>
/// No-op delivery store for development. Replaced by EF Core store in production (ISO 27001 audit).
/// </summary>
internal sealed class NullNotificationDeliveryWriter : INotificationDeliveryWriter
{
    public Task<bool> HasBeenDeliveredAsync(Guid deliveryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    /// <remarks>Returns <see langword="true"/> perpetually — there is no cross-process idempotency without persistence.</remarks>
    public Task<bool> TryAcquireDeliveryAttemptAsync(
        NotificationDeliveryAttempt claim,
        CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task CompleteDeliveryAttemptAsync(
        Guid deliveryId,
        bool success,
        long durationMilliseconds,
        string? errorMessage,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> DeleteBeforeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
