using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Granit.Analyzers.Tests;

public sealed class SynchronousSaveChangesAnalyzerTests
{
    [Fact]
    public async Task GREF001_fires_on_SaveChanges_no_args()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;

            public class AppDbContext : DbContext { }

            public class Service
            {
                public void Save(AppDbContext context)
                {
                    context.SaveChanges();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.DbContextStub },
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GREF001_fires_on_SaveChanges_with_bool_arg()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;

            public class AppDbContext : DbContext { }

            public class Service
            {
                public void Save(AppDbContext context)
                {
                    context.SaveChanges(true);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.DbContextStub },
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GREF001_silent_on_SaveChangesAsync()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;
            using System.Threading.Tasks;

            public class AppDbContext : DbContext { }

            public class Service
            {
                public async Task SaveAsync(AppDbContext context)
                {
                    await context.SaveChangesAsync();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.DbContextStub },
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GREF001_fires_on_DbContext_subclass()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;

            public class TenantDbContext : DbContext { }

            public class OrderDbContext : TenantDbContext { }

            public class Service
            {
                public void Save(OrderDbContext context)
                {
                    context.SaveChanges();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.DbContextStub },
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task GREF001_silent_when_DbContext_absent()
    {
        string source = """
            public class FakeContext
            {
                public int SaveChanges() => 0;
            }

            public class Service
            {
                public void Save(FakeContext context)
                {
                    context.SaveChanges();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                Array.Empty<string>(),
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GREF001_silent_on_unrelated_SaveChanges()
    {
        string source = """
            public class MyRepository
            {
                public void SaveChanges() { }
            }

            public class Service
            {
                public void Save(MyRepository repo)
                {
                    repo.SaveChanges();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.DbContextStub },
                TestContext.Current.CancellationToken);

        diagnostics.Where(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task GREF001_fires_on_direct_DbContext_reference()
    {
        string source = """
            using Microsoft.EntityFrameworkCore;

            public class Service
            {
                public void Save(DbContext context)
                {
                    context.SaveChanges();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzerTestHelpers.RunAnalyzerAsync<SynchronousSaveChangesAnalyzer>(
                source,
                new[] { AnalyzerTestHelpers.DbContextStub },
                TestContext.Current.CancellationToken);

        diagnostics.ShouldContain(d => d.Id == SynchronousSaveChangesAnalyzer.DiagnosticId);
    }
}
