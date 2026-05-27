using Granit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Helpers for applying a client-supplied concurrency stamp to an
/// <see cref="IConcurrencyAware"/> entity in disconnected (CQRS) update scenarios.
/// </summary>
/// <remarks>
/// <para>
/// In the <b>connected</b> scenario — the entity was loaded from the same
/// <see cref="DbContext"/> that saves it — EF Core already tracks the original
/// stamp and no helper is needed.
/// </para>
/// <para>
/// In the <b>disconnected</b> scenario — a command handler receives the stamp from
/// the client and applies it to a freshly-attached entity — the original value must
/// be set explicitly so EF Core emits it in the <c>WHERE</c> clause on update.
/// These helpers replace the hand-written
/// <c>context.Entry(entity).Property(e =&gt; e.ConcurrencyStamp).OriginalValue = stamp;</c>
/// expression. A mismatch with the stored value raises
/// <see cref="DbUpdateConcurrencyException"/> (HTTP 409 via the framework exception mapper).
/// </para>
/// </remarks>
public static class ConcurrencyStampExtensions
{
    /// <summary>
    /// Sets the original concurrency stamp for <paramref name="entity"/> so EF Core
    /// validates it against the stored value on the next save (disconnected update).
    /// </summary>
    /// <typeparam name="TEntity">The <see cref="IConcurrencyAware"/> entity type.</typeparam>
    /// <param name="context">The tracking context the entity is (or will be) attached to.</param>
    /// <param name="entity">The entity whose original stamp is being set.</param>
    /// <param name="originalStamp">The stamp the client last read; must match the stored value.</param>
    public static void SetConcurrencyStampOriginalValue<TEntity>(
        this DbContext context,
        TEntity entity,
        string originalStamp)
        where TEntity : class, IConcurrencyAware
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrEmpty(originalStamp);

        context.Entry(entity).Property(e => e.ConcurrencyStamp).OriginalValue = originalStamp;
    }

    /// <summary>
    /// Sets the original concurrency stamp for <paramref name="entity"/> from a request DTO
    /// implementing <see cref="IConcurrencyStampRequest"/> (disconnected update).
    /// </summary>
    /// <typeparam name="TEntity">The <see cref="IConcurrencyAware"/> entity type.</typeparam>
    /// <param name="context">The tracking context the entity is (or will be) attached to.</param>
    /// <param name="entity">The entity whose original stamp is being set.</param>
    /// <param name="request">The request carrying the client-supplied stamp.</param>
    public static void SetConcurrencyStampOriginalValue<TEntity>(
        this DbContext context,
        TEntity entity,
        IConcurrencyStampRequest request)
        where TEntity : class, IConcurrencyAware
    {
        ArgumentNullException.ThrowIfNull(request);

        context.SetConcurrencyStampOriginalValue(entity, request.ConcurrencyStamp);
    }
}
