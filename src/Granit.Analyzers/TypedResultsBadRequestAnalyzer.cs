using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-API002 — Reports a warning when <c>TypedResults.BadRequest("message")</c> or
/// <c>TypedResults.BadRequest&lt;string&gt;("message")</c> is used instead of
/// <c>TypedResults.Problem(detail, statusCode)</c>.
/// </summary>
/// <remarks>
/// <c>TypedResults.BadRequest</c> with a body returns a plain <c>BadRequest&lt;T&gt;</c>
/// whose content type is <c>application/json</c>. Using <c>TypedResults.Problem</c>
/// guarantees the RFC 7807 <c>application/problem+json</c> format, providing a uniform
/// error contract for all consumers.
/// Always active — no opt-in needed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypedResultsBadRequestAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRAPI002";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Avoid TypedResults.BadRequest with body — use TypedResults.Problem for RFC 7807",
        messageFormat: "Use TypedResults.Problem(detail: ..., statusCode: StatusCodes.Status400BadRequest) "
            + "instead of TypedResults.BadRequest() with a body to ensure RFC 7807 compliance",
        category: "Api",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TypedResults.BadRequest with a body produces application/json instead of "
            + "application/problem+json (RFC 7807). Use TypedResults.Problem with detail and "
            + "statusCode parameters for a uniform error contract.");

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

        // Match both BadRequest and BadRequest<T> (GenericNameSyntax)
        string methodName = memberAccess.Name switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => string.Empty
        };

        if (methodName != "BadRequest")
        {
            return;
        }

        // Only flag calls with arguments (bare BadRequest() returning 400 with no body is acceptable)
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (methodSymbol.ContainingType.ToDisplayString() != "Microsoft.AspNetCore.Http.TypedResults")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, invocation.GetLocation()));
    }
}
