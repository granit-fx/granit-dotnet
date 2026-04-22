using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.SharedConnection;

/// <summary>
/// Factory-style accessor for the host application's authorization <see cref="DbContext"/>
/// — the one that implements <c>IPermissionGrantDbContext</c> and owns
/// <c>authorization_role_metadata</c> + <c>authorization_permission_grants</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="IIdentityDbContextAccessor"/> which exposes the scoped
/// instance, this accessor returns a <b>fresh</b> DbContext on every call, built via
/// <see cref="IDbContextFactory{TContext}"/>. The reason:
/// <c>IGranitRoleOrchestrator</c> swaps the underlying <c>DbConnection</c> on the
/// host context to share the Identity transaction. Calling
/// <c>Database.SetDbConnection(...)</c> on a DbContext that has already executed
/// queries in the current scope can throw <see cref="System.InvalidOperationException"/>
/// ("The connection was already initialized"). A fresh context is guaranteed not to
/// be "warm", so the swap is always safe.
/// </para>
/// <para>
/// Callers own the returned context — it must be disposed (<c>await using</c>).
/// </para>
/// <para>
/// This accessor requires the host application's DbContext to be registered via
/// <c>AddDbContextFactory&lt;THost&gt;</c> (which <c>AddGranitDbContext</c> does by
/// default). Applications wired with <c>AddDbContext&lt;THost&gt;</c> only will not
/// have the factory available; in that case the orchestrator's atomic path is
/// unavailable and it falls back to the compensating-write strategy.
/// </para>
/// </remarks>
public interface IAuthorizationHostDbContextAccessor
{
    /// <summary>Creates a fresh host DbContext. The caller owns disposal.</summary>
    Task<DbContext> CreateFreshDbContextAsync(CancellationToken cancellationToken = default);
}
