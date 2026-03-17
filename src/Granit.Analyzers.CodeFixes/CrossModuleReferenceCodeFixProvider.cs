using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// CodeFix for GRMOD001 — suggests replacing a cross-module internal type reference
/// with the equivalent type from the target module's <c>.Contracts</c> namespace,
/// if such a type exists.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CrossModuleReferenceCodeFixProvider))]
[Shared]
public sealed class CrossModuleReferenceCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(CrossModuleReferenceAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics.First();
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not SimpleNameSyntax)
        {
            return;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node, context.CancellationToken);
        ISymbol? referencedSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (referencedSymbol is not INamedTypeSymbol referencedType)
        {
            return;
        }

        string? targetModule = diagnostic.Properties.GetValueOrDefault(CrossModuleReferenceAnalyzer.TargetModuleProperty);
        if (targetModule is null)
        {
            return;
        }

        // Search for a type with the same name in the target module's .Contracts namespace.
        INamedTypeSymbol? contractsType = FindContractsType(
            semanticModel.Compilation,
            targetModule,
            referencedType.Name);

        if (contractsType is null)
        {
            return;
        }

        string contractsNamespace = contractsType.ContainingNamespace.ToDisplayString();
        string title = $"Use {referencedType.Name} from {contractsNamespace}";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: cancellationToken =>
                    ReplaceWithContractsTypeAsync(context.Document, contractsNamespace, cancellationToken),
                equivalenceKey: title),
            diagnostic);
    }

    private static INamedTypeSymbol? FindContractsType(
        Compilation compilation,
        string targetModule,
        string typeName)
    {
        string contractsPrefix = ".Modules." + targetModule + ".Contracts";

        return FindTypeInNamespace(compilation.GlobalNamespace, contractsPrefix, typeName);
    }

    private static INamedTypeSymbol? FindTypeInNamespace(
        INamespaceSymbol ns,
        string contractsPrefix,
        string typeName)
    {
        string fullName = ns.ToDisplayString();
        if (fullName.Contains(contractsPrefix))
        {
            INamedTypeSymbol? match = ns.GetTypeMembers(typeName).FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        foreach (INamespaceSymbol child in ns.GetNamespaceMembers())
        {
            INamedTypeSymbol? found = FindTypeInNamespace(child, contractsPrefix, typeName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static async Task<Document> ReplaceWithContractsTypeAsync(
        Document document,
        string contractsNamespace,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Replace the using directive for the internal namespace with the contracts namespace.
        if (root is CompilationUnitSyntax compilationUnit)
        {
            // Find the using directive that imports the internal namespace.
            string? internalNamespace = GetUsingForNode(compilationUnit);

            if (internalNamespace is not null)
            {
                UsingDirectiveSyntax? existingUsing = compilationUnit.Usings
                    .FirstOrDefault(u => u.Name?.ToString() == internalNamespace);

                if (existingUsing is not null)
                {
                    // Check if the contracts using already exists.
                    bool contractsUsingExists = compilationUnit.Usings
                        .Any(u => u.Name?.ToString() == contractsNamespace);

                    if (contractsUsingExists)
                    {
                        // Just remove the internal using.
                        root = compilationUnit.RemoveNode(existingUsing, SyntaxRemoveOptions.KeepNoTrivia);
                    }
                    else
                    {
                        // Replace with contracts using.
                        UsingDirectiveSyntax newUsing = existingUsing.WithName(
                            SyntaxFactory.ParseName(contractsNamespace));
                        root = root.ReplaceNode(existingUsing, newUsing);
                    }
                }
            }
        }

        return document.WithSyntaxRoot(root!);
    }

    private static string? GetUsingForNode(CompilationUnitSyntax compilationUnit)
    {
        // Try to find which using directive brought the type into scope.
        // Heuristic: look for a using that contains ".Modules." but not ".Contracts".
        foreach (UsingDirectiveSyntax usingDirective in compilationUnit.Usings)
        {
            string? usingName = usingDirective.Name?.ToString();
            if (usingName is not null
                && usingName.Contains(".Modules.")
                && !usingName.Contains(".Contracts"))
            {
                return usingName;
            }
        }

        return null;
    }
}
