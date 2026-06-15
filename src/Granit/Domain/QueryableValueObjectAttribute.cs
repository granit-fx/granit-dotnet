namespace Granit.Domain;

/// <summary>
/// Opts a <see cref="SingleValueObject{T}"/> entity column into the <b>queryable</b> persistence
/// strategy (ADR-070, strategy B): the value object is mapped as an EF
/// <c>ComplexProperty</c> so its underlying <c>.Value</c> is a genuinely mapped scalar column,
/// rather than the default opaque <c>ValueConverter</c>.
/// </summary>
/// <remarks>
/// <para>
/// The default converter mapping makes a value-object column searchable only by equality/IN
/// (EF Core cannot translate <c>LIKE</c>, <c>GROUP BY</c> or keyset comparisons over a value
/// converter — see ADR-070 and issue #2767). Marking the property with this attribute tells the
/// QueryEngine to <b>drill into <c>.Value</c></b> — substring search, range filters and group-by
/// then translate against the real column.
/// </para>
/// <para>
/// The owning <c>DbContext</c> must map the property as a complex property with the inner value
/// named to match the column, e.g.
/// <c>builder.ComplexProperty(e =&gt; e.Slug, b =&gt; b.Property(s =&gt; s.Value).HasColumnName("Slug"))</c>.
/// Only <b>non-nullable</b> value-object columns are supported (a nullable complex property throws
/// on a null value — verified, EF Core 10).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class QueryableValueObjectAttribute : Attribute;
