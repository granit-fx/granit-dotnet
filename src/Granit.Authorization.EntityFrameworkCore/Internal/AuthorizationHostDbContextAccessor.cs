using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authorization.EntityFrameworkCore.Internal;

/// <summary>
/// Factory-style accessor for the host application's authorization DbContext.
/// Resolves <see cref="IDbContextFactory{TContext}"/> lazily via the service provider
/// so consumers that only registered the DbContext via <c>AddDbContext&lt;T&gt;</c>
/// (no factory) can still wire this accessor — the atomic path in
/// <c>IGranitRoleOrchestrator</c> then naturally falls back to the compensating
/// strategy because <see cref="CreateFreshDbContextAsync"/> throws.
/// </summary>
internal sealed class AuthorizationHostDbContextAccessor<TContext>(
    IServiceProvider serviceProvider) : IAuthorizationHostDbContextAccessor
    where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
{
    public async Task<Microsoft.EntityFrameworkCore.DbContext> CreateFreshDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        IDbContextFactory<TContext>? factory =
            serviceProvider.GetService<IDbContextFactory<TContext>>()
            ?? throw new InvalidOperationException(
                $"IDbContextFactory<{typeof(TContext).Name}> is not registered. " +
                "The shared-connection atomic transaction path requires the host DbContext " +
                "to be registered via AddDbContextFactory<T> (AddGranitDbContext does this by default).");

        return await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    }
}
