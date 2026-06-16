using Granit.Authorization;
using Granit.Authorization.Domain;

namespace Granit.Identity.Federated.Cognito.Tests.Integration;

/// <summary>
/// Minimal in-memory <see cref="IRoleMetadataStore"/> for the integration tests. SQL-level
/// behaviour is covered by <c>RoleMetadataPostgresTests</c> in the Authorization.EF.Core
/// integration suite.
/// </summary>
internal sealed class InMemoryRoleMetadataStore : IRoleMetadataStore
{
    private readonly List<RoleMetadata> _rows = [];

    public IReadOnlyList<RoleMetadata> All => _rows;

    public Task<RoleMetadata?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.FirstOrDefault(r => r.Id == id));

    public Task<RoleMetadata?> FindByNameAsync(
        string name, Guid? tenantId, string? clientId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.FirstOrDefault(r =>
            r.Name == name && r.TenantId == tenantId && r.ClientId == clientId));

    public Task<IReadOnlyList<RoleMetadata>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoleMetadata>>(_rows);
    public Task<IReadOnlyList<RoleMetadata>> ListByClientIdAsync(
        string? clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoleMetadata>>(_rows.Where(r => r.ClientId == clientId).ToList());


    public Task AddAsync(RoleMetadata role, CancellationToken cancellationToken = default)
    {
        _rows.Add(role);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RoleMetadata role, string? concurrencyStamp = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(RoleMetadata role, CancellationToken cancellationToken = default)
    {
        _rows.Remove(role);
        return Task.CompletedTask;
    }
}
