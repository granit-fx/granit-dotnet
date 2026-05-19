using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class DateTimeNowAnalyzerTests
{
    [Fact]
    public async Task GRSEC001_fires_on_DateTime_Now()
    {
        string source = """
            using System;

            public class Service
            {
                public DateTime GetTime() => DateTime.Now;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DateTimeNowAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC001_fires_on_DateTime_UtcNow()
    {
        string source = """
            using System;

            public class Service
            {
                public DateTime GetTime() => DateTime.UtcNow;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DateTimeNowAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC001_fires_on_DateTimeOffset_Now()
    {
        string source = """
            using System;

            public class Service
            {
                public DateTimeOffset GetTime() => DateTimeOffset.Now;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DateTimeNowAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC001_fires_on_DateTimeOffset_UtcNow()
    {
        string source = """
            using System;

            public class Service
            {
                public DateTimeOffset GetTime() => DateTimeOffset.UtcNow;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DateTimeNowAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC001_silent_on_custom_IClock_Now()
    {
        string source = """
            using System;

            public interface IClock
            {
                DateTimeOffset Now { get; }
            }

            public class Service
            {
                private readonly IClock _clock;
                public Service(IClock clock) { _clock = clock; }
                public DateTimeOffset GetTime() => _clock.Now;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DateTimeNowAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC001_silent_on_custom_class_named_DateTime()
    {
        string source = """
            namespace MyApp
            {
                public static class DateTime
                {
                    public static string Now => "custom";
                }

                public class Service
                {
                    public string GetTime() => DateTime.Now;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DateTimeNowAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC001_fires_in_property_initializer()
    {
        string source = """
            using System;

            public class Entity
            {
                public DateTime CreatedAt { get; set; } = DateTime.Now;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DateTimeNowAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC001_reports_multiple_occurrences()
    {
        string source = """
            using System;

            public class Service
            {
                public DateTime GetLocal() => DateTime.Now;
                public DateTimeOffset GetOffset() => DateTimeOffset.UtcNow;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DateTimeNowAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DateTimeNowAnalyzer.DiagnosticId)
            .Count().ShouldBe(2);
    }
}
