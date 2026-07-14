using System.Linq.Expressions;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Filtering.Exceptions;
using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Builds a single <see cref="Expression{TDelegate}"/> from a <see cref="QueryPredicate"/> tree
/// under <b>strict</b> validation semantics — the counterpart of the lenient
/// <see cref="QueryableFilterExtensions.ApplyFilters{TEntity}"/> path.
/// </summary>
/// <remarks>
/// <para>
/// The lenient wire path silently drops unknown fields, disallowed operators, and unconvertible
/// values: inside a flat implicit-AND filter, a dropped criterion only widens the result, which
/// is acceptable for UI grids. Inside OR/NOT, a silently vanished leaf <b>changes result
/// semantics</b> (an OR branch that should constrain nothing more suddenly matches everything;
/// a NOT flips a dropped criterion into an unintended full match). The strict path therefore
/// collects every violation and throws <see cref="QueryPredicateValidationException"/> instead
/// of returning a superset.
/// </para>
/// <para>
/// Negation follows C#/EF two-valued semantics: <c>Not(Contains(...))</c> matches rows whose
/// column is NULL (the Contains predicate guards <c>column != null</c>, so its negation is true
/// for NULL columns). This is deliberate and pinned by tests — protocol adapters that need
/// SQL three-valued semantics must express null handling explicitly via
/// <see cref="FilterOperator.IsNull"/> / <see cref="FilterOperator.IsNotNull"/>.
/// </para>
/// </remarks>
internal static class PredicateExpressionBuilder
{
    /// <summary>
    /// Validates <paramref name="predicate"/> against the definition's filterable columns and
    /// builds one predicate expression sharing a single <see cref="ParameterExpression"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="predicate">The predicate tree.</param>
    /// <param name="builder">The query definition builder (filterable-column whitelist).</param>
    /// <param name="logger">Optional logger for diagnosing conversion failures.</param>
    /// <returns>The composed predicate expression.</returns>
    /// <exception cref="QueryPredicateValidationException">
    /// The tree failed strict validation; <see cref="QueryPredicateValidationException.Errors"/>
    /// lists every violation.
    /// </exception>
    public static Expression<Func<TEntity, bool>> BuildStrict<TEntity>(
        QueryPredicate predicate,
        QueryDefinitionBuilder<TEntity> builder,
        ILogger? logger = null)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(builder);

        BuildContext<TEntity> context = new(builder, logger);
        Expression? body = context.Visit(predicate, depth: 1);

        if (context.Errors.Count > 0)
        {
            throw new QueryPredicateValidationException(context.Errors);
        }

        // A null body with zero errors cannot happen: every visit path either produces a body
        // or records at least one error. The guard keeps the invariant explicit.
        return Expression.Lambda<Func<TEntity, bool>>(
            body ?? throw new InvalidOperationException("Predicate produced no expression body."),
            context.Parameter);
    }

    private sealed class BuildContext<TEntity> where TEntity : class
    {
        private readonly Dictionary<string, ColumnDescriptor> _columns;
        private readonly Dictionary<string, ColumnDescriptor>? _shadowColumns;
        private readonly ILogger? _logger;
        private int _leafCount;
        private bool _depthReported;
        private bool _leafCountReported;

        public BuildContext(QueryDefinitionBuilder<TEntity> builder, ILogger? logger)
        {
            _logger = logger;
            _columns = new Dictionary<string, ColumnDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (ColumnDescriptor column in builder.Columns)
            {
                _columns.TryAdd(column.PropertyName, column);
            }

            var shadows = builder.Columns.Where(c => c.IsShadowProperty && c.IsFilterable).ToList();
            if (shadows.Count > 0)
            {
                _shadowColumns = shadows.ToDictionary(c => c.PropertyName, StringComparer.OrdinalIgnoreCase);
            }
        }

        public ParameterExpression Parameter { get; } = Expression.Parameter(typeof(TEntity), "e");

        public List<QueryPredicateError> Errors { get; } = [];

        public Expression? Visit(QueryPredicate node, int depth)
        {
            if (depth > QueryPredicate.MaxDepth)
            {
                if (!_depthReported)
                {
                    _depthReported = true;
                    Errors.Add(new QueryPredicateError(
                        Field: null,
                        QueryPredicateErrorCodes.TreeTooDeep,
                        $"The predicate tree exceeds the maximum depth of {QueryPredicate.MaxDepth}."));
                }

                return null; // Prune: nothing below an over-deep node is built or validated.
            }

            return node switch
            {
                FilterPredicate leaf => VisitLeaf(leaf.Criteria),
                AndPredicate and => VisitComposite(and.Operands, depth, Expression.AndAlso),
                OrPredicate or => VisitComposite(or.Operands, depth, Expression.OrElse),
                NotPredicate not => VisitNot(not, depth),
                _ => throw new ArgumentException(
                    $"Unsupported predicate node type '{node.GetType().Name}'. QueryPredicate is a closed tree.",
                    nameof(node)),
            };
        }

        private UnaryExpression? VisitNot(NotPredicate node, int depth)
        {
            Expression? operand = Visit(node.Operand, depth + 1);
            return operand is null ? null : Expression.Not(operand);
        }

        private Expression? VisitComposite(
            IReadOnlyList<QueryPredicate> operands,
            int depth,
            Func<Expression, Expression, BinaryExpression> combine)
        {
            // The factories require >= 1 operand; an empty list is only reachable by direct
            // record construction. Identity semantics would silently widen (empty AND = true)
            // or empty (empty OR = false) the result, so reject it as a programming error.
            if (operands.Count == 0)
            {
                throw new ArgumentException(
                    "And/Or predicates require at least one operand. Use the QueryPredicate.And/Or factories.",
                    nameof(operands));
            }

            Expression? combined = null;
            bool failed = false;

            foreach (QueryPredicate operand in operands)
            {
                Expression? body = Visit(operand, depth + 1);
                if (body is null)
                {
                    failed = true; // Keep visiting: collect ALL violations before throwing.
                    continue;
                }

                combined = combined is null ? body : combine(combined, body);
            }

            return failed ? null : combined;
        }

        private Expression? VisitLeaf(FilterCriteria criteria)
        {
            _leafCount++;
            if (_leafCount > QueryPredicate.MaxLeafCount)
            {
                if (!_leafCountReported)
                {
                    _leafCountReported = true;
                    Errors.Add(new QueryPredicateError(
                        Field: null,
                        QueryPredicateErrorCodes.TooManyCriteria,
                        $"The predicate tree exceeds the maximum of {QueryPredicate.MaxLeafCount} leaf criteria."));
                }

                return null;
            }

            if (!_columns.TryGetValue(criteria.Field, out ColumnDescriptor? column))
            {
                Errors.Add(new QueryPredicateError(
                    criteria.Field,
                    QueryPredicateErrorCodes.UnknownField,
                    $"Field '{criteria.Field}' is not declared on the query definition."));
                return null;
            }

            if (!column.IsFilterable)
            {
                Errors.Add(new QueryPredicateError(
                    criteria.Field,
                    QueryPredicateErrorCodes.FieldNotFilterable,
                    $"Field '{criteria.Field}' is declared but not filterable."));
                return null;
            }

            bool isNullable = FilterOperatorInference.IsNullableColumnType(column.ClrType);

            if (criteria.Operator is FilterOperator.IsNull or FilterOperator.IsNotNull && !isNullable)
            {
                Errors.Add(new QueryPredicateError(
                    criteria.Field,
                    QueryPredicateErrorCodes.NullCheckOnNonNullable,
                    $"Field '{criteria.Field}' is a non-nullable {column.ClrType.Name} column and can never be NULL."));
                return null;
            }

            IReadOnlyList<FilterOperator> allowed = FilterOperatorInference.GetOperators(column.ClrType, isNullable);
            if (!allowed.Contains(criteria.Operator))
            {
                Errors.Add(new QueryPredicateError(
                    criteria.Field,
                    QueryPredicateErrorCodes.OperatorNotAllowed,
                    $"Operator '{criteria.Operator}' is not allowed on field '{criteria.Field}' ({column.ClrType.Name})."));
                return null;
            }

            Expression<Func<TEntity, bool>>? built =
                FilterExpressionBuilder.Build<TEntity>(criteria, _logger, _shadowColumns);

            if (built is null)
            {
                // The lenient builder drops a leaf it cannot translate (unconvertible value,
                // value-object range restriction, misdeclared column). Inside OR/NOT a vanished
                // leaf changes result semantics, so strict mode surfaces it instead.
                Errors.Add(new QueryPredicateError(
                    criteria.Field,
                    QueryPredicateErrorCodes.ValueNotConvertible,
                    $"Value '{criteria.Value}' cannot be applied to field '{criteria.Field}' " +
                    $"({column.ClrType.Name}) with operator '{criteria.Operator}'."));
                return null;
            }

            // Rebind the independently built lambda onto the single shared parameter.
            return new ParameterReplacer(built.Parameters[0], Parameter).Visit(built.Body);
        }
    }
}
