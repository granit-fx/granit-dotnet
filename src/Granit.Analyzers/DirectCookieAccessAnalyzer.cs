using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-SEC004 — Reports a warning when <c>IResponseCookies.Append()</c> or
/// <c>IResponseCookies.Delete()</c> is called directly instead of going through
/// <c>IGranitCookieManager</c>.
/// </summary>
/// <remarks>
/// Direct cookie manipulation bypasses the Strict Registry Pattern and GDPR consent checks.
/// Use <c>IGranitCookieManager.SetCookieAsync()</c> or <c>IGranitCookieManager.DeleteCookie()</c> instead.
/// Opt-in: only activates when <c>Granit.Http.Cookies.IGranitCookieManager</c> is present in the compilation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectCookieAccessAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC004";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Avoid direct IResponseCookies access — use IGranitCookieManager",
        messageFormat: "Use IGranitCookieManager instead of directly calling IResponseCookies.{0}() "
            + "to enforce the Strict Registry Pattern and GDPR consent checks",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Direct calls to IResponseCookies.Append() or Delete() bypass the cookie registry "
            + "and GDPR consent checks. Use IGranitCookieManager.SetCookieAsync() or DeleteCookie() instead.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterCompilationStartAction(compilationContext =>
        {
            // Opt-in: only activate when Granit.Http.Cookies is referenced.
            INamedTypeSymbol? cookieManager = compilationContext.Compilation
                .GetTypeByMetadataName("Granit.Http.Cookies.IGranitCookieManager");
            if (cookieManager is null)
            {
                return;
            }

            INamedTypeSymbol? responseCookies = compilationContext.Compilation
                .GetTypeByMetadataName("Microsoft.AspNetCore.Http.IResponseCookies");
            if (responseCookies is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, responseCookies),
                SyntaxKind.InvocationExpression);
        });

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol responseCookiesInterface)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("Append" or "Delete"))
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!ImplementsOrEquals(methodSymbol.ContainingType, responseCookiesInterface))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, invocation.GetLocation(), methodName));
    }

    private static bool ImplementsOrEquals(INamedTypeSymbol type, INamedTypeSymbol interfaceType) =>
        SymbolEqualityComparer.Default.Equals(type, interfaceType)
        || type.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, interfaceType));
}
