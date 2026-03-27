using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class TagListPiiAnalyzerTests
{
    /// <summary>
    /// Minimal stub for <c>System.Diagnostics.TagList</c> — a value-type collection
    /// used by OpenTelemetry metrics. Only the struct name and namespace matter for
    /// the semantic-model type check in the analyzer.
    /// </summary>
    private const string TagListStub = """
        namespace System.Diagnostics
        {
            public struct TagList : System.Collections.IEnumerable
            {
                public void Add(System.Collections.Generic.KeyValuePair<string, object> tag) { }
                public System.Collections.IEnumerator GetEnumerator() => null;
            }
        }
        """;

    [Fact]
    public async Task GRSEC010_fires_on_email_tag_key()
    {
        string source = """
            using System.Diagnostics;

            public class Metrics
            {
                public void Record()
                {
                    var tags = new TagList
                    {
                        { "user_email", "test@example.com" }
                    };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TagListPiiAnalyzer>(
                source, [TagListStub], TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == TagListPiiAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC010_fires_on_phone_number_tag_key()
    {
        string source = """
            using System.Diagnostics;

            public class Metrics
            {
                public void Record()
                {
                    var tags = new TagList
                    {
                        { "phone_number", "+32123456789" }
                    };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TagListPiiAnalyzer>(
                source, [TagListStub], TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == TagListPiiAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC010_silent_on_tenant_id_tag_key()
    {
        string source = """
            using System.Diagnostics;

            public class Metrics
            {
                public void Record()
                {
                    var tags = new TagList
                    {
                        { "tenant_id", "abc-123" }
                    };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TagListPiiAnalyzer>(
                source, [TagListStub], TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TagListPiiAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC010_silent_on_http_status_tag_key()
    {
        string source = """
            using System.Diagnostics;

            public class Metrics
            {
                public void Record()
                {
                    var tags = new TagList
                    {
                        { "http_status", "200" }
                    };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TagListPiiAnalyzer>(
                source, [TagListStub], TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TagListPiiAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC010_silent_when_value_is_variable_with_pii_but_key_is_clean()
    {
        string source = """
            using System.Diagnostics;

            public class Metrics
            {
                public void Record()
                {
                    string emailValue = "test@example.com";
                    var tags = new TagList
                    {
                        { "category", emailValue }
                    };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TagListPiiAnalyzer>(
                source, [TagListStub], TestContext.Current.CancellationToken);

        // The analyzer only inspects string literals inside the initializer.
        // A variable reference (emailValue) is not a string literal, so it
        // is not checked — only the key "category" is inspected.
        diagnostics.Where(d => d.Id == TagListPiiAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC010_silent_on_empty_tag_list()
    {
        string source = """
            using System.Diagnostics;

            public class Metrics
            {
                public void Record()
                {
                    var tags = new TagList { };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<TagListPiiAnalyzer>(
                source, [TagListStub], TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == TagListPiiAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
