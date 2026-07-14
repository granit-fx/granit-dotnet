using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Filtering.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryPredicateValidationExceptionTests
{
    [Fact]
    public void Carries_All_Errors()
    {
        QueryPredicateError[] errors =
        [
            new("Ghost", QueryPredicateErrorCodes.UnknownField, "Field 'Ghost' is not declared."),
            new(null, QueryPredicateErrorCodes.TreeTooDeep, "Too deep."),
        ];

        QueryPredicateValidationException exception = new(errors);

        exception.Errors.ShouldBe(errors);
        exception.ShouldBeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void Message_Summarizes_Every_Error()
    {
        QueryPredicateError[] errors =
        [
            new("Ghost", QueryPredicateErrorCodes.UnknownField, "Field 'Ghost' is not declared."),
            new("Price", QueryPredicateErrorCodes.OperatorNotAllowed, "Operator 'Contains' is not allowed."),
        ];

        QueryPredicateValidationException exception = new(errors);

        exception.Message.ShouldContain("2 error(s)");
        exception.Message.ShouldContain(QueryPredicateErrorCodes.UnknownField);
        exception.Message.ShouldContain(QueryPredicateErrorCodes.OperatorNotAllowed);
        exception.Message.ShouldContain("Ghost");
        exception.Message.ShouldContain("Price");
    }

    [Fact]
    public void Rejects_Empty_Error_List()
    {
        Should.Throw<ArgumentException>(() => new QueryPredicateValidationException([]));
    }
}
