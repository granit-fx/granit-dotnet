using System.Diagnostics.Metrics;
using Granit.DataFiltering;
using Granit.Encryption;
using Granit.MultiTenancy;
using Granit.Parties.Diagnostics;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Minimal <see cref="IMeterFactory"/> stub for tests — returns a fresh <see cref="Meter"/>
/// per request without DI plumbing. Used to construct a real <see cref="PartiesMetrics"/>
/// in store / scope tests where metric assertions are not the focus.
/// </summary>
internal sealed class StubMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new(options);
    public void Dispose() { }

    public static PartiesMetrics CreatePartiesMetrics() => new(new StubMeterFactory());
}

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
/// + <see cref="IDataFilter"/> into every <see cref="PartiesDbContext"/> it produces, so
/// the multi-tenant query filter is wired and active.
/// </summary>
internal sealed class ScopedFactory(
    DbContextOptions<PartiesDbContext> options,
    ICurrentTenant tenant,
    IDataFilter filter) : IDbContextFactory<PartiesDbContext>
{
    public PartiesDbContext CreateDbContext() => new(options, new PassthroughEncryption(), tenant, filter);

    public Task<PartiesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PartiesDbContext(options, new PassthroughEncryption(), tenant, filter));
}

internal sealed class PassthroughEncryption : IStringEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string? Decrypt(string cipherText) => cipherText;
}
