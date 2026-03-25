using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Filtering;

public sealed class FilterOperatorInferenceTests
{
    [Fact]
    public void String_returns_string_operators()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(string));

        operators.ShouldBe([
            FilterOperator.Eq,
            FilterOperator.Contains,
            FilterOperator.StartsWith,
            FilterOperator.EndsWith,
            FilterOperator.In,
        ]);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(short))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(double))]
    [InlineData(typeof(float))]
    public void Numeric_types_return_numeric_operators(Type type)
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(type);

        operators.ShouldContain(FilterOperator.Eq);
        operators.ShouldContain(FilterOperator.Gt);
        operators.ShouldContain(FilterOperator.Gte);
        operators.ShouldContain(FilterOperator.Lt);
        operators.ShouldContain(FilterOperator.Lte);
        operators.ShouldContain(FilterOperator.Between);
        operators.ShouldContain(FilterOperator.In);
    }

    [Theory]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(TimeOnly))]
    public void Date_types_return_date_operators(Type type)
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(type);

        operators.ShouldContain(FilterOperator.Eq);
        operators.ShouldContain(FilterOperator.Gt);
        operators.ShouldContain(FilterOperator.Gte);
        operators.ShouldContain(FilterOperator.Lt);
        operators.ShouldContain(FilterOperator.Lte);
        operators.ShouldContain(FilterOperator.Between);
    }

    [Fact]
    public void Bool_returns_eq_only()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(bool));

        operators.ShouldBe([FilterOperator.Eq]);
    }

    [Fact]
    public void Enum_returns_eq_and_in()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(DayOfWeek));

        operators.ShouldBe([FilterOperator.Eq, FilterOperator.In]);
    }

    [Fact]
    public void Guid_returns_eq_and_in()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(Guid));

        operators.ShouldBe([FilterOperator.Eq, FilterOperator.In]);
    }

    [Fact]
    public void Nullable_int_returns_numeric_operators()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(int?));

        operators.ShouldContain(FilterOperator.Eq);
        operators.ShouldContain(FilterOperator.Gt);
        operators.ShouldContain(FilterOperator.Between);
    }

    [Fact]
    public void Nullable_DateTime_returns_date_operators()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(DateTime?));

        operators.ShouldContain(FilterOperator.Eq);
        operators.ShouldContain(FilterOperator.Between);
    }

    [Fact]
    public void Nullable_bool_returns_eq_only()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(bool?));

        operators.ShouldBe([FilterOperator.Eq]);
    }

    [Fact]
    public void Nullable_enum_returns_eq_and_in()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(DayOfWeek?));

        operators.ShouldBe([FilterOperator.Eq, FilterOperator.In]);
    }

    [Fact]
    public void Nullable_Guid_returns_eq_and_in()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(Guid?));

        operators.ShouldBe([FilterOperator.Eq, FilterOperator.In]);
    }

    [Fact]
    public void Unknown_type_returns_empty_list()
    {
        IReadOnlyList<FilterOperator> operators = FilterOperatorInference.GetOperators(typeof(object));

        operators.ShouldBeEmpty();
    }

    [Fact]
    public void Null_type_throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            FilterOperatorInference.GetOperators(null!));
    }
}
