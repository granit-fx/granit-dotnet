namespace Granit.Domain;

/// <summary>
/// Storage strategy for a <see cref="QueryableValueObjectAttribute"/> column (ADR-070).
/// </summary>
public enum QueryableValueObjectStorage
{
    /// <summary>
    /// EF <c>ComplexProperty</c> — the inner <c>.Value</c> is a real scalar column (indexable, no
    /// storage change). The default. <b>Non-nullable columns only.</b>
    /// </summary>
    ComplexProperty,

    /// <summary>
    /// EF JSON column (<c>OwnsOne(...).ToJson()</c>) — the only strategy that supports a
    /// <b>nullable</b> searchable value object. Stores <c>{"Value":"…"}</c> (a data migration to
    /// adopt) and indexes more weakly than a real column.
    /// </summary>
    Json,
}

/// <summary>
/// Opts a <see cref="SingleValueObject{T}"/> entity column into a <b>queryable</b> persistence
/// strategy (ADR-070): the value object is mapped so its underlying <c>.Value</c> is queryable
/// (substring search, group-by, sort) instead of the default opaque <c>ValueConverter</c>.
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="QueryableValueObjectStorage.ComplexProperty"/> exposes <c>.Value</c> as a
/// real scalar column (non-nullable only). For a <b>nullable</b> searchable column use
/// <see cref="QueryableValueObjectStorage.Json"/>. Either way the QueryEngine drills into
/// <c>.Value</c> — substring/range/group-by translate against the underlying value.
/// </para>
/// <para>
/// <c>ApplyGranitConventions</c> applies the mapping automatically; no manual configuration needed.
/// EF Core cannot translate these operations over the default value converter (see ADR-070 and
/// <see href="https://github.com/dotnet/efcore/issues/10434">dotnet/efcore#10434</see>).
/// </para>
/// </remarks>
/// <param name="storage">The persistence strategy (defaults to <see cref="QueryableValueObjectStorage.ComplexProperty"/>).</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class QueryableValueObjectAttribute(
    QueryableValueObjectStorage storage = QueryableValueObjectStorage.ComplexProperty) : Attribute
{
    /// <summary>The persistence strategy for this column.</summary>
    public QueryableValueObjectStorage Storage { get; } = storage;
}
