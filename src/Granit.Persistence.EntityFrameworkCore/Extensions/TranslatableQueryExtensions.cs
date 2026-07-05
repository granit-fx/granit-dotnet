using System.Linq.Expressions;
using Granit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// LINQ extensions for querying <see cref="ITranslatable{TTranslation}"/> entities
/// with culture-aware filtering, ordering, and eager loading.
/// </summary>
/// <remarks>
/// All extensions produce EF Core-translatable expression trees.
/// Compatible with both PostgreSQL and SQL Server providers.
/// </remarks>
public static class TranslatableQueryExtensions
{
    /// <summary>
    /// Includes translations for the specified culture. If no culture is
    /// specified, includes all translations.
    /// </summary>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <typeparam name="TTranslation">The translation entity type.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="culture">
    /// Optional BCP 47 culture to filter. When <c>null</c>, all translations are included.
    /// </param>
    /// <returns>The query with translations eagerly loaded.</returns>
    public static IQueryable<TEntity> IncludeTranslations<TEntity, TTranslation>(
        this IQueryable<TEntity> query,
        string? culture = null)
        where TEntity : class, ITranslatable<TTranslation>
        where TTranslation : class, ITranslation
    {
        if (culture is null)
        {
            return query.Include(e => e.Translations);
        }

        return query.Include(e => e.Translations.Where(t => t.Culture == culture));
    }

    /// <summary>
    /// Filters entities where a translation property matches a predicate
    /// for the specified culture.
    /// </summary>
    /// <remarks>
    /// Translates to SQL:
    /// <c>WHERE EXISTS (SELECT 1 FROM Translations t WHERE t.ParentId = e.Id AND t.Culture = @c AND ...)</c>
    /// </remarks>
    /// <example>
    /// <code>
    /// query.WhereTranslation&lt;Document, DocumentTranslation&gt;(
    ///     "fr", t => t.Title.Contains("rapport"))
    /// </code>
    /// </example>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <typeparam name="TTranslation">The translation entity type.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="culture">The BCP 47 culture to filter on.</param>
    /// <param name="predicate">The predicate to apply on the translation.</param>
    /// <returns>The filtered query.</returns>
    public static IQueryable<TEntity> WhereTranslation<TEntity, TTranslation>(
        this IQueryable<TEntity> query,
        string culture,
        Expression<Func<TTranslation, bool>> predicate)
        where TEntity : class, ITranslatable<TTranslation>
        where TTranslation : class, ITranslation
    {
        // Reuse the predicate's parameter (t) to add: t.Culture == culture
        ParameterExpression translationParam = predicate.Parameters[0];

        BinaryExpression cultureMatch = Expression.Equal(
            Expression.Property(translationParam, nameof(ITranslation.Culture)),
            Expression.Constant(culture));

        // Combine: t.Culture == culture && predicate(t)
        BinaryExpression combinedBody = Expression.AndAlso(cultureMatch, predicate.Body);

        var combinedPredicate =
            Expression.Lambda<Func<TTranslation, bool>>(combinedBody, translationParam);

        // Build: e => e.Translations.Any(combinedPredicate)
        ParameterExpression entityParam = Expression.Parameter(typeof(TEntity), "e");

        MemberExpression translationsProperty = Expression.Property(
            entityParam,
            typeof(ITranslatable<TTranslation>).GetProperty(nameof(ITranslatable<TTranslation>.Translations))!);

        MethodCallExpression anyCall = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Any),
            [typeof(TTranslation)],
            translationsProperty,
            combinedPredicate);

        var whereExpression =
            Expression.Lambda<Func<TEntity, bool>>(anyCall, entityParam);

        return query.Where(whereExpression);
    }

    /// <summary>
    /// Orders entities by a translation property for the specified culture.
    /// Entities without a translation in the requested culture sort last.
    /// </summary>
    /// <remarks>
    /// Translates to SQL: <code>ORDER BY (SELECT t.Column FROM Translations t
    /// WHERE t.ParentId = e.Id AND t.Culture = @c LIMIT 1)</code>
    /// </remarks>
    /// <example>
    /// <code>
    /// query.OrderByTranslation&lt;Document, DocumentTranslation, string&gt;(
    ///     "fr", t => t.Title)
    /// </code>
    /// </example>
    /// <typeparam name="TEntity">The parent entity type.</typeparam>
    /// <typeparam name="TTranslation">The translation entity type.</typeparam>
    /// <typeparam name="TKey">The ordering key type.</typeparam>
    /// <param name="query">The source query.</param>
    /// <param name="culture">The BCP 47 culture to order by.</param>
    /// <param name="keySelector">The property selector on the translation.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<TEntity> OrderByTranslation<TEntity, TTranslation, TKey>(
        this IQueryable<TEntity> query,
        string culture,
        Expression<Func<TTranslation, TKey>> keySelector)
        where TEntity : class, ITranslatable<TTranslation>
        where TTranslation : class, ITranslation
    {
        ParameterExpression entityParam = Expression.Parameter(typeof(TEntity), "e");
        ParameterExpression translationParam = Expression.Parameter(typeof(TTranslation), "t");

        // t.Culture == culture
        BinaryExpression cultureMatch = Expression.Equal(
            Expression.Property(translationParam, nameof(ITranslation.Culture)),
            Expression.Constant(culture));

        var filterLambda =
            Expression.Lambda<Func<TTranslation, bool>>(cultureMatch, translationParam);

        MemberExpression translationsProperty = Expression.Property(
            entityParam,
            typeof(ITranslatable<TTranslation>).GetProperty(nameof(ITranslatable<TTranslation>.Translations))!);

        // e.Translations.Where(t => t.Culture == culture)
        MethodCallExpression whereCall = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Where),
            [typeof(TTranslation)],
            translationsProperty,
            filterLambda);

        // .Select(keySelector)
        MethodCallExpression selectCall = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Select),
            [typeof(TTranslation), typeof(TKey)],
            whereCall,
            keySelector);

        // .FirstOrDefault()
        MethodCallExpression firstOrDefaultCall = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.FirstOrDefault),
            [typeof(TKey)],
            selectCall);

        var orderExpression =
            Expression.Lambda<Func<TEntity, TKey>>(firstOrDefaultCall, entityParam);

        return query.OrderBy(orderExpression);
    }
}
