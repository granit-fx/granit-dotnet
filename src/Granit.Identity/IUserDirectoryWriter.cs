using Granit.Identity.Domain;

namespace Granit.Identity;

/// <summary>
/// Write-side companion to <see cref="IUserDirectoryQueryableSource"/> —
/// inserts <see cref="User"/> rows into the canonical directory. Per
/// ADR-051 B-step 2.5, the local-side ASP.NET Identity wrapper calls
/// <see cref="CreateAsync"/> before persisting a <c>LocalIdentity</c> so
/// that every authenticatable record has a matching <see cref="User"/>
/// surface for admin grids, OData feeds, and BI exports.
/// </summary>
/// <remarks>
/// <para>
/// Implementations live in persistence-layer companion packages (e.g.
/// <c>Granit.Identity.EntityFrameworkCore</c>). Hosts that swap the
/// persistence layer (a Mongo or DynamoDB companion) only need to
/// re-implement this interface — the local-side identity wiring stays
/// the same.
/// </para>
/// <para>
/// Profile updates and deletions flow through the bridge integration
/// events (<c>UserProfileChangedEto</c>, future GDPR deletion handler) —
/// this contract is intentionally creation-only. Reads stay on
/// <see cref="IUserDirectoryQueryableSource"/>.
/// </para>
/// </remarks>
public interface IUserDirectoryWriter
{
    /// <summary>
    /// Persists a new <see cref="User"/> row. Caller-supplied
    /// <see cref="Granit.Domain.Entity{TKey}.Id"/> is honoured — this is
    /// the alignment point that lets the local-side <c>LocalIdentity</c>
    /// re-use the same Guid for both rows so historical references
    /// continue to resolve.
    /// </summary>
    /// <param name="user">The user aggregate to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compensating delete for the rare case where the upstream
    /// <c>LocalIdentity</c> insert fails after the <see cref="User"/>
    /// row has already been persisted. Hard-deletes the row — the
    /// <see cref="User"/> aggregate is not soft-deletable (per ADR-051
    /// the canonical user is the GDPR drop point).
    /// </summary>
    /// <param name="userId">The user identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
