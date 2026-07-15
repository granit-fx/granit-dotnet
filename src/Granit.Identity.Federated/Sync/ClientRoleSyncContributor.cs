using Granit.Persistence.DataSeeding;

namespace Granit.Identity.Federated.Sync;

/// <summary>
/// Host-level data seed contributor that runs the client-role sync at boot for every registered
/// federated provider. Replaces the per-provider contributors — each provider now registers only
/// an <see cref="IClientRoleSyncPolicy"/>, and this single contributor drives them all through the
/// shared <see cref="ClientRoleSyncEngine"/>.
/// </summary>
/// <remarks>
/// The enclosing seeder runs all <see cref="IHostDataSeedContributor"/> contributors once per host
/// boot in a tenantless scope — which matches the sync contract (client roles are host-scope,
/// persisted with <c>TenantId = null</c>). With no provider wired, <paramref name="policies"/> is
/// empty and this is a no-op.
/// </remarks>
internal sealed class ClientRoleSyncContributor(
    IClientRoleSyncEngine engine,
    IEnumerable<IClientRoleSyncPolicy> policies) : IHostDataSeedContributor
{
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        foreach (IClientRoleSyncPolicy policy in policies)
        {
            await engine.SyncAsync(policy, cancellationToken).ConfigureAwait(false);
        }
    }
}
