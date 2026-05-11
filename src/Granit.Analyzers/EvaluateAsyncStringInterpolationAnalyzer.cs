using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GRBROWSING001 — Flags untrusted-string composition on the first argument of
/// JS / CSS injection methods on <c>Granit.Browsing.IBrowserPage</c>:
/// <c>EvaluateAsync</c>, <c>AddScriptTagAsync</c>, <c>AddStyleTagAsync</c>, <c>WaitForFunctionAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// Building JavaScript / CSS strings via string interpolation, concatenation, or <c>string.Format</c>
/// can introduce script injection. Validate inputs at the boundary or use a typed builder.
/// See VULN-203.
/// </para>
/// <para>
/// The analyzer triggers on:
/// <list type="bullet">
///   <item><description><see cref="InterpolatedStringExpressionSyntax"/> with at least one non-constant interpolation hole.</description></item>
///   <item><description>Binary <c>+</c> over strings.</description></item>
///   <item><description>Invocations of <c>string.Format</c>, <c>string.Concat</c>, <c>string.Join</c>.</description></item>
/// </list>
/// String literals and fully-constant interpolations (e.g. <c>$"page={1}"</c>, <c>nameof(Foo)</c>) do NOT trigger.
/// </para>
/// <para>
/// The public-facing name of this rule is <c>GRANIT-BROWSING-001</c> (used in docs and VULN-203
/// references). Roslyn diagnostic identifiers must be valid C# identifiers, so the on-the-wire ID
/// is <c>GRBROWSING001</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EvaluateAsyncStringInterpolationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic identifier (Roslyn-valid form; public name is <c>GRANIT-BROWSING-001</c>).</summary>
    public const string DiagnosticId = "GRBROWSING001";

    private const string BrowserPageMetadataName = "Granit.Browsing.IBrowserPage";

    private static readonly ImmutableHashSet<string> _targetMethods = ImmutableHashSet.Create(
        "EvaluateAsync",
        "AddScriptTagAsync",
        "AddStyleTagAsync",
        "WaitForFunctionAsync");

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "JS expression argument mixes user-controllable data",
        messageFormat: "JS/CSS argument to '{0}' is built via {1}; verify inputs are validated at the boundary or use a typed builder (VULN-203)",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Building JavaScript / CSS strings via string interpolation, concatenation, or string.Format can introduce script injection. Validate inputs at the boundary or use a typed builder. See VULN-203.",
        helpLinkUri: "https://github.com/granit-fx/granit-dotnet/blob/develop/docs-site/src/content/docs/dotnet/browsing/security.mdx#granit-browsing-001");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(_rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Opt-in: only activate when Granit.Browsing.IBrowserPage is present in the compilation.
            INamedTypeSymbol? browserPage = compilationContext.Compilation
                .GetTypeByMetadataName(BrowserPageMetadataName);
            if (browserPage is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, browserPage),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol browserPageInterface)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Cheap syntax filter first: method name must match before we resolve symbols.
        string? methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.Text,
            _ => null,
        };

        if (methodName is null || !_targetMethods.Contains(methodName))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!ImplementsOrEquals(methodSymbol.ContainingType, browserPageInterface))
        {
            return;
        }

        ArgumentSyntax firstArg = invocation.ArgumentList.Arguments[0];
        if (TryClassify(firstArg.Expression, context.SemanticModel, context.CancellationToken, out string? kind))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                _rule,
                firstArg.GetLocation(),
                methodName,
                kind));
        }
    }

    private static bool TryClassify(
        ExpressionSyntax expression,
        SemanticModel model,
        System.Threading.CancellationToken ct,
        out string? kind)
    {
        // Parenthesized → unwrap.
        while (expression is ParenthesizedExpressionSyntax paren)
        {
            expression = paren.Expression;
        }

        // Fast path: a fully-constant expression (literal, nameof, all-const interpolation) is safe.
        Optional<object?> constant = model.GetConstantValue(expression, ct);
        if (constant.HasValue)
        {
            kind = null;
            return false;
        }

        switch (expression)
        {
            case InterpolatedStringExpressionSyntax interp:
                if (HasNonConstantHole(interp, model, ct))
                {
                    kind = "string interpolation";
                    return true;
                }

                kind = null;
                return false;

            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
                if (IsStringConcatenation(bin, model, ct))
                {
                    kind = "string concatenation";
                    return true;
                }

                kind = null;
                return false;

            case InvocationExpressionSyntax inner:
                if (IsStringFormatConcatJoin(inner, model, ct, out string? formatKind))
                {
                    kind = formatKind;
                    return true;
                }

                kind = null;
                return false;

            default:
                kind = null;
                return false;
        }
    }

    private static bool HasNonConstantHole(
        InterpolatedStringExpressionSyntax interp,
        SemanticModel model,
        System.Threading.CancellationToken ct)
    {
        foreach (InterpolatedStringContentSyntax content in interp.Contents)
        {
            if (content is InterpolationSyntax interpolation)
            {
                Optional<object?> c = model.GetConstantValue(interpolation.Expression, ct);
                if (!c.HasValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsStringConcatenation(
        BinaryExpressionSyntax bin,
        SemanticModel model,
        System.Threading.CancellationToken ct)
    {
        // The `+` operator is string concatenation when either operand is typed as `string`.
        TypeInfo leftType = model.GetTypeInfo(bin.Left, ct);
        TypeInfo rightType = model.GetTypeInfo(bin.Right, ct);

        return IsStringType(leftType.Type) || IsStringType(rightType.Type);
    }

    private static bool IsStringType(ITypeSymbol? type) =>
        type is { SpecialType: SpecialType.System_String };

    private static bool IsStringFormatConcatJoin(
        InvocationExpressionSyntax inner,
        SemanticModel model,
        System.Threading.CancellationToken ct,
        out string? kind)
    {
        ISymbol? sym = model.GetSymbolInfo(inner, ct).Symbol;
        if (sym is IMethodSymbol ms
            && ms.ContainingType is { SpecialType: SpecialType.System_String })
        {
            switch (ms.Name)
            {
                case "Format":
                    kind = "string.Format";
                    return true;
                case "Concat":
                    kind = "string.Concat";
                    return true;
                case "Join":
                    kind = "string.Join";
                    return true;
            }
        }

        kind = null;
        return false;
    }

    private static bool ImplementsOrEquals(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceType))
            {
                return true;
            }
        }

        return false;
    }
}
