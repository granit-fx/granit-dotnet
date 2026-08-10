using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-API001 — Reports a warning when <c>Results.Ok()</c>, <c>Results.BadRequest()</c>, or
/// other static methods on <c>Microsoft.AspNetCore.Http.Results</c> are used instead of the
/// typed equivalents on <c>Microsoft.AspNetCore.Http.TypedResults</c>.
/// </summary>
/// <remarks>
/// <c>TypedResults</c> carries metadata that OpenAPI uses to document response schemas
/// automatically. <c>Results</c> returns <c>IResult</c> which provides no compile-time
/// type information and requires manual <c>[ProducesResponseType]</c> attributes.
/// Always active — no opt-in needed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UntypedResultsAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRAPI001";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Avoid Results static class — use TypedResults",
        messageFormat: "Use TypedResults.{0}() instead of Results.{0}() "
            + "to provide OpenAPI response metadata and compile-time type safety",
        category: "Api",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The Results static class returns IResult which carries no response metadata. "
            + "TypedResults returns typed results (Ok<T>, Created<T>, ProblemHttpResult, etc.) "
            + "that OpenAPI uses to generate response schemas automatically.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string methodName = memberAccess.Name.Identifier.Text;

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        // Only flag the untyped Microsoft.AspNetCore.Http.Results class
        if (methodSymbol.ContainingType.ToDisplayString() != "Microsoft.AspNetCore.Http.Results")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, invocation.GetLocation(), methodName));
    }
}
