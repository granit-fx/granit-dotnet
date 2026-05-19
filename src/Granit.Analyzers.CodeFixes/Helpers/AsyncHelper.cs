using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes.Helpers;

/// <summary>
/// Shared helper for GREF001 CodeFix provider.
/// Handles async method transformation and await wrapping.
/// </summary>
internal static class AsyncHelper
{
    /// <summary>
    /// Adds the <c>async</c> modifier and transforms the return type:
    /// <c>void</c> → <c>Task</c>, <c>T</c> → <c>Task&lt;T&gt;</c>.
    /// No-op if already async.
    /// </summary>
    internal static MethodDeclarationSyntax MakeMethodAsync(MethodDeclarationSyntax method)
    {
        if (method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return method;
        }

        // Add async modifier
        MethodDeclarationSyntax asyncMethod = method.AddModifiers(
            SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

        // Transform return type
        string returnType = method.ReturnType.ToString();

        if (returnType == "void")
        {
            asyncMethod = asyncMethod.WithReturnType(
                SyntaxFactory.ParseTypeName("Task").WithTrailingTrivia(SyntaxFactory.Space));
        }
        else if (returnType != "Task" && !returnType.StartsWith("Task<", System.StringComparison.Ordinal))
        {
            asyncMethod = asyncMethod.WithReturnType(
                SyntaxFactory.ParseTypeName($"Task<{returnType}>")
                    .WithTrailingTrivia(SyntaxFactory.Space));
        }

        return asyncMethod;
    }

    /// <summary>
    /// Wraps an expression in <c>await</c>.
    /// </summary>
    internal static AwaitExpressionSyntax WrapInAwait(ExpressionSyntax expression) =>
        SyntaxFactory.AwaitExpression(expression);

    /// <summary>
    /// Ensures a using directive exists at the compilation unit level.
    /// </summary>
    internal static CompilationUnitSyntax EnsureUsingDirective(
        CompilationUnitSyntax root,
        string namespaceName) =>
        DependencyInjectionHelper.EnsureUsingDirective(root, namespaceName);
}
