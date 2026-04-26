using Granit.Contacts.EntityFrameworkCore.Internal;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore.Tests.Internal;

/// <summary>Mutable <see cref="ICurrentTenant"/> stub for scoped reads inside one fixture.</summary>
internal sealed class StubCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => Id is not null;
    public Guid? Id { get; private set; }
    public string? Name { get; private set; }

    public void Set(Guid tenantId, string? name = null)
    {
        Id = tenantId;
        Name = name;
    }

    public void Clear()
    {
        Id = null;
        Name = null;
    }

    public IDisposable Change(Guid? id, string? name = null)
    {
        Guid? previousId = Id;
        string? previousName = Name;
        Id = id;
        Name = name;
        return new RestoreScope(() =>
        {
            Id = previousId;
            Name = previousName;
        });
    }

    private sealed class RestoreScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/> that injects an <see cref="ICurrentTenant"/>
/// + <see cref="IDataFilter"/> into every <see cref="ContactsDbContext"/> it produces, so
/// the multi-tenant query filter is wired and active.
/// </summary>
internal sealed class ScopedFactory(
    DbContextOptions<ContactsDbContext> options,
    ICurrentTenant tenant,
    IDataFilter filter) : IDbContextFactory<ContactsDbContext>
{
    public ContactsDbContext CreateDbContext() => new(options, tenant, filter);

    public Task<ContactsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ContactsDbContext(options, tenant, filter));
}
