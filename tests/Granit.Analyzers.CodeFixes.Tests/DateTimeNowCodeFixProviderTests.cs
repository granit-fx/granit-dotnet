using Xunit;

namespace Granit.Analyzers.CodeFixes.Tests;

public sealed class DateTimeNowCodeFixProviderTests
{
    [Fact]
    public async Task Replaces_DateTime_Now_and_injects_IClock()
    {
        string source = """
            using System;
            public class MyService
            {
                public MyService() { }
                public void DoWork()
                {
                    DateTimeOffset now = DateTime.Now;
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Timing;

            public class MyService
            {
                private readonly IClock _clock;

                public MyService(IClock clock)
                {
                    _clock = clock;
                }
                public void DoWork()
                {
                    DateTimeOffset now = _clock.Now;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DateTimeNowAnalyzer, DateTimeNowCodeFixProvider>(
            source, expected);
    }

    [Fact]
    public async Task Replaces_DateTimeOffset_UtcNow_and_injects_IClock()
    {
        string source = """
            using System;
            public class MyService
            {
                public MyService() { }
                public void DoWork()
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Timing;

            public class MyService
            {
                private readonly IClock _clock;

                public MyService(IClock clock)
                {
                    _clock = clock;
                }
                public void DoWork()
                {
                    DateTimeOffset now = _clock.Now;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DateTimeNowAnalyzer, DateTimeNowCodeFixProvider>(
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
                    DateTimeOffset now = DateTime.Now;
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Timing;

            public class MyService
            {
                private readonly IClock _clock;

                public MyService(IClock clock)
                {
                    _clock = clock;
                }

                public void DoWork()
                {
                    DateTimeOffset now = _clock.Now;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DateTimeNowAnalyzer, DateTimeNowCodeFixProvider>(
            source, expected);
    }

    [Fact]
    public async Task Does_not_duplicate_field_when_clock_already_exists()
    {
        string source = """
            using System;
            public class MyService
            {
                private readonly IClock _clock;
                public MyService(IClock clock) { _clock = clock; }
                public void DoWork()
                {
                    DateTimeOffset now = DateTime.Now;
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Timing;

            public class MyService
            {
                private readonly IClock _clock;
                public MyService(IClock clock) { _clock = clock; }
                public void DoWork()
                {
                    DateTimeOffset now = _clock.Now;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DateTimeNowAnalyzer, DateTimeNowCodeFixProvider>(
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
                    DateTimeOffset now = DateTime.Now;
                }
            }
            """;

        string expected = """
            using System;
            using Granit.Timing;

            public class MyService
            {
                public static void DoWork()
                {
                    DateTimeOffset now = _clock.Now;
                }
            }
            """;

        await CodeFixTestHelpers.VerifyCodeFixAsync<DateTimeNowAnalyzer, DateTimeNowCodeFixProvider>(
            source, expected);
    }
}
