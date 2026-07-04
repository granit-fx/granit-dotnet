using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class RedundantEnumValueAnalyzerTests
{
    private const string PersistAsIntStub = """
        namespace Granit.Domain
        {
            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
            public sealed class PersistAsIntAttribute : System.Attribute { }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string source, params string[] additionalSources)
        => await AnalyzerTestHelpers.RunAnalyzerAsync<RedundantEnumValueAnalyzer>(
            source, additionalSources, TestContext.Current.CancellationToken);

    private static int CountGrEnum(ImmutableArray<Diagnostic> diagnostics)
        => diagnostics.Count(d => d.Id == RedundantEnumValueAnalyzer.DiagnosticId);

    [Fact]
    public async Task GRENUM001_fires_on_every_member_of_a_sequential_from_zero_enum()
    {
        string source = """
            public enum ChartType
            {
                Bar = 0,
                Line = 1,
                Area = 2,
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await RunAsync(source);

        CountGrEnum(diagnostics).ShouldBe(3);
    }

    [Fact]
    public async Task GRENUM001_silent_on_enum_without_explicit_values()
    {
        string source = """
            public enum ChartType
            {
                Bar,
                Line,
                Area,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_on_flags_enum()
    {
        string source = """
            [System.Flags]
            public enum Capabilities
            {
                None = 0,
                Read = 1,
                Write = 2,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_on_non_zero_start()
    {
        string source = """
            public enum Tier
            {
                Deterministic = 1,
                Blocking = 2,
                Fuzzy = 3,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_on_gapped_wire_codes()
    {
        string source = """
            public enum RedirectType
            {
                MovedPermanently = 301,
                Found = 302,
                TemporaryRedirect = 307,
                PermanentRedirect = 308,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_on_shift_expressions()
    {
        string source = """
            public enum Sides
            {
                Host = 1 << 0,
                Tenant = 1 << 1,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_on_partial_explicit_values()
    {
        string source = """
            public enum DeviceKind
            {
                Unknown = 0,
                Phone,
                Tablet,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_when_enum_is_consumed_by_a_PersistAsInt_property()
    {
        string source = """
            using Granit.Domain;

            public enum ManualStatus
            {
                Available = 0,
                Busy = 1,
                Away = 2,
            }

            public class UserPresence
            {
                [PersistAsInt]
                public ManualStatus Status { get; set; }
            }
            """;

        CountGrEnum(await RunAsync(source, PersistAsIntStub)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_silent_when_enum_is_consumed_by_a_nullable_PersistAsInt_property()
    {
        string source = """
            using Granit.Domain;

            public enum ManualStatus
            {
                Available = 0,
                Busy = 1,
            }

            public class UserPresence
            {
                [PersistAsInt]
                public ManualStatus? Status { get; set; }
            }
            """;

        CountGrEnum(await RunAsync(source, PersistAsIntStub)).ShouldBe(0);
    }

    [Fact]
    public async Task GRENUM001_fires_on_single_member_enum()
    {
        string source = """
            public enum SingleKind
            {
                Only = 0,
            }
            """;

        CountGrEnum(await RunAsync(source)).ShouldBe(1);
    }
}
