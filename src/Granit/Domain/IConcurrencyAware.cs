namespace Granit.Domain;

/// <summary>
/// Interface for entities with optimistic concurrency control.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ConcurrencyStamp"/> is automatically regenerated on every
/// <c>SaveChanges</c>/<c>SaveChangesAsync</c> by <c>ConcurrencyStampInterceptor</c>
/// and configured as an EF Core concurrency token by
/// <c>ModelBuilderExtensions.ApplyGranitConventions()</c>.
/// </para>
/// <para>
/// When a concurrent update is detected (stamp mismatch in the <c>WHERE</c> clause),
/// EF Core throws <c>Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException</c>,
/// which is mapped to HTTP 409 Conflict by <c>EfCoreExceptionStatusCodeMapper</c>.
/// </para>
/// <para>
/// <b>Disconnected updates (CQRS):</b> when the entity is not loaded from the same
/// <c>DbContext</c>, set the original stamp received from the client before saving via
/// <c>ConcurrencyStampExtensions.SetConcurrencyStampOriginalValue</c>
/// (in <c>Granit.Persistence.EntityFrameworkCore</c>):
/// <code>
/// dbContext.SetConcurrencyStampOriginalValue(entity, request.ConcurrencyStamp);
/// </code>
/// </para>
/// </remarks>
public interface IConcurrencyAware
{
    /// <summary>
    /// Opaque concurrency stamp (GUID string, 36 characters).
    /// Automatically regenerated on every save by <c>ConcurrencyStampInterceptor</c>.
    /// </summary>
    string ConcurrencyStamp { get; set; }
}
