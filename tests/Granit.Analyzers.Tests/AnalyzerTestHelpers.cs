using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers.Tests;

/// <summary>
/// Infrastructure for running Roslyn analyzers against in-memory C# compilations.
/// EF Core and Granit types are provided as source stubs — no real package references needed.
/// </summary>
internal static class AnalyzerTestHelpers
{
    /// <summary>
    /// Minimal EF Core stubs: <c>Migration</c> abstract class + <c>MigrationBuilder</c>
    /// with the methods targeted by GR-MIGA001–004.
    /// </summary>
    internal const string EfCoreMigrationsStub = """
        namespace Microsoft.EntityFrameworkCore.Migrations
        {
            public abstract class Migration
            {
                protected virtual void Up(MigrationBuilder migrationBuilder) { }
                protected virtual void Down(MigrationBuilder migrationBuilder) { }
            }

            public class MigrationBuilder
            {
                public void DropColumn(string name, string table = null, string schema = null) { }

                public void RenameColumn(string name, string table, string newName, string schema = null) { }

                public void AddColumn<T>(
                    string name,
                    string table = null,
                    string schema = null,
                    bool nullable = false,
                    object defaultValue = null,
                    string defaultValueSql = null) { }

                public void AlterColumn<T>(
                    string name,
                    string table = null,
                    string schema = null,
                    bool nullable = false,
                    System.Type oldClrType = null) { }
            }
        }
        """;

    /// <summary>
    /// Stub for <c>Granit.Persistence.Migrations.MigrationCycleAttribute</c> and
    /// <c>MigrationPhase</c> — simulates having <c>Granit.Persistence.Migrations</c> referenced.
    /// </summary>
    internal const string MigrationCycleAttributeStub = """
        namespace Granit.Persistence.Migrations
        {
            public enum MigrationPhase { Expand = 0, Migrate = 1, Contract = 2 }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class MigrationCycleAttribute : System.Attribute
            {
                public MigrationCycleAttribute(MigrationPhase phase, string cycleId) { }
            }
        }
        """;

    /// <summary>
    /// Minimal EF Core <c>DbContext</c> stub with <c>SaveChanges()</c> and
    /// <c>SaveChangesAsync()</c> — used by GR-EF001 tests.
    /// </summary>
    internal const string DbContextStub = """
        namespace Microsoft.EntityFrameworkCore
        {
            public abstract class DbContext
            {
                public int SaveChanges() => 0;
                public int SaveChanges(bool acceptAllChangesOnSuccess) => 0;
                public System.Threading.Tasks.Task<int> SaveChangesAsync(
                    System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(0);
            }
        }
        """;

    /// <summary>
    /// Minimal ASP.NET Core stubs for <c>IResponseCookies</c>, <c>HttpResponse</c>,
    /// <c>HttpContext</c>, and <c>CookieOptions</c> — used by GRSEC004 tests.
    /// </summary>
    internal const string ResponseCookiesStub = """
        namespace Microsoft.AspNetCore.Http
        {
            public class CookieOptions { }

            public interface IResponseCookies
            {
                void Append(string key, string value);
                void Append(string key, string value, CookieOptions options);
                void Delete(string key);
                void Delete(string key, CookieOptions options);
            }

            public abstract class HttpResponse
            {
                public abstract IResponseCookies Cookies { get; }
            }

            public abstract class HttpContext
            {
                public abstract HttpResponse Response { get; }
            }
        }
        """;

    /// <summary>
    /// Minimal stub for <c>Granit.Http.Cookies.IGranitCookieManager</c> — used to activate
    /// the opt-in GRSEC004 analyzer.
    /// </summary>
    internal const string GranitCookieManagerStub = """
        namespace Granit.Http.Cookies
        {
            public interface IGranitCookieManager
            {
                System.Threading.Tasks.Task SetCookieAsync(
                    Microsoft.AspNetCore.Http.HttpContext httpContext,
                    string cookieName,
                    string value);
                void DeleteCookie(
                    Microsoft.AspNetCore.Http.HttpContext httpContext,
                    string cookieName);
            }
        }
        """;

    /// <summary>
    /// Minimal stubs for ASP.NET Core binding attributes: <c>[FromServices]</c>,
    /// <c>[FromQuery]</c>, <c>[FromRoute]</c>, <c>[FromBody]</c>, <c>[FromHeader]</c>,
    /// <c>[FromForm]</c> (from <c>Microsoft.AspNetCore.Mvc</c>) and
    /// <c>[AsParameters]</c>, <c>IFormFile</c>, <c>IFormFileCollection</c>
    /// (from <c>Microsoft.AspNetCore.Http</c>) — used by GRAPI003 tests.
    /// </summary>
    internal const string MinimalApiBindingStub = """
        namespace Microsoft.AspNetCore.Http
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class AsParametersAttribute : System.Attribute { }

            public interface IFormFile { }
            public interface IFormFileCollection { }
        }

        namespace Microsoft.AspNetCore.Mvc
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromServicesAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromQueryAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromRouteAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromBodyAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromHeaderAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromFormAttribute : System.Attribute { }
        }
        """;

    /// <summary>
    /// Minimal ASP.NET Core stubs for <c>Results</c>, <c>TypedResults</c>, <c>StatusCodes</c>,
    /// and typed result types — used by GRAPI001 and GRAPI002 tests.
    /// </summary>
    internal const string MinimalApiResultsStub = """
        namespace Microsoft.AspNetCore.Http
        {
            public static class StatusCodes
            {
                public const int Status400BadRequest = 400;
                public const int Status404NotFound = 404;
                public const int Status422UnprocessableEntity = 422;
                public const int Status500InternalServerError = 500;
            }

            public static class Results
            {
                public static IResult Ok() => null;
                public static IResult Ok<T>(T value) => null;
                public static IResult BadRequest() => null;
                public static IResult BadRequest<T>(T error) => null;
                public static IResult NotFound() => null;
                public static IResult NotFound<T>(T value) => null;
                public static IResult Problem(string detail = null, int? statusCode = null) => null;
            }

            public static class TypedResults
            {
                public static Ok Ok() => null;
                public static Ok<T> Ok<T>(T value) => null;
                public static BadRequest BadRequest() => null;
                public static BadRequest<T> BadRequest<T>(T error) => null;
                public static NotFound NotFound() => null;
                public static NotFound<T> NotFound<T>(T value) => null;
                public static ProblemHttpResult Problem(string detail = null, int? statusCode = null) => null;
            }

            public interface IResult { }
            public class Ok : IResult { }
            public class Ok<T> : IResult { }
            public class BadRequest : IResult { }
            public class BadRequest<T> : IResult { }
            public class NotFound : IResult { }
            public class NotFound<T> : IResult { }
            public class ProblemHttpResult : IResult { }
        }
        """;

    /// <summary>
    /// Runs <typeparamref name="TAnalyzer"/> against the given <paramref name="source"/> code,
    /// compiled together with the EF Core stub and optionally the <c>MigrationCycleAttribute</c> stub.
    /// </summary>
    /// <param name="source">The C# source snippet to analyze.</param>
    /// <param name="includeMigrationCycleAttribute">
    /// <see langword="true"/> (default) to simulate a project that references
    /// <c>Granit.Persistence.Migrations</c>; <see langword="false"/> to omit it.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(
        string source,
        bool includeMigrationCycleAttribute = true,
        CancellationToken cancellationToken = default)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        SyntaxTree sourceTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        SyntaxTree efCoreTree = CSharpSyntaxTree.ParseText(EfCoreMigrationsStub, cancellationToken: cancellationToken);

        ImmutableArray<SyntaxTree>.Builder treeBuilder = ImmutableArray.CreateBuilder<SyntaxTree>();
        treeBuilder.Add(sourceTree);
        treeBuilder.Add(efCoreTree);

        if (includeMigrationCycleAttribute)
        {
            treeBuilder.Add(CSharpSyntaxTree.ParseText(MigrationCycleAttributeStub, cancellationToken: cancellationToken));
        }

        ImmutableArray<MetadataReference> references = GetNetCoreReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: treeBuilder.ToImmutable(),
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    /// <summary>
    /// Runs <typeparamref name="TAnalyzer"/> against the given <paramref name="source"/> code
    /// with optional additional source stubs (no EF Core migration stubs included by default).
    /// </summary>
    /// <param name="source">The C# source snippet to analyze.</param>
    /// <param name="additionalSources">Extra source stubs to include in the compilation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(
        string source,
        string[] additionalSources,
        CancellationToken cancellationToken = default)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        ImmutableArray<SyntaxTree>.Builder treeBuilder = ImmutableArray.CreateBuilder<SyntaxTree>();
        treeBuilder.Add(CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken));

        foreach (string additional in additionalSources)
        {
            treeBuilder.Add(CSharpSyntaxTree.ParseText(additional, cancellationToken: cancellationToken));
        }

        ImmutableArray<MetadataReference> references = GetNetCoreReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: treeBuilder.ToImmutable(),
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new TAnalyzer()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    private static ImmutableArray<MetadataReference> GetNetCoreReferences()
    {
        string? assembliesStr = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (assembliesStr is null)
        {
            return ImmutableArray<MetadataReference>.Empty;
        }

        ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in assembliesStr.Split(Path.PathSeparator))
        {
            builder.Add(MetadataReference.CreateFromFile(path));
        }

        return builder.ToImmutable();
    }
}
