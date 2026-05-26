using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class PrivacyExportSubjectSubstitutionAnalyzerTests
{
    private const string PrivacyExportContextStub = """
        namespace Granit.Privacy.DataExport
        {
            public sealed record PrivacyExportContext(
                System.Guid RequestId,
                System.Guid SubjectUserId,
                System.Guid CallerUserId,
                System.Guid? TenantId,
                string Regulation);
        }
        """;

    [Fact]
    public async Task GRSEC005_silent_when_subject_and_caller_share_a_symbol()
    {
        string source = """
            using System;
            using Granit.Privacy.DataExport;

            public class Service
            {
                public PrivacyExportContext Create(Guid userId) =>
                    new PrivacyExportContext(
                        RequestId: Guid.Empty,
                        SubjectUserId: userId,
                        CallerUserId: userId,
                        TenantId: null,
                        Regulation: "EU_GDPR");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<PrivacyExportSubjectSubstitutionAnalyzer>(
                source, [PrivacyExportContextStub], TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == PrivacyExportSubjectSubstitutionAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC005_fires_when_subject_and_caller_bind_to_different_symbols()
    {
        string source = """
            using System;
            using Granit.Privacy.DataExport;

            public class Service
            {
                public PrivacyExportContext Create(Guid subject, Guid caller) =>
                    new PrivacyExportContext(
                        RequestId: Guid.Empty,
                        SubjectUserId: subject,
                        CallerUserId: caller,
                        TenantId: null,
                        Regulation: "EU_GDPR");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<PrivacyExportSubjectSubstitutionAnalyzer>(
                source, [PrivacyExportContextStub], TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == PrivacyExportSubjectSubstitutionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC005_fires_on_positional_construction()
    {
        string source = """
            using System;
            using Granit.Privacy.DataExport;

            public class Service
            {
                public PrivacyExportContext Create(Guid subject, Guid caller) =>
                    new PrivacyExportContext(Guid.Empty, subject, caller, null, "EU_GDPR");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<PrivacyExportSubjectSubstitutionAnalyzer>(
                source, [PrivacyExportContextStub], TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == PrivacyExportSubjectSubstitutionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC005_fires_on_implicit_target_typed_new()
    {
        string source = """
            using System;
            using Granit.Privacy.DataExport;

            public class Service
            {
                public PrivacyExportContext Create(Guid subject, Guid caller) =>
                    new(Guid.Empty, subject, caller, null, "EU_GDPR");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<PrivacyExportSubjectSubstitutionAnalyzer>(
                source, [PrivacyExportContextStub], TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == PrivacyExportSubjectSubstitutionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC005_silent_when_both_arguments_are_non_identifier_expressions()
    {
        // Two literal Guid.NewGuid() calls bind to the same method symbol — the
        // analyzer treats this as "same symbol" and stays silent. False-negative
        // tolerated; the alternative noises up every test fixture.
        string source = """
            using System;
            using Granit.Privacy.DataExport;

            public class Service
            {
                public PrivacyExportContext Create() =>
                    new PrivacyExportContext(
                        RequestId: Guid.Empty,
                        SubjectUserId: Guid.NewGuid(),
                        CallerUserId: Guid.NewGuid(),
                        TenantId: null,
                        Regulation: "EU_GDPR");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<PrivacyExportSubjectSubstitutionAnalyzer>(
                source, [PrivacyExportContextStub], TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == PrivacyExportSubjectSubstitutionAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC005_silent_on_other_records()
    {
        // Sanity check: the analyzer is scoped to PrivacyExportContext, not any
        // record that happens to have SubjectUserId / CallerUserId field names.
        string source = """
            using System;

            public sealed record OtherContext(
                Guid SubjectUserId,
                Guid CallerUserId);

            public class Service
            {
                public OtherContext Create(Guid subject, Guid caller) =>
                    new OtherContext(subject, caller);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<PrivacyExportSubjectSubstitutionAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == PrivacyExportSubjectSubstitutionAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
