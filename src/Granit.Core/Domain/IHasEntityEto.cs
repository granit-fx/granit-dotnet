namespace Granit.Core.Domain;

/// <summary>
/// Opt-in interface for distributed entity lifecycle events (ETO — Event Transfer Object).
/// Extends <see cref="IEmitEntityLifecycleEvents"/> so local lifecycle events are also emitted.
/// </summary>
/// <typeparam name="TEto">
/// The flat, serializable ETO type. Must not contain EF Core entities, navigation properties,
/// lazy-load proxies, or any type that cannot be serialized to JSON.
/// </typeparam>
/// <remarks>
/// Entities implementing this interface automatically emit both:
/// <list type="bullet">
///   <item>Local: <c>EntityCreatedEvent&lt;T&gt;</c> / <c>EntityUpdatedEvent&lt;T&gt;</c> / <c>EntityDeletedEvent&lt;T&gt;</c></item>
///   <item>Distributed: <c>EntityCreatedEto&lt;TEto&gt;</c> / <c>EntityUpdatedEto&lt;TEto&gt;</c> / <c>EntityDeletedEto&lt;TEto&gt;</c> via Wolverine Outbox</item>
/// </list>
/// The distributed ETOs are dispatched in <c>SavingChanges</c> (before the transaction commits)
/// so that Wolverine writes outbox envelopes atomically within the same EF Core transaction.
/// <example>
/// <code>
/// public class Product : AggregateRoot, IHasEntityEto&lt;ProductEto&gt;
/// {
///     public string Name { get; private set; } = string.Empty;
///     public decimal Price { get; private set; }
///
///     public ProductEto ToEto() => new(Id, Name, Price);
/// }
///
/// public sealed record ProductEto(Guid Id, string Name, decimal Price);
/// </code>
/// </example>
/// </remarks>
public interface IHasEntityEto<TEto> : IEmitEntityLifecycleEvents, IEntityEtoProvider
    where TEto : class
{
    /// <summary>
    /// Produces a flat, serializable snapshot of the entity for cross-service event dispatch.
    /// Called by the <c>EntityLifecycleEventInterceptor</c> in <c>SavingChanges</c>.
    /// </summary>
    TEto ToEto();

    (Type EtoType, object Eto) IEntityEtoProvider.GetEto() => (typeof(TEto), ToEto());
}
