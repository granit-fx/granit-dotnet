using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// CodeFix for GRAPI001 — replaces <c>Results.X(...)</c> with <c>TypedResults.X(...)</c>.
/// </summary>
/// <remarks>
/// Simple receiver replacement — no DI injection needed since both
/// <c>Results</c> and <c>TypedResults</c> are static classes in <c>Microsoft.AspNetCore.Http</c>.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UntypedResultsCodeFixProvider))]
[Shared]
public sealed class UntypedResultsCodeFixProvider : CodeFixProvider
{
    private const string Title = "Replace with TypedResults";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(UntypedResultsAnalyzer.DiagnosticId);

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

        Diagnostic diagnostic = context.Diagnostics.First();
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Expression is not IdentifierNameSyntax receiver
            || receiver.Identifier.Text != "Results")
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => ReplaceReceiverAsync(context.Document, receiver, ct),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ReplaceReceiverAsync(
        Document document,
        IdentifierNameSyntax receiver,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        IdentifierNameSyntax replacement = SyntaxFactory.IdentifierName("TypedResults")
            .WithTriviaFrom(receiver);

        root = root.ReplaceNode(receiver, replacement);
        return document.WithSyntaxRoot(root);
    }
}
