using Granit.Domain;
using NpgsqlTypes;

namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// EF Core row backing one indexed entry. One physical table per <typeparamref name="TKey"/>
/// — naming convention <c>IndexedEntry_{TKey}</c>, mapped by
/// <see cref="Configurations.IndexedEntryRowConfiguration{TKey}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-tenant.</b> Implements <see cref="IMultiTenant"/> so
/// <c>GranitDbContext</c>'s parameterised filter applies to every read and the
/// <c>AuditedEntityInterceptor</c> auto-stamps <see cref="TenantId"/> on insert.
/// </para>
/// <para>
/// <b>tsvector column.</b> <see cref="SearchVector"/> maps to a Postgres
/// <c>GENERATED ALWAYS AS (...) STORED</c> column built by
/// <see cref="Extensions.ModelBuilderExtensions.HasGeneratedTsVectorColumn"/>. The
/// generator picks the Postgres text-search dictionary from <see cref="Language"/>
/// (English, French, …) — the mapping is owned by I-F3.1 (<c>Granit.Indexing.Lingua</c>).
/// </para>
/// <para>
/// <b>No ACL columns.</b> Per the framework's authorization boundary, this row carries
/// no permission state. <see cref="DataSubjectId"/> is the only personal-data hook; it
/// drives GDPR Art. 17 cascades and is NOT consulted at read time.
/// </para>
/// </remarks>
/// <typeparam name="TKey">Primary key of the source resource.</typeparam>
public sealed class IndexedEntryRow<TKey> : IMultiTenant
{
    /// <summary>Resource primary key.</summary>
    public TKey Key { get; set; } = default!;

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }

    /// <summary>Indexed body. Postgres column type <c>text</c>; no length cap on the column.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Generated tsvector column. Configured as a Postgres stored generated column;
    /// EF Core treats this as <c>HasComputedColumnSql(..., stored: true)</c> and never
    /// writes to it from .NET.
    /// </summary>
    public NpgsqlTsVector SearchVector { get; set; } = default!;

    /// <summary>ISO 639-1 language code used to pick the tsvector dictionary.</summary>
    public string? Language { get; set; }

    /// <summary>Optional abstractive summary, surfaced as a search-result snippet.</summary>
    public string? Summary { get; set; }

    /// <summary>Optional facet tags; mapped as Postgres <c>text[]</c>.</summary>
    public string[]? Tags { get; set; }

    /// <summary>True when the source text was truncated by the producing extractor.</summary>
    public bool IsTruncated { get; set; }

    /// <summary>Character count of <see cref="Content"/> as produced.</summary>
    public int CharCount { get; set; }

    /// <summary>
    /// Optional data-subject identifier. Indexed by <c>(TenantId, DataSubjectId)</c> so the
    /// GDPR Art. 17 handler can delete by subject in a single <c>ExecuteDelete</c>.
    /// </summary>
    public Guid? DataSubjectId { get; set; }
}
