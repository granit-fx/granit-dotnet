using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-SEC001 — Reports a warning when <c>DateTime.Now</c>, <c>DateTime.UtcNow</c>,
/// <c>DateTimeOffset.Now</c> or <c>DateTimeOffset.UtcNow</c> is used directly.
/// </summary>
/// <remarks>
/// Direct access to the system clock makes code non-deterministic and untestable.
/// Use <c>IClock</c> from <c>Granit.Timing</c> instead.
/// Always active — no opt-in needed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DateTimeNowAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC001";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Avoid direct DateTime/DateTimeOffset clock access",
        messageFormat: "Use IClock from Granit.Timing instead of {0} to ensure deterministic, testable time access",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Direct access to DateTime.Now/UtcNow or DateTimeOffset.Now/UtcNow "
            + "produces non-deterministic, untestable code. "
            + "Use IClock from Granit.Timing instead.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        string memberName = memberAccess.Name.Identifier.Text;
        if (memberName != "Now" && memberName != "UtcNow")
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol is not IPropertySymbol propertySymbol)
        {
            return;
        }

        string containingType = propertySymbol.ContainingType.ToDisplayString();
        if (containingType != "System.DateTime" && containingType != "System.DateTimeOffset")
        {
            return;
        }

        string fullAccess = propertySymbol.ContainingType.Name + "." + memberName;

        context.ReportDiagnostic(
            Diagnostic.Create(_rule, memberAccess.GetLocation(), fullAccess));
    }
}
