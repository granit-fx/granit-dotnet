using System.Linq.Expressions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// Issue #2767: a substring filter (contains/startsWith/endsWith) on a non-string column —
// including a SingleValueObject<string> mapped via a ValueConverter — cannot translate to LIKE.
// The builder must drop the criterion AND log a warning so it is observable, not silent.
public sealed class FilterExpressionBuilderNonStringSubstringTests
{
    [Theory]
    [InlineData(FilterOperator.Contains)]
    [InlineData(FilterOperator.StartsWith)]
    [InlineData(FilterOperator.EndsWith)]
    public void Substring_on_non_string_column_returns_null_and_logs_warning(FilterOperator op)
    {
        CapturingLogger logger = new();
        FilterCriteria criteria = new("Price", op, "5");

        Expression<Func<TestProduct, bool>>? expr =
            FilterExpressionBuilder.Build<TestProduct>(criteria, logger);

        expr.ShouldBeNull();
        logger.Levels.ShouldContain(LogLevel.Warning);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
