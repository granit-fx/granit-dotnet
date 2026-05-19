using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class GuidNewGuidCodeFixProviderTests
{
    [Fact]
    public async Task Replaces_Guid_NewGuid_and_injects_IGuidGenerator()
    {
        string source = """
            using System;
            public class MyService
            {
                public MyService() { }
                public void DoWork()
                {
                    Guid id = Guid.NewGuid();
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Guids;

            public class MyService
            {
                private readonly IGuidGenerator _guidGenerator;

                public MyService(IGuidGenerator guidGenerator)
                {
                    _guidGenerator = guidGenerator;
                }
                public void DoWork()
                {
                    Guid id = _guidGenerator.Create();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<GuidNewGuidAnalyzer, GuidNewGuidCodeFixProvider>(
            source, expected);
    }

    [Fact]
    public async Task Creates_constructor_when_none_exists()
    {
        string source = """
            using System;
            public class MyService
            {
                public void DoWork()
                {
                    Guid id = Guid.NewGuid();
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Guids;

            public class MyService
            {
                private readonly IGuidGenerator _guidGenerator;

                public MyService(IGuidGenerator guidGenerator)
                {
                    _guidGenerator = guidGenerator;
                }

                public void DoWork()
                {
                    Guid id = _guidGenerator.Create();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<GuidNewGuidAnalyzer, GuidNewGuidCodeFixProvider>(
            source, expected);
    }

    [Fact]
    public async Task Does_not_duplicate_field_when_guidGenerator_already_exists()
    {
        string source = """
            using System;
            public class MyService
            {
                private readonly IGuidGenerator _guidGenerator;
                public MyService(IGuidGenerator guidGenerator) { _guidGenerator = guidGenerator; }
                public void DoWork()
                {
                    Guid id = Guid.NewGuid();
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Guids;

            public class MyService
            {
                private readonly IGuidGenerator _guidGenerator;
                public MyService(IGuidGenerator guidGenerator) { _guidGenerator = guidGenerator; }
                public void DoWork()
                {
                    Guid id = _guidGenerator.Create();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<GuidNewGuidAnalyzer, GuidNewGuidCodeFixProvider>(
            source, expected);
    }

    [Fact]
    public async Task Static_method_replaces_expression_without_injection()
    {
        string source = """
            using System;
            public class MyService
            {
                public static void DoWork()
                {
                    Guid id = Guid.NewGuid();
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Guids;

            public class MyService
            {
                public static void DoWork()
                {
                    Guid id = _guidGenerator.Create();
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<GuidNewGuidAnalyzer, GuidNewGuidCodeFixProvider>(
            source, expected);
    }
}
