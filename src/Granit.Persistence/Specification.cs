using System.Linq.Expressions;

namespace Granit.Persistence;

/// <summary>
/// Persistence-agnostic query builder using <see cref="Expression{TDelegate}"/> predicates.
/// Works with EF Core, MongoDB LINQ, and compiled for in-memory evaluation.
/// </summary>
/// <typeparam name="T">The entity type to query.</typeparam>
/// <remarks>
/// <para>
/// Subclass for named, reusable specifications:
/// <code>
/// public sealed class OrphanedBlobsSpec : Specification&lt;BlobDescriptor&gt;
/// {
///     public OrphanedBlobsSpec(DateTimeOffset cutoff, int batchSize)
///     {
///         Where(b =&gt; b.Status == BlobStatus.Pending &amp;&amp; b.CreatedAt &lt; cutoff);
///         OrderBy(b =&gt; b.CreatedAt);
///         Limit(batchSize);
///     }
/// }
/// </code>
/// </para>
/// <para>
/// For inline, one-off specifications use <see cref="Spec.For{T}()"/>:
/// <code>
/// ListAsync(Spec.For&lt;BlobDescriptor&gt;()
///     .Where(b =&gt; b.Status == BlobStatus.Pending)
///     .OrderBy(b =&gt; b.CreatedAt)
///     .Limit(100), ct);
/// </code>
/// </para>
/// <para>
/// No <c>IgnoreFilter</c> API is provided. Query filter bypass (tenant, soft-delete, GDPR)
/// requires explicit DbContext access via <c>ReadAsync</c> delegate — visible, auditable,
/// and guarded by architecture tests. See VULN-200 in ADR-019 security review.
/// </para>
/// </remarks>
public abstract class Specification<T> where T : class
{
    private readonly List<Expression<Func<T, bool>>> _criteria = [];
    private readonly List<SortExpression<T>> _orderExpressions = [];

    /// <summary>Filter predicates (ANDed together).</summary>
    public IReadOnlyList<Expression<Func<T, bool>>> Criteria => _criteria;

    /// <summary>Sort expressions in application order.</summary>
    public IReadOnlyList<SortExpression<T>> OrderExpressions => _orderExpressions;

    /// <summary>Number of items to skip (offset pagination).</summary>
    public int? Skip { get; private set; }

    /// <summary>Maximum number of items to return.</summary>
    public int? Take { get; private set; }

    /// <summary>Hint for read-only queries (e.g., <c>AsNoTracking</c> in EF Core).</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>Adds a filter predicate. Multiple calls are ANDed.</summary>
    protected void Where(Expression<Func<T, bool>> predicate) => _criteria.Add(predicate);

    /// <summary>Adds an ascending sort criterion.</summary>
    protected void OrderBy(Expression<Func<T, object>> keySelector) =>
        _orderExpressions.Add(new(keySelector, Ascending: true));

    /// <summary>Adds a descending sort criterion.</summary>
    protected void OrderByDescending(Expression<Func<T, object>> keySelector) =>
        _orderExpressions.Add(new(keySelector, Ascending: false));

    /// <summary>Applies offset pagination.</summary>
    protected void Paginate(int page, int pageSize)
    {
        Skip = (page - 1) * pageSize;
        Take = pageSize;
    }

    /// <summary>Limits the result count without offset.</summary>
    protected void Limit(int count) => Take = count;

    /// <summary>Marks the query as read-only (no change tracking).</summary>
    protected void AsReadOnly() => IsReadOnly = true;
}

/// <summary>
/// Specification with server-side projection to <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="T">The source entity type.</typeparam>
/// <typeparam name="TResult">The projected result type.</typeparam>
public abstract class Specification<T, TResult> : Specification<T>
    where T : class
{
    /// <summary>Projection expression (e.g., <c>Select(e =&gt; new Dto(e.Name))</c>).</summary>
    public Expression<Func<T, TResult>>? Selector { get; private set; }

    /// <summary>Sets the projection expression.</summary>
    protected void Select(Expression<Func<T, TResult>> selector) => Selector = selector;
}
