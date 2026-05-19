using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class HardcodedSecretAnalyzerTests
{
    [Fact]
    public async Task GRSEC003_fires_on_password_variable()
    {
        string source = """
            public class Config
            {
                public void Setup()
                {
                    string password = "myP@ssw0rd!";
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC003_fires_on_ApiKey_property()
    {
        string source = """
            public class Config
            {
                public string ApiKey { get; } = "sk-1234567890abcdef";
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC003_fires_on_connectionString_field()
    {
        string source = """
            public class Config
            {
                private string connectionString = "Server=localhost;Database=db;User=sa;Password=secret";
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC003_fires_on_secret_named_argument()
    {
        string source = """
            public class Service
            {
                public void Configure(string secret) { }

                public void Setup()
                {
                    Configure(secret: "abc123def456");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC003_fires_on_token_assignment()
    {
        string source = """
            public class Service
            {
                private string token;

                public void Setup()
                {
                    token = "bearer-xyz-12345";
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GRSEC003_silent_on_empty_string()
    {
        string source = """
            public class Config
            {
                public string Password { get; } = "";
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC003_silent_on_short_string()
    {
        string source = """
            public class Config
            {
                public string Password { get; } = "ab";
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC003_silent_on_placeholder()
    {
        string source = """
            public class Config
            {
                public string Password { get; } = "{vault:secret/data/db#password}";
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC003_silent_on_non_secret_variable()
    {
        string source = """
            public class Config
            {
                public void Setup()
                {
                    string userName = "admin123456";
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GRSEC003_silent_on_unassigned_string_literal()
    {
        string source = """
            using System;

            public class Service
            {
                public void Log()
                {
                    Console.WriteLine("password123secret");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<HardcodedSecretAnalyzer>(
                source, Array.Empty<string>(), TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == HardcodedSecretAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
