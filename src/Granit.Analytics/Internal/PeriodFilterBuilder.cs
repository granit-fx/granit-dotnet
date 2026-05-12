using System.Linq.Expressions;
using Granit.Timing;

namespace Granit.Analytics.Internal;

/// <summary>
/// Builds an EF Core-translatable <c>Where</c> predicate constraining a queryable
/// to a half-open <c>[from, to)</c> window using a property selector.
/// </summary>
internal static class PeriodFilterBuilder
{
    public static IQueryable<TEntity> ApplyPeriod<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, DateTimeOffset>> selector,
        ResolvedPeriod period)
        where TEntity : class
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");

        // Inline the selector body into our predicate (EF Core does not always translate
        // Expression.Invoke; manual parameter substitution is the safe path).
        Expression body = ParameterReplacer.Replace(selector.Body, selector.Parameters[0], parameter);

        Expression greaterThanOrEqual = Expression.GreaterThanOrEqual(
            body,
            Expression.Constant(period.From, typeof(DateTimeOffset)));
        Expression lessThan = Expression.LessThan(
            body,
            Expression.Constant(period.To, typeof(DateTimeOffset)));

        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(greaterThanOrEqual, lessThan),
            parameter);

        return source.Where(predicate);
    }

    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        private readonly ParameterExpression _source = source;
        private readonly ParameterExpression _target = target;

        public static Expression Replace(Expression expression, ParameterExpression source, ParameterExpression target) =>
            new ParameterReplacer(source, target).Visit(expression);

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _source ? _target : base.VisitParameter(node);
    }
}
