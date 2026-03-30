using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class DropColumnWithoutContractAnalyzerTests
{
    [Fact]
    public async Task GR_MIGA001_fires_when_DropColumn_inside_migration_without_annotation()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class DropOldColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(name: "old_col", table: "patients");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DropColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == DropColumnWithoutContractAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GR_MIGA001_silent_when_migration_has_contract_annotation()
    {
        string source = """
            using Granit.Persistence.Migrations;
            using Microsoft.EntityFrameworkCore.Migrations;

            [MigrationCycle(MigrationPhase.Contract, "patient-v2")]
            public class DropOldColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(name: "old_col", table: "patients");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DropColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DropColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA001_silent_when_granit_package_absent()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class DropOldColumn : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(name: "old_col", table: "patients");
                }

                protected override void Down(MigrationBuilder migrationBuilder) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DropColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: false,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DropColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GR_MIGA001_silent_when_DropColumn_outside_migration_class()
    {
        string source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class SomeService
            {
                public void DoSomething(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(name: "old_col", table: "patients");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<DropColumnWithoutContractAnalyzer>(
                source,
                includeMigrationCycleAttribute: true,
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == DropColumnWithoutContractAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }
}
