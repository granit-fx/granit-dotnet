using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class FilterOperatorInferenceTests
{
    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(double))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DayOfWeek))]
    [InlineData(typeof(Guid))]
    public void Every_Type_Family_Offers_Ne(Type clrType)
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(clrType);

        operators.ShouldContain(FilterOperator.Ne);
    }

    [Fact]
    public void Unsupported_Type_Offers_No_Operators()
    {
        FilterOperatorInference.GetOperators(typeof(Uri)).ShouldBeEmpty();
    }

    [Fact]
    public void Single_Argument_Overload_Never_Offers_Null_Checks()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(string));

        operators.ShouldNotContain(FilterOperator.IsNull);
        operators.ShouldNotContain(FilterOperator.IsNotNull);
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(DateTimeOffset?))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(DayOfWeek?))]
    [InlineData(typeof(Guid?))]
    public void Nullable_Column_Offers_Null_Checks(Type clrType)
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(
            clrType, FilterOperatorInference.IsNullableColumnType(clrType));

        operators.ShouldContain(FilterOperator.IsNull);
        operators.ShouldContain(FilterOperator.IsNotNull);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DayOfWeek))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    public void Non_Nullable_Value_Type_Column_Does_Not_Offer_Null_Checks(Type clrType)
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(
            clrType, FilterOperatorInference.IsNullableColumnType(clrType));

        operators.ShouldNotContain(FilterOperator.IsNull);
        operators.ShouldNotContain(FilterOperator.IsNotNull);
    }

    [Fact]
    public void Nullable_Overload_Preserves_Base_Operator_Set()
    {
        IReadOnlyList<FilterOperator> baseOperators = FilterOperatorInference.GetOperators(typeof(int?));
        IReadOnlyList<FilterOperator> nullableOperators = FilterOperatorInference.GetOperators(typeof(int?), isNullableColumn: true);

        nullableOperators.Take(baseOperators.Count).ShouldBe(baseOperators);
        nullableOperators.Count.ShouldBe(baseOperators.Count + 2);
    }

    [Fact]
    public void Nullable_Overload_On_Unsupported_Type_Stays_Empty()
    {
        // A nullable column of an unsupported type still filters nothing — null checks are
        // only offered on top of a non-empty base operator set.
        FilterOperatorInference.GetOperators(typeof(Uri), isNullableColumn: true).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(Guid?), true)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(Guid), false)]
    [InlineData(typeof(DayOfWeek), false)]
    public void IsNullableColumnType_Distinguishes_Reference_And_Nullable_Value_Types(Type clrType, bool expected)
    {
        FilterOperatorInference.IsNullableColumnType(clrType).ShouldBe(expected);
    }
}
