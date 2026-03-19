namespace Granit.Core.Events;

/// <summary>
/// Marker interface for domain events.
/// </summary>
/// <remarks>
/// Domain events are in-process and transactional: handled within the same
/// Wolverine execution context and database transaction as the originating command.
/// They never cross module boundaries and are never routed to the Outbox.
/// <para>
/// Preferred publication — Wolverine sidecar pattern:
/// <code>
/// public static PatientDischarged Handle(DischargePatientCommand cmd, AppDbContext db)
/// {
///     // ...
///     return new PatientDischarged(patient.Id);
/// }
/// </code>
/// </para>
/// <para>Naming convention: <c>*Event</c> suffix (e.g., <c>PatientDischargedEvent</c>).</para>
/// </remarks>
public interface IDomainEvent { }
