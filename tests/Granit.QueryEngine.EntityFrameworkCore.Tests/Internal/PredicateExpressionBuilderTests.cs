using System.Linq.Expressions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Filtering.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class PredicateExpressionBuilderTests
{
    private static QueryDefinitionBuilder<TestProduct> CreateBuilder()
    {
        QueryDefinitionBuilder<TestProduct> builder = new();
        builder
            .Column(p => p.Name, c => c.Filterable())
            .Column(p => p.Price, c => c.Filterable())
            .Column(p => p.Category, c => c.Filterable())
            .Column(p => p.CreatedAt, c => c.Filterable())
            .Column(p => p.Amount); // declared but NOT filterable
        return builder;
    }

    private static List<TestProduct> SampleProducts() =>
    [
        new() { Name = "Laptop", Price = 1000, Category = ProductCategory.Electronics, CreatedAt = DateTimeOffset.UtcNow },
        new() { Name = "Novel", Price = 15, Category = ProductCategory.Books, CreatedAt = null },
        new() { Name = "Phone", Price = 800, Category = ProductCategory.Electronics, CreatedAt = DateTimeOffset.UtcNow },
        new() { Name = "T-Shirt", Price = 25, Category = ProductCategory.Clothing, CreatedAt = null },
    ];

    [Fact]
    public void Single_criterion_filters_rows()
    {
        var predicate = QueryPredicate.Criterion("Name", FilterOperator.Eq, "Laptop");

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.ShouldHaveSingleItem().Name.ShouldBe("Laptop");
    }

    [Fact]
    public void And_composition_requires_all_criteria()
    {
        var predicate = QueryPredicate.And(
            QueryPredicate.Criterion("Category", FilterOperator.Eq, "Electronics"),
            QueryPredicate.Criterion("Price", FilterOperator.Gt, "900"));

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.ShouldHaveSingleItem().Name.ShouldBe("Laptop");
    }

    [Fact]
    public void Or_composition_matches_any_criterion()
    {
        var predicate = QueryPredicate.Or(
            QueryPredicate.Criterion("Name", FilterOperator.Eq, "Novel"),
            QueryPredicate.Criterion("Price", FilterOperator.Gt, "900"));

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.Select(p => p.Name).ShouldBe(["Laptop", "Novel"], ignoreOrder: true);
    }

    [Fact]
    public void Not_negates_criterion()
    {
        var predicate = QueryPredicate.Not(
            QueryPredicate.Criterion("Category", FilterOperator.Eq, "Electronics"));

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.Select(p => p.Name).ShouldBe(["Novel", "T-Shirt"], ignoreOrder: true);
    }

    [Fact]
    public void Nested_tree_composes_with_shared_parameter()
    {
        var predicate = QueryPredicate.Or(
            QueryPredicate.And(
                QueryPredicate.Criterion("Category", FilterOperator.Eq, "Electronics"),
                QueryPredicate.Not(QueryPredicate.Criterion("Price", FilterOperator.Lt, "900"))),
            QueryPredicate.Criterion("Name", FilterOperator.StartsWith, "T-"));

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        // One shared lambda parameter, no scoping artifacts: translatable and evaluable.
        expr.Parameters.ShouldHaveSingleItem();
        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.Select(p => p.Name).ShouldBe(["Laptop", "T-Shirt"], ignoreOrder: true);
    }

    [Fact]
    public void Ne_matches_null_columns_two_valued_semantics()
    {
        var predicate = QueryPredicate.Criterion("CreatedAt", FilterOperator.Ne, "2024-01-01T00:00:00Z");

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        // NULL CreatedAt rows match Ne — C# two-valued semantics.
        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.Count.ShouldBe(4);
    }

    [Fact]
    public void IsNull_and_IsNotNull_on_nullable_column()
    {
        Expression<Func<TestProduct, bool>> isNull = PredicateExpressionBuilder.BuildStrict(
            QueryPredicate.Criterion("CreatedAt", FilterOperator.IsNull), CreateBuilder());
        Expression<Func<TestProduct, bool>> isNotNull = PredicateExpressionBuilder.BuildStrict(
            QueryPredicate.Criterion("CreatedAt", FilterOperator.IsNotNull), CreateBuilder());

        SampleProducts().AsQueryable().Where(isNull).Select(p => p.Name)
            .ShouldBe(["Novel", "T-Shirt"], ignoreOrder: true);
        SampleProducts().AsQueryable().Where(isNotNull).Select(p => p.Name)
            .ShouldBe(["Laptop", "Phone"], ignoreOrder: true);
    }

    [Fact]
    public void IsNull_on_string_reference_column()
    {
        var predicate = QueryPredicate.Criterion("Name", FilterOperator.IsNull);

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        List<TestProduct> products = [new() { Name = null! }, new() { Name = "Alice" }];
        products.AsQueryable().Where(expr).Count().ShouldBe(1);
    }

    [Fact]
    public void Not_Contains_matches_null_columns_two_valued_semantics()
    {
        // PINNED: Not(Contains(...)) follows C#/EF two-valued semantics. Contains guards
        // `column != null && column.Contains(v)`, so its negation is TRUE for NULL columns.
        // Adapters needing SQL three-valued behavior must add IsNotNull explicitly.
        var predicate = QueryPredicate.Not(
            QueryPredicate.Criterion("Name", FilterOperator.Contains, "o"));

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        List<TestProduct> products =
        [
            new() { Name = null! },
            new() { Name = "Laptop" },
            new() { Name = "T-Shirt" },
        ];

        var result = products.AsQueryable().Where(expr).ToList();
        result.Count.ShouldBe(2); // NULL name + "T-Shirt"; "Laptop" contains 'o'.
        result.ShouldContain(p => p.Name == null);
    }

    [Fact]
    public void Unknown_field_throws_with_UnknownField_code()
    {
        var predicate = QueryPredicate.Criterion("Ghost", FilterOperator.Eq, "x");

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        QueryPredicateError error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(QueryPredicateErrorCodes.UnknownField);
        error.Field.ShouldBe("Ghost");
    }

    [Fact]
    public void Non_filterable_field_throws_with_FieldNotFilterable_code()
    {
        var predicate = QueryPredicate.Criterion("Amount", FilterOperator.Gt, "10");

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.FieldNotFilterable);
    }

    [Fact]
    public void Disallowed_operator_throws_with_OperatorNotAllowed_code()
    {
        var predicate = QueryPredicate.Criterion("Price", FilterOperator.Contains, "10");

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.OperatorNotAllowed);
    }

    [Fact]
    public void Unconvertible_value_throws_with_ValueNotConvertible_code()
    {
        var predicate = QueryPredicate.Criterion("Price", FilterOperator.Eq, "not-a-number");

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.ValueNotConvertible);
    }

    [Fact]
    public void Leaf_that_would_vanish_inside_Or_throws_instead()
    {
        // The lenient path would drop the unconvertible leaf; inside an OR that silently turns
        // "Price == garbage OR Name == Novel" into "Name == Novel" — a DIFFERENT predicate that
        // can only be caught by throwing. Strict mode throws ValueNotConvertible.
        var predicate = QueryPredicate.Or(
            QueryPredicate.Criterion("Price", FilterOperator.Eq, "not-a-number"),
            QueryPredicate.Criterion("Name", FilterOperator.Eq, "Novel"));

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.ValueNotConvertible);
    }

    [Fact]
    public void Null_check_on_non_nullable_column_throws_with_NullCheckOnNonNullable_code()
    {
        var predicate = QueryPredicate.Criterion("Price", FilterOperator.IsNull);

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.NullCheckOnNonNullable);
    }

    [Fact]
    public void Tree_deeper_than_MaxDepth_throws_with_TreeTooDeep_code()
    {
        var predicate = QueryPredicate.Criterion("Name", FilterOperator.Eq, "x");
        for (int i = 0; i <= QueryPredicate.MaxDepth; i++)
        {
            predicate = QueryPredicate.Not(predicate);
        }

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.TreeTooDeep);
    }

    [Fact]
    public void Tree_at_MaxDepth_is_accepted()
    {
        var predicate = QueryPredicate.Criterion("Name", FilterOperator.Eq, "Laptop");
        for (int i = 0; i < QueryPredicate.MaxDepth - 1; i++)
        {
            predicate = QueryPredicate.Not(predicate);
        }

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        // 31 negations flip the leaf an odd number of times: everything BUT Laptop.
        var result = SampleProducts().AsQueryable().Where(expr).ToList();
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void More_leaves_than_MaxLeafCount_throws_with_TooManyCriteria_code()
    {
        QueryPredicate[] leaves = [.. Enumerable.Range(0, QueryPredicate.MaxLeafCount + 1)
            .Select(i => QueryPredicate.Criterion("Price", FilterOperator.Ne, i.ToString()))];
        var predicate = QueryPredicate.And(leaves);

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.TooManyCriteria);
    }

    [Fact]
    public void All_violations_are_collected_before_throwing()
    {
        var predicate = QueryPredicate.And(
            QueryPredicate.Criterion("Ghost", FilterOperator.Eq, "x"),
            QueryPredicate.Or(
                QueryPredicate.Criterion("Amount", FilterOperator.Gt, "10"),
                QueryPredicate.Criterion("Price", FilterOperator.Contains, "10")),
            QueryPredicate.Criterion("Price", FilterOperator.Eq, "not-a-number"));

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));

        exception.Errors.Count.ShouldBe(4);
        exception.Errors.Select(e => e.Code).ShouldBe(
            [
                QueryPredicateErrorCodes.UnknownField,
                QueryPredicateErrorCodes.FieldNotFilterable,
                QueryPredicateErrorCodes.OperatorNotAllowed,
                QueryPredicateErrorCodes.ValueNotConvertible,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Field_lookup_is_case_insensitive()
    {
        var predicate = QueryPredicate.Criterion("name", FilterOperator.Eq, "Laptop");

        Expression<Func<TestProduct, bool>> expr =
            PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder());

        SampleProducts().AsQueryable().Where(expr).Count().ShouldBe(1);
    }

    [Fact]
    public void Directly_constructed_empty_And_is_rejected()
    {
        // Only reachable by bypassing the factories; identity semantics (empty AND = true)
        // would silently widen the result, so it is rejected as a programming error.
        AndPredicate predicate = new([]);

        Should.Throw<ArgumentException>(
            () => PredicateExpressionBuilder.BuildStrict(predicate, CreateBuilder()));
    }
}
