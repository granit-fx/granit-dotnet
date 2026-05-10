using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.Parties.Deduplication.Tests.Internal;

/// <summary>Stub <see cref="ICurrentTenant"/> with a settable id — same shape as the
/// helper in <c>Granit.Parties.EntityFrameworkCore.Tests</c>.</summary>
internal sealed class StubCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => Id is not null;
    public Guid? Id { get; private set; }
    public string? Name { get; private set; }

    public void Set(Guid? id) => Id = id;

    public IDisposable Change(Guid? id, string? name = null)
    {
        Guid? previousId = Id;
        Id = id;
        return new RestoreScope(() => Id = previousId);
    }

    private sealed class RestoreScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

/// <summary>Minimal <see cref="IDbContextFactory{TContext}"/> over a shared
/// <see cref="InMemoryDatabaseRoot"/> + <see cref="ICurrentTenant"/> stub. Lets the
/// detector matchers — which all call <c>contextFactory.CreateDbContextAsync</c> —
/// exercise the full code path against a single InMemory instance.</summary>
internal sealed class InMemoryPartiesDbContextFactory : IDbContextFactory<PartiesDbContext>
{
    private readonly DbContextOptions<PartiesDbContext> _options;
    private readonly StubCurrentTenant _tenant;
    private readonly DataFilter _filter;
    private readonly PartyCanonicalisationInterceptor _canonicaliser = new(new IdentityHasher());

    public InMemoryPartiesDbContextFactory(string databaseName, StubCurrentTenant tenant, DataFilter filter)
    {
        _tenant = tenant;
        _filter = filter;

        InMemoryDatabaseRoot dbRoot = new();
        _options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseInMemoryDatabase(databaseName, dbRoot)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .EnableServiceProviderCaching(false)
            .AddInterceptors(_canonicaliser)
            .Options;
    }

    public PartiesDbContext CreateDbContext() => new(_options, new PassthroughEncryption(), _tenant, _filter);

    public Task<PartiesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PartiesDbContext(_options, new PassthroughEncryption(), _tenant, _filter));
}

internal sealed class PassthroughEncryption : Granit.Encryption.IStringEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string? Decrypt(string cipherText) => cipherText;
}

internal sealed class IdentityHasher : Granit.Parties.EntityFrameworkCore.Internal.IPartyLookupHasher
{
    public string? ComputeHash(string? canonical) =>
        string.IsNullOrEmpty(canonical) ? null : $"hash:{canonical}";
}
