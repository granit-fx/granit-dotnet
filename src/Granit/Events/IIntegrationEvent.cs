namespace Granit.Events;

/// <summary>
/// Marker interface for integration events (ETO — Event Transfer Object).
/// </summary>
/// <remarks>
/// Integration events cross module boundaries and are delivered durably via the
/// Wolverine Outbox. They are only dispatched after the originating database
/// transaction commits.
/// <para>
/// <strong>NEVER</strong> include EF Core entities or internal domain objects.
/// Use flat, serializable DTOs only. Exposing an entity as an integration event
/// leaks the internal model and breaks module boundaries.
/// </para>
/// <para>Naming convention: <c>*Eto</c> suffix — Event Transfer Object (e.g., <c>BedReleasedEto</c>).</para>
/// </remarks>
public interface IIntegrationEvent { }
