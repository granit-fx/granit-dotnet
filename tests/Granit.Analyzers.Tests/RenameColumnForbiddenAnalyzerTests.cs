using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class RenameColumnForbiddenAnalyzerTests
{
    [Fact]
    public async Task GR_MIGA002_fires_when_RenameColumn_inside_migration()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class RenamePatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.RenameColumn(name: "old_name", table: "patients", newName: "new_name");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<RenameColumnForbiddenAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == RenameColumnForbiddenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA002_fires_even_without_granit_package()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class RenamePatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.RenameColumn(name: "old_name", table: "patients", newName: "new_name");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<RenameColumnForbiddenAnalyzer>(
                source,
                includeMigrationCycleAttribute: false,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == RenameColumnForbiddenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA002_fires_even_with_contract_annotation()
    {
        string source = """
            using Granit.Persistence.EntityFrameworkCore.Migrations;
            using Microsoft.EntityFrameworkCore.Migrations;

            [MigrationCycle(MigrationPhase.Contract, "patient-v2")]
            public class RenamePatientColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.RenameColumn(name: "old_name", table: "patients", newName: "new_name");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<RenameColumnForbiddenAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == RenameColumnForbiddenAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA002_silent_when_RenameColumn_outside_migration_class()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class SomeService
            {
                public void DoSomething(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.RenameColumn(name: "old_name", table: "patients", newName: "new_name");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<RenameColumnForbiddenAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == RenameColumnForbiddenAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
