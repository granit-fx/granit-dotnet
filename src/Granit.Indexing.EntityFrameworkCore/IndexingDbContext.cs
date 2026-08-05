using Granit.DataFiltering;
using Granit.Indexing.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Isolated EF Core DbContext for the indexing module. One physical
/// <see cref="DbSet{TEntity}"/> per registered key type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-tenant.</b> Inherits <see cref="GranitDbContext"/> so the tenant query
/// filter is parameterised at the SQL level (no closure-captured constant leak).
/// </para>
/// <para>
/// <b>Per-key-type shape.</b> Consumers pass every key type they index to
/// <c>AddGranitIndexingEntityFrameworkCore</c>; the DbContext enumerates registrations
/// from <see cref="IndexingDbContextSchema"/> at <see cref="OnGranitModelCreating"/>
/// time and delegates the model-building to
/// <c>ModelBuilderExtensions.ConfigureIndexingModule</c>.
/// </para>
/// <para>
/// <b>Alternative folding path.</b> Hosts that prefer a single consolidated DbContext
/// (single migration tree, single connection scope) can skip
/// <c>AddGranitIndexingEntityFrameworkCore</c> entirely and call
/// <c>modelBuilder.ConfigureIndexingModule([typeof(Guid), ...])</c> directly from
/// their own <see cref="DbContext.OnModelCreating"/>.
/// </para>
/// </remarks>
public sealed class IndexingDbContext : GranitDbContext
{
    internal IReadOnlyList<Type> IndexedKeyTypes { get; }
    internal string DefaultDictionary { get; }
    internal int? EmbeddingDimensions { get; }

    public IndexingDbContext(
        DbContextOptions<IndexingDbContext> options,
        ICurrentTenant currentTenant,
        IndexingDbContextSchema schema,
        IDataFilter? dataFilter = null)
        : base(options, currentTenant, dataFilter)
    {
        ArgumentNullException.ThrowIfNull(schema);
        IndexedKeyTypes = schema.KeyTypes;
        DefaultDictionary = schema.DefaultDictionary;
        EmbeddingDimensions = schema.EmbeddingDimensions;
    }

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        bool isPostgres = string.Equals(Database.ProviderName, GranitDbProviders.Postgres, StringComparison.Ordinal);

        modelBuilder.ConfigureIndexingModule(
            IndexedKeyTypes,
            DefaultDictionary,
            EmbeddingDimensions,
            isPostgres);
    }
}

/// <summary>
/// Per-DbContext schema bundle: which TKey tables to map, which fallback dictionary to
/// use for tsvector generation, and (optionally) the embedding vector dimensionality
/// when the host opts into <c>Granit.Indexing.Embeddings</c>. Resolved from DI by
/// <see cref="IndexingDbContext"/> at construction time.
/// </summary>
public sealed record IndexingDbContextSchema(
    IReadOnlyList<Type> KeyTypes,
    string DefaultDictionary,
    int? EmbeddingDimensions = null);
