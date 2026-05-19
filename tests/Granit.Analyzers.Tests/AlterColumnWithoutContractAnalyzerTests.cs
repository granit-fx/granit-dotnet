using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class AlterColumnWithoutContractAnalyzerTests
{
    [Fact]
    public async Task GR_MIGA004_fires_when_type_changes_without_contract_annotation()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AlterPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AlterColumn<string>(name: "age", table: "patients", oldClrType: typeof(int));
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<AlterColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == AlterColumnWithoutContractAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA004_silent_when_contract_annotation_present()
    {
        string source = """
            using Granit.Persistence.Migrations;
            using Microsoft.EntityFrameworkCore.Migrations;

            [MigrationCycle(MigrationPhase.Contract, "patient-v2")]
            public class AlterPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AlterColumn<string>(name: "age", table: "patients", oldClrType: typeof(int));
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<AlterColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == AlterColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA004_silent_when_oldClrType_absent()
    {
        // Constraint-only modification — no warning
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AlterPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AlterColumn<string>(name: "full_name", table: "patients", nullable: false);
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<AlterColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == AlterColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA004_silent_when_same_type()
    {
        // oldClrType matches target type — not a type change
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AlterPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AlterColumn<string>(name: "full_name", table: "patients", oldClrType: typeof(string));
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<AlterColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == AlterColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA004_silent_when_granit_package_absent()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AlterPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AlterColumn<string>(name: "age", table: "patients", oldClrType: typeof(int));
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<AlterColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: false,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == AlterColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
