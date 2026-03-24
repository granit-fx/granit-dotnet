namespace Granit.Events;

/// <summary>
/// No-op dispatcher used when no messaging infrastructure (e.g., Wolverine) is configured.
/// Registered by default in <c>Granit.Persistence</c>; replaced by the real implementation
/// when <c>Granit.Wolverine</c> is added.
/// </summary>
public sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
