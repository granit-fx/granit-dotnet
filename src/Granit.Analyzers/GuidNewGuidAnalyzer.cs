using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-SEC002 — Reports a warning when <c>Guid.NewGuid()</c> is called directly.
/// </summary>
/// <remarks>
/// <c>Guid.NewGuid()</c> generates random GUIDs that cause index fragmentation in clustered
/// indexes. Use <c>IGuidGenerator.Create()</c> from <c>Granit.Guids</c> for sequential GUID
/// generation optimized for database performance.
/// Always active — no opt-in needed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GuidNewGuidAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC002";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Avoid Guid.NewGuid() — use IGuidGenerator",
        messageFormat: "Use IGuidGenerator.Create() from Granit.Guids instead of Guid.NewGuid() "
            + "for sequential GUID generation optimized for clustered indexes",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Guid.NewGuid() generates random GUIDs that cause index fragmentation "
            + "in clustered indexes. Use IGuidGenerator.Create() from Granit.Guids for "
            + "sequential GUID generation.");

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

        if (memberAccess.Name.Identifier.Text != "NewGuid")
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (methodSymbol.ContainingType.ToDisplayString() != "System.Guid")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, invocation.GetLocation()));
    }
}
