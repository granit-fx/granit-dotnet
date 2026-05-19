using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class GuidNewGuidAnalyzerTests
{
    [Fact]
    public async Task GRSEC002_fires_on_Guid_NewGuid()
    {
        string source = """
            using System;

            public class Service
            {
                public Guid CreateId() => Guid.NewGuid();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<GuidNewGuidAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == GuidNewGuidAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC002_fires_on_Guid_NewGuid_in_field_initializer()
    {
        string source = """
            using System;

            public class Entity
            {
                public Guid Id { get; set; } = Guid.NewGuid();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<GuidNewGuidAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == GuidNewGuidAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC002_silent_on_custom_NewGuid_method()
    {
        string source = """
            using System;

            public class IdFactory
            {
                public Guid NewGuid() => Guid.Empty;
            }

            public class Service
            {
                public Guid CreateId()
                {
                    IdFactory factory = new IdFactory();
                    return factory.NewGuid();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<GuidNewGuidAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == GuidNewGuidAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC002_fires_in_lambda()
    {
        string source = """
            using System;

            public class Service
            {
                public Func<Guid> Factory => () => Guid.NewGuid();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<GuidNewGuidAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == GuidNewGuidAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC002_reports_multiple_occurrences()
    {
        string source = """
            using System;

            public class Service
            {
                public Guid First() => Guid.NewGuid();
                public Guid Second() => Guid.NewGuid();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<GuidNewGuidAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == GuidNewGuidAnalyzer.DiagnosticId)
            .Count().ShouldBe(2);
    }
}
