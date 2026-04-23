using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData.Lookups;

/// <summary>
/// Lookup source backed by an <see cref="IReferenceDataStoreReader{TEntity}"/>. Projects every
/// reference data entry to the canonical <see cref="LookupItem"/> shape using its
/// <see cref="ReferenceDataEntity.Code"/> as the value and <see cref="ReferenceDataEntity.Label"/>
/// as the label (already localized via <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>).
/// </summary>
/// <remarks>
/// <para>
/// The source honors the <see cref="ReferenceDataEntity.Activated"/> filter (only active
/// entries are returned) and the ambient <c>ICurrentTenant</c> for tenant-scoped refdata.
/// Labels fall back to <see cref="ReferenceDataEntity.LabelEn"/> when the culture-specific
/// column is empty (see <see cref="ReferenceDataEntity.Label"/>).
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
internal sealed class ReferenceDataLookupSource<TEntity> : ILookupSource, IKindProviderLookupSource
    where TEntity : ReferenceDataEntity
{
    private readonly IReferenceDataStoreReader<TEntity> _reader;

    public ReferenceDataLookupSource(
        string name,
        IReferenceDataStoreReader<TEntity> reader,
        string? requiredPermission = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(reader);

        Name = name;
        _reader = reader;
        RequiredPermission = requiredPermission;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string? RequiredPermission { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> ScopeKeys => [];

    LookupKind IKindProviderLookupSource.Kind => LookupKind.ReferenceData;

    /// <inheritdoc/>
    public async ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ReferenceDataQuery refQuery = new(
            ActiveOnly: true,
            SearchTerm: string.IsNullOrWhiteSpace(query.Search) ? null : query.Search,
            SortBy: "SortOrder",
            Descending: false,
            Page: Math.Max(1, query.Page),
            PageSize: Math.Clamp(query.PageSize, 1, 200));

        PagedResult<TEntity> page = await _reader
            .GetAllAsync(refQuery, cancellationToken)
            .ConfigureAwait(false);

        LookupItem[] items = [.. page.Items.Select(ToLookupItem)];
        return new LookupResult(items, page.TotalCount);
    }

    /// <inheritdoc/>
    public async ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        string? code = value.ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        TEntity? entry = await _reader
            .GetByCodeAsync(code, cancellationToken)
            .ConfigureAwait(false);

        return entry is null ? null : ToLookupItem(entry);
    }

    private static LookupItem ToLookupItem(TEntity entry) =>
        new(entry.Code, string.IsNullOrEmpty(entry.Label) ? entry.Code : entry.Label);
}
