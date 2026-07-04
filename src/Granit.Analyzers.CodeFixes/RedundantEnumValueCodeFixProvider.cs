using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// CodeFix for GRENUM001 — removes the redundant <c>= N</c> initializer from an enum member,
/// leaving the implicit ordinal value unchanged.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantEnumValueCodeFixProvider))]
[Shared]
public sealed class RedundantEnumValueCodeFixProvider : CodeFixProvider
{
    private const string Title = "Remove redundant enum value";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(RedundantEnumValueAnalyzer.DiagnosticId);

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

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);

        EnumMemberDeclarationSyntax? member = node.FirstAncestorOrSelf<EnumMemberDeclarationSyntax>();
        if (member?.EqualsValue is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => RemoveInitializerAsync(context.Document, member, cancellationToken),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> RemoveInitializerAsync(
        Document document,
        EnumMemberDeclarationSyntax member,
        CancellationToken cancellationToken)
    {
        SyntaxNode root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;

        // Move the initializer's trailing trivia (the newline on a last member, empty otherwise)
        // onto the identifier so the member closes cleanly once `= N` is gone.
        EnumMemberDeclarationSyntax newMember = member
            .WithIdentifier(member.Identifier.WithTrailingTrivia(member.EqualsValue!.Value.GetTrailingTrivia()))
            .WithEqualsValue(null);

        return document.WithSyntaxRoot(root.ReplaceNode(member, newMember));
    }
}
