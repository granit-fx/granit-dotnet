#nullable disable
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GRSEC010 — Reports an error when a <c>TagList</c> initializer contains a string
/// literal key matching a PII-indicative name (email, phone, ipAddress, etc.).
/// </summary>
/// <remarks>
/// Using PII as metric tag values causes both a GDPR violation (PII in telemetry
/// backends) and a cardinality explosion (unbounded memory, high cost in Prometheus/Grafana).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TagListPiiAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC010";

    private static readonly string[] PiiPatterns =
    {
        "email",
        "mail",
        "phone",
        "mobile",
        "address",
        "street",
        "city",
        "postal",
        "zip",
        "firstname",
        "lastname",
        "fullname",
        "displayname",
        "username",
        "ssn",
        "nationalid",
        "passport",
        "birthdate",
        "dateofbirth",
        "salary",
        "income",
        "bankaccount",
        "iban",
        "ipaddress",
        "password",
        "secret",
        "avatar",
        "photo"
    };

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "PII-indicative name used as metric tag key",
        messageFormat: "Tag key '{0}' in TagList matches a PII pattern. Use bounded, low-cardinality values only (tenant_id, category, status). PII in metrics violates GDPR Art. 5 and causes cardinality explosion.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Metric tags must not contain PII-indicative names. Using user-controlled "
            + "values as metric tags causes both a GDPR violation (PII in telemetry backends) "
            + "and a cardinality explosion (unbounded memory, high cost in monitoring systems).");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeInitializer, SyntaxKind.ObjectInitializerExpression, SyntaxKind.CollectionInitializerExpression);

    private static void AnalyzeInitializer(SyntaxNodeAnalysisContext context)
    {
        var initializer = (InitializerExpressionSyntax)context.Node;

        // Check if this initializer is on a TagList (by checking the type of the enclosing expression).
        if (!IsTagListInitializer(initializer, context.SemanticModel))
        {
            return;
        }

        // Scan all string literals inside the initializer for PII patterns.
        foreach (SyntaxNode descendant in initializer.DescendantNodes())
        {
            if (descendant is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                string value = literal.Token.ValueText;
                if (IsPiiTagName(value))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(_rule, literal.GetLocation(), value));
                }
            }
        }
    }

    private static bool IsTagListInitializer(InitializerExpressionSyntax initializer, SemanticModel semanticModel)
    {
        SyntaxNode parent = initializer.Parent;
        if (parent is null)
        {
            return false;
        }

        // Case: new TagList { { "key", value } }
        if (parent is ObjectCreationExpressionSyntax creation)
        {
            ITypeSymbol type = semanticModel.GetTypeInfo(creation).Type;
            return type != null && IsTagListType(type);
        }

        // Case: implicit new TagList { ... } or collection expression
        if (parent is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            ITypeSymbol type = semanticModel.GetTypeInfo(implicitCreation).Type;
            return type != null && IsTagListType(type);
        }

        return false;
    }

    private static bool IsTagListType(ITypeSymbol type)
    {
        return type.Name == "TagList"
            && type.ContainingNamespace != null
            && type.ContainingNamespace.ToDisplayString() == "System.Diagnostics";
    }

    private static bool IsPiiTagName(string name)
    {
        for (int i = 0; i < PiiPatterns.Length; i++)
        {
            if (name.IndexOf(PiiPatterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
