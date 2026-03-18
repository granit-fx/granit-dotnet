using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Granit.Analyzers.CodeFixes.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// Code fix for <c>GRAPI003</c>: adds <c>[FromServices]</c> to a minimal API endpoint
/// handler parameter of interface type that is missing an explicit binding attribute.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MinimalApiServiceParameterCodeFixProvider))]
[System.Composition.Shared]
public sealed class MinimalApiServiceParameterCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add [FromServices]";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MinimalApiServiceParameterAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics.First();
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);

        ParameterSyntax? parameter = node.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => AddFromServicesAsync(context.Document, parameter, ct),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> AddFromServicesAsync(
        Document document,
        ParameterSyntax parameter,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        // Build [FromServices] attribute with trailing space so the type follows on the same line.
        AttributeSyntax attr = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("FromServices"));
        AttributeListSyntax attrList = SyntaxFactory
            .AttributeList(SyntaxFactory.SingletonSeparatedList(attr))
            .WithTrailingTrivia(SyntaxFactory.Space);

        ParameterSyntax newParam = parameter.WithAttributeLists(
            parameter.AttributeLists.Add(attrList));

        root = root.ReplaceNode(parameter, newParam);

        // Ensure `using Microsoft.AspNetCore.Mvc;` is present.
        if (root is CompilationUnitSyntax compilationUnit)
        {
            root = DependencyInjectionHelper.EnsureUsingDirective(
                compilationUnit, "Microsoft.AspNetCore.Mvc");
        }

        return document.WithSyntaxRoot(root);
    }
}
