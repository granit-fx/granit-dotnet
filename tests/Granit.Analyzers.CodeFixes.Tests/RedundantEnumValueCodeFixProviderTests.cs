using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class RedundantEnumValueCodeFixProviderTests
{
    [Fact]
    public async Task Removes_redundant_initializer_from_first_member()
    {
        string source = """
            public enum ChartType
            {
                Bar = 0,
                Line = 1,
                Area = 2,
            }
            """;

        string expected = """
            public enum ChartType
            {
                Bar,
                Line = 1,
                Area = 2,
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<RedundantEnumValueAnalyzer, RedundantEnumValueCodeFixProvider>(
            source, expected);
    }

    [Fact]
    public async Task Removes_redundant_initializer_from_single_member_enum()
    {
        string source = """
            public enum SingleKind
            {
                Only = 0,
            }
            """;

        string expected = """
            public enum SingleKind
            {
                Only,
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<RedundantEnumValueAnalyzer, RedundantEnumValueCodeFixProvider>(
            source, expected);
    }
}
