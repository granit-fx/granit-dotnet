using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// CodeFix for GRAPI002 — replaces <c>TypedResults.BadRequest("msg")</c> with
/// <c>TypedResults.Problem(detail: "msg", statusCode: StatusCodes.Status400BadRequest)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TypedResultsBadRequestCodeFixProvider))]
[Shared]
public sealed class TypedResultsBadRequestCodeFixProvider : CodeFixProvider
{
    private const string Title = "Replace with TypedResults.Problem (RFC 7807)";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(TypedResultsBadRequestAnalyzer.DiagnosticId);

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
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => ReplaceBadRequestWithProblemAsync(context.Document, invocation, ct),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ReplaceBadRequestWithProblemAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Extract the first argument as the detail value
        ExpressionSyntax detailExpression = invocation.ArgumentList.Arguments[0].Expression;

        // Build: TypedResults.Problem(detail: <expr>, statusCode: StatusCodes.Status400BadRequest)
        InvocationExpressionSyntax replacement = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("TypedResults"),
                SyntaxFactory.IdentifierName("Problem")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(
                        nameColon: SyntaxFactory.NameColon("detail"),
                        refKindKeyword: default,
                        expression: detailExpression),
                    SyntaxFactory.Argument(
                        nameColon: SyntaxFactory.NameColon("statusCode"),
                        refKindKeyword: default,
                        expression: SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("StatusCodes"),
                            SyntaxFactory.IdentifierName("Status400BadRequest")))
                })))
            .WithTriviaFrom(invocation);

        root = root.ReplaceNode(invocation, replacement);

        // Ensure using Microsoft.AspNetCore.Http is present (for StatusCodes)
        if (root is CompilationUnitSyntax compilationUnit)
        {
            const string usingNamespace = "Microsoft.AspNetCore.Http";
            bool hasUsing = compilationUnit.Usings.Any(u => u.Name?.ToString() == usingNamespace);
            if (!hasUsing)
            {
                root = compilationUnit.AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingNamespace))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));
            }
        }

        return document.WithSyntaxRoot(root);
    }
}
