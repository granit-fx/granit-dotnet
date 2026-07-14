namespace Granit.DataExchange.Import.Identity;

/// <summary>
/// The kind of key an <see cref="EntityKey"/> represents, used to pick the
/// correct lookup strategy (and property set) when prefetching existing rows.
/// </summary>
public enum EntityKeyKind
{
    /// <summary>The key is the entity's primary key (e.g. resolved via an external-ID mapping table).</summary>
    PrimaryKey,

    /// <summary>The key is a declared business key (single or composite) from the import definition.</summary>
    BusinessKey,
}
