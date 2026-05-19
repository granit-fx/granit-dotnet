using System.Collections.Immutable;
using Granit.Analyzers.CodeFixes.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// Base class for CodeFix providers that replace a direct API call with a Granit service injection.
/// Handles the common pattern: node annotation and tracking, expression replacement,
/// constructor DI injection, and using directive addition.
/// </summary>
public abstract class DependencyInjectionCodeFixProviderBase : CodeFixProvider
{
    /// <summary>Gets the user-visible title shown in the code fix lightbulb menu.</summary>
    protected abstract string Title { get; }

    /// <summary>Gets the diagnostic ID this provider fixes.</summary>
    protected abstract string DiagnosticId { get; }

    /// <summary>Gets the name of the interface to inject (e.g. <c>IClock</c>).</summary>
    protected abstract string InterfaceName { get; }

    /// <summary>Gets the backing field name (e.g. <c>_clock</c>).</summary>
    protected abstract string FieldName { get; }

    /// <summary>Gets the constructor parameter name (e.g. <c>clock</c>).</summary>
    protected abstract string ParamName { get; }

    /// <summary>Gets the using namespace to add (e.g. <c>Granit.Timing</c>).</summary>
    protected abstract string UsingNamespace { get; }

    /// <summary>Returns whether the flagged node has the expected syntax type.</summary>
    protected abstract bool IsExpectedNodeType(SyntaxNode node);

    /// <summary>
    /// Builds the replacement expression from the tracked node.
    /// The base class applies <see cref="Microsoft.CodeAnalysis.SyntaxNodeExtensions.WithTriviaFrom{T}"/> automatically.
    /// </summary>
    protected abstract SyntaxNode BuildReplacement(SyntaxNode trackedNode);

    /// <summary>
    /// Returns whether the DI field and constructor parameter should be injected.
    /// Override to return <c>false</c> in static contexts where DI is not applicable.
    /// </summary>
    protected virtual bool ShouldInjectDependency(SyntaxNode originalNode) => true;

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics.First();
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);

        if (!IsExpectedNodeType(node))
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

    private async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Annotate the target node to track it after immutable tree mutations
        SyntaxAnnotation annotation = new();
        root = root.ReplaceNode(node, node.WithAdditionalAnnotations(annotation));

        SyntaxNode? trackedNode = root.GetAnnotatedNodes(annotation).FirstOrDefault();
        if (trackedNode is null)
        {
            return document;
        }

        // Build the replacement expression and apply it
        SyntaxNode replacement = BuildReplacement(trackedNode).WithTriviaFrom(trackedNode);
        root = root.ReplaceNode(trackedNode, replacement);

        // Inject the dependency unless the call site is in a static context
        if (ShouldInjectDependency(node))
        {
            ClassDeclarationSyntax? classDecl =
                replacement.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                ?? root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (classDecl is not null)
            {
                SyntaxAnnotation classAnnotation = new();
                root = root.ReplaceNode(classDecl, classDecl.WithAdditionalAnnotations(classAnnotation));

                ClassDeclarationSyntax? trackedClass = root.GetAnnotatedNodes(classAnnotation)
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                if (trackedClass is not null)
                {
                    ClassDeclarationSyntax modifiedClass = DependencyInjectionHelper.EnsureFieldExists(
                        trackedClass, InterfaceName, FieldName);
                    modifiedClass = DependencyInjectionHelper.EnsureConstructorParameter(
                        modifiedClass, InterfaceName, ParamName, FieldName);
                    root = root.ReplaceNode(trackedClass, modifiedClass);
                }
            }
        }

        // Ensure the required using directive is present
        if (root is CompilationUnitSyntax compilationUnit)
        {
            root = DependencyInjectionHelper.EnsureUsingDirective(compilationUnit, UsingNamespace);
        }

        return document.WithSyntaxRoot(root);
    }
}
