using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class NullableColumnInExpandAnalyzerTests
{
    [Fact]
    public async Task GR_MIGA003_fires_when_AddColumn_not_null_without_default()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(name: "full_name", table: "patients", nullable: false);
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<NullableColumnInExpandAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == NullableColumnInExpandAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA003_fires_when_AddColumn_not_null_implicit_without_default()
    {
        // nullable is not specified — defaults to false — should fire
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(name: "full_name", table: "patients");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<NullableColumnInExpandAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == NullableColumnInExpandAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA003_silent_when_nullable_true()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(name: "full_name", table: "patients", nullable: true);
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<NullableColumnInExpandAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == NullableColumnInExpandAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA003_silent_when_default_value_provided()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(name: "full_name", table: "patients", nullable: false, defaultValue: "");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<NullableColumnInExpandAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == NullableColumnInExpandAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA003_silent_when_default_value_sql_provided()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(name: "full_name", table: "patients", nullable: false, defaultValueSql: "''");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<NullableColumnInExpandAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == NullableColumnInExpandAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA003_silent_when_granit_package_absent()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddPatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(name: "full_name", table: "patients", nullable: false);
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<NullableColumnInExpandAnalyzer>(
                source,
                includeMigrationCycleAttribute: false,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == NullableColumnInExpandAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
