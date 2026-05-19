using System.Collections.Immutable;
using System.Composition;
using Granit.Analyzers.CodeFixes.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// CodeFix for GREF001 — replaces <c>SaveChanges()</c> with <c>await SaveChangesAsync()</c>
/// and transforms the enclosing method to async.
/// </summary>
/// <remarks>
/// <b>Async contagion</b>: changing the enclosing method's signature (e.g. <c>void</c> → <c>async Task</c>)
/// will break callers. This is expected — the IDE will then offer further fixes upstream.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SynchronousSaveChangesCodeFixProvider))]
[Shared]
public sealed class SynchronousSaveChangesCodeFixProvider : CodeFixProvider
{
    private const string Title = "Replace with SaveChangesAsync()";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(SynchronousSaveChangesAnalyzer.DiagnosticId);

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

        if (node is not InvocationExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: cancellationToken => ApplyFixAsync(context.Document, node, cancellationToken),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var invocation = (InvocationExpressionSyntax)node;

        // 1. Replace SaveChanges with SaveChangesAsync (preserve arguments)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        MemberAccessExpressionSyntax newMemberAccess = memberAccess.WithName(
            SyntaxFactory.IdentifierName("SaveChangesAsync"));

        InvocationExpressionSyntax newInvocation = invocation.WithExpression(newMemberAccess);

        // 2. Wrap in await
        ExpressionSyntax awaitExpression = AsyncHelper.WrapInAwait(newInvocation);

        // Annotate the enclosing method to find it after replacement
        MethodDeclarationSyntax? enclosingMethod = node.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        SyntaxAnnotation? methodAnnotation = null;
        if (enclosingMethod is not null)
        {
            methodAnnotation = new SyntaxAnnotation();
            SyntaxNode annotatedMethod = enclosingMethod.WithAdditionalAnnotations(methodAnnotation);
            root = root.ReplaceNode(enclosingMethod, annotatedMethod);

            // Re-find the invocation node in the new tree
            MethodDeclarationSyntax? trackedMethod = root.GetAnnotatedNodes(methodAnnotation)
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (trackedMethod is not null)
            {
                // Find the invocation within the tracked method
                InvocationExpressionSyntax? targetInvocation = trackedMethod
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(i =>
                        i.Expression is MemberAccessExpressionSyntax ma &&
                        ma.Name.Identifier.Text == "SaveChanges" &&
                        i.Span.Start == node.Span.Start);

                if (targetInvocation is not null)
                {
                    // Rebuild the replacement for the tracked invocation
                    var trackedMemberAccess =
                        (MemberAccessExpressionSyntax)targetInvocation.Expression;
                    MemberAccessExpressionSyntax newTrackedMemberAccess = trackedMemberAccess.WithName(
                        SyntaxFactory.IdentifierName("SaveChangesAsync"));
                    InvocationExpressionSyntax newTrackedInvocation =
                        targetInvocation.WithExpression(newTrackedMemberAccess);
                    ExpressionSyntax trackedAwaitExpression = AsyncHelper.WrapInAwait(newTrackedInvocation);

                    root = root.ReplaceNode(targetInvocation, trackedAwaitExpression);
                }

                // 3. Make the method async
                MethodDeclarationSyntax? methodAfterReplace = root.GetAnnotatedNodes(methodAnnotation)
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();

                if (methodAfterReplace is not null)
                {
                    MethodDeclarationSyntax asyncMethod = AsyncHelper.MakeMethodAsync(methodAfterReplace);
                    root = root.ReplaceNode(methodAfterReplace, asyncMethod);
                }
            }
        }
        else
        {
            // No enclosing method — just replace the invocation with await
            root = root.ReplaceNode(node, awaitExpression);
        }

        // 4. Add using System.Threading.Tasks
        if (root is CompilationUnitSyntax compilationUnit)
        {
            root = AsyncHelper.EnsureUsingDirective(compilationUnit, "System.Threading.Tasks");
        }

        return document.WithSyntaxRoot(root);
    }
}
