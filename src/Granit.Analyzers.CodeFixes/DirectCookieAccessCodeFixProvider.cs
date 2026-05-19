using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes;

/// <summary>
/// CodeFix for GRSEC004 — replaces direct <c>IResponseCookies.Append()</c> / <c>Delete()</c>
/// with <c>IGranitCookieManager</c> calls and injects the dependency via constructor DI.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DirectCookieAccessCodeFixProvider))]
[Shared]
public sealed class DirectCookieAccessCodeFixProvider : DependencyInjectionCodeFixProviderBase
{
    protected override string Title => "Replace with IGranitCookieManager injection";
    protected override string DiagnosticId => DirectCookieAccessAnalyzer.DiagnosticId;
    protected override string InterfaceName => "IGranitCookieManager";
    protected override string FieldName => "_cookieManager";
    protected override string ParamName => "cookieManager";
    protected override string UsingNamespace => "Granit.Http.Cookies";

    protected override bool IsExpectedNodeType(SyntaxNode node) =>
        node is InvocationExpressionSyntax;

    protected override SyntaxNode BuildReplacement(SyntaxNode trackedNode)
    {
        var tracked = (InvocationExpressionSyntax)trackedNode;
        var memberAccess = (MemberAccessExpressionSyntax)tracked.Expression;
        string methodName = memberAccess.Name.Identifier.Text;
        ArgumentListSyntax originalArgs = tracked.ArgumentList;

        if (methodName == "Append")
        {
            SeparatedSyntaxList<ArgumentSyntax> newArgs = SyntaxFactory.SeparatedList(
                new[]
                {
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("httpContext")),
                    originalArgs.Arguments.ElementAtOrDefault(0)
                        ?? SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(""))),
                    originalArgs.Arguments.ElementAtOrDefault(1)
                        ?? SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("")))
                });

            return SyntaxFactory.AwaitExpression(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(FieldName),
                        SyntaxFactory.IdentifierName("SetCookieAsync")),
                    SyntaxFactory.ArgumentList(newArgs)));
        }

        SeparatedSyntaxList<ArgumentSyntax> deleteArgs = SyntaxFactory.SeparatedList(
            new[]
            {
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("httpContext")),
                originalArgs.Arguments.ElementAtOrDefault(0)
                    ?? SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("")))
            });

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(FieldName),
                SyntaxFactory.IdentifierName("DeleteCookie")),
            SyntaxFactory.ArgumentList(deleteArgs));
    }
}
