using System.Runtime.CompilerServices;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// No-yield implementation of <see cref="ITenantEnumerator"/> for single-tenant
/// and Shared database topologies.
/// </summary>
/// <remarks>
/// When this enumerator returns no values, <c>MigrationStartupService</c> falls back
/// to the <c>TenantId</c> stored in each <c>MigrationProgress</c> row.
/// </remarks>
internal sealed class NullTenantEnumerator : ITenantEnumerator
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<Guid> GetActiveTenantIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
