using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Shouldly;

namespace Granit.Analyzers.CodeFixes.Tests;

/// <summary>
/// Infrastructure for testing Roslyn CodeFixProviders against in-memory C# compilations.
/// Custom helper because <c>Microsoft.CodeAnalysis.CSharp.CodeFix.Testing.XUnit</c> is
/// incompatible with xUnit v3 (binary dependency on <c>xunit.assert</c> v2).
/// </summary>
internal static class CodeFixTestHelpers
{
    /// <summary>
    /// Compiles <paramref name="source"/>, runs <typeparamref name="TAnalyzer"/>,
    /// applies the first CodeFix action from <typeparamref name="TCodeFix"/>,
    /// formats the result, and asserts it matches <paramref name="expected"/>.
    /// </summary>
    internal static async Task VerifyCodeFixAsync<TAnalyzer, TCodeFix>(
        string source,
        string expected,
        string[]? additionalSources = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        CancellationToken cancellationToken = Xunit.TestContext.Current.CancellationToken;
        // 1. Build the compilation
        ImmutableArray<SyntaxTree>.Builder treeBuilder = ImmutableArray.CreateBuilder<SyntaxTree>();
        treeBuilder.Add(CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken));

        if (additionalSources is not null)
        {
            foreach (string additional in additionalSources)
            {
                treeBuilder.Add(CSharpSyntaxTree.ParseText(additional, cancellationToken: cancellationToken));
            }
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: treeBuilder.ToImmutable(),
            references: GetNetCoreReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // 2. Run the analyzer
        TAnalyzer analyzer = new();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        diagnostics.ShouldNotBeEmpty("the analyzer should report at least one diagnostic");

        // 3. Apply the first CodeFix action on the first diagnostic
        Diagnostic diagnostic = diagnostics
            .OrderBy(d => d.Location.SourceSpan.Start)
            .First();

        using AdhocWorkspace workspace = new();
        Document document = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(GetNetCoreReferences())
            .AddDocument("Test.cs", source);

        // Add additional source documents
        if (additionalSources is not null)
        {
            Project project = document.Project;
            for (int i = 0; i < additionalSources.Length; i++)
            {
                project = project.AddDocument($"Additional{i}.cs", additionalSources[i]).Project;
            }

            document = project.Documents.First();
        }

        TCodeFix codeFix = new();
        CodeAction? codeAction = null;

        CodeFixContext context = new(
            document,
            diagnostic,
            (action, _) => codeAction = action,
            cancellationToken);

        await codeFix.RegisterCodeFixesAsync(context);
        codeAction.ShouldNotBeNull("the CodeFix provider should register an action");

        ImmutableArray<CodeActionOperation> operations = await codeAction!.GetOperationsAsync(cancellationToken);
        ApplyChangesOperation? applyChanges = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
        applyChanges.ShouldNotBeNull("the CodeFix action should produce an ApplyChangesOperation");

        // 4. Get the changed document and format it
        Document changedDocument = applyChanges!.ChangedSolution.GetDocument(document.Id)!;
        Document formattedDocument = await Formatter.FormatAsync(changedDocument, cancellationToken: cancellationToken);
        string actualText = (await formattedDocument.GetTextAsync(cancellationToken)).ToString();

        // 5. Compare — use Assert.Equal to avoid FormatException with curly braces in Shouldly
        Xunit.Assert.Equal(expected, actualText);
    }

    /// <summary>
    /// Verifies that no CodeFix is offered for the given source.
    /// </summary>
    internal static async Task VerifyNoCodeFixAsync<TAnalyzer, TCodeFix>(
        string source,
        string[]? additionalSources = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        CancellationToken cancellationToken = Xunit.TestContext.Current.CancellationToken;
        ImmutableArray<SyntaxTree>.Builder treeBuilder = ImmutableArray.CreateBuilder<SyntaxTree>();
        treeBuilder.Add(CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken));

        if (additionalSources is not null)
        {
            foreach (string additional in additionalSources)
            {
                treeBuilder.Add(CSharpSyntaxTree.ParseText(additional, cancellationToken: cancellationToken));
            }
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: treeBuilder.ToImmutable(),
            references: GetNetCoreReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        TAnalyzer analyzer = new();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        diagnostics.ShouldBeEmpty("no diagnostic should be reported");
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
