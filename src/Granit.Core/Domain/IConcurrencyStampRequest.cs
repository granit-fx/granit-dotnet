namespace Granit.Core.Domain;

/// <summary>
/// Optional convention for request DTOs that carry a concurrency stamp
/// for optimistic concurrency control on <see cref="IConcurrencyAware"/> entities.
/// </summary>
/// <remarks>
/// <para>
/// Endpoints handling <see cref="IConcurrencyAware"/> entities should include
/// the stamp in the response DTO and accept it back in the update request DTO.
/// The endpoint maps it to the entity before saving:
/// </para>
/// <code>
/// // Connected scenario (entity loaded from same DbContext):
/// entity.Name = request.Name; // EF Core already knows OriginalValue
///
/// // Disconnected scenario (CQRS command, new DbContext):
/// dbContext.Entry(entity).Property(e =&gt; e.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;
/// </code>
/// <para>
/// If the stamp in the database differs from the original value, EF Core throws
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>,
/// mapped to HTTP 409 Conflict by <c>EfCoreExceptionStatusCodeMapper</c>.
/// </para>
/// </remarks>
public interface IConcurrencyStampRequest
{
    /// <summary>
    /// Concurrency stamp from the last read, used for optimistic locking.
    /// Must match the current <see cref="IConcurrencyAware.ConcurrencyStamp"/>
    /// in the database; otherwise, the save throws a concurrency conflict.
    /// </summary>
    string ConcurrencyStamp { get; }
}
