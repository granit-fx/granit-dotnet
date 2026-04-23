using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-SEC003 — Reports an error when a string literal is assigned to a variable
/// or property whose name suggests a secret (password, token, API key, etc.).
/// </summary>
/// <remarks>
/// Hardcoded secrets violate ISO 27001 and GDPR compliance. Use Granit.Vault or
/// secure configuration (environment variables, Key Vault) instead.
/// Always active — no opt-in needed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HardcodedSecretAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC003";

    private static readonly string[] SecretPatterns =
    {
        "password",
        "passwd",
        "secret",
        "apikey",
        "api_key",
        "connectionstring",
        "connection_string",
        "token",
        "credential",
        "accesskey",
        "access_key",
        "secretkey",
        "secret_key",
        "privatekey",
        "private_key"
    };

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Potential hardcoded secret detected",
        messageFormat: "Potential hardcoded secret detected in '{0}'. Use Granit.Vault or secure configuration instead.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Hardcoded secrets violate ISO 27001 and GDPR compliance requirements. "
            + "Store secrets in HashiCorp Vault via Granit.Vault or use secure "
            + "configuration providers.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        string value = literal.Token.ValueText;
        if (value.Length < 4)
        {
            return;
        }

        // Exclude placeholders and templates.
        if (value[0] == '{' || value[0] == '$')
        {
            return;
        }

        if (value.IndexOf('<') >= 0)
        {
            return;
        }

        string? identifierName = GetAssignmentTargetName(literal);
        if (identifierName is null)
        {
            return;
        }

        if (!IsSecretIdentifier(identifierName))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(_rule, literal.GetLocation(), identifierName));
    }

    private static bool IsSecretIdentifier(string name)
    {
        for (int i = 0; i < SecretPatterns.Length; i++)
        {
            if (name.IndexOf(SecretPatterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetAssignmentTargetName(SyntaxNode node)
    {
        SyntaxNode? parent = node.Parent;

        // Walk up through parenthesized expressions.
        while (parent is ParenthesizedExpressionSyntax)
        {
            parent = parent.Parent;
        }

        // Case 1: string password = "literal"; / field initializer
        if (parent is EqualsValueClauseSyntax equalsClause)
        {
            if (equalsClause.Parent is VariableDeclaratorSyntax variableDecl)
            {
                return variableDecl.Identifier.Text;
            }

            // Case 2: property declaration initializer
            if (equalsClause.Parent is PropertyDeclarationSyntax propertyDecl)
            {
                return propertyDecl.Identifier.Text;
            }

            // Case 3: default parameter value
            if (equalsClause.Parent is ParameterSyntax parameterDecl)
            {
                return parameterDecl.Identifier.Text;
            }
        }

        // Case 4: named argument — Method(password: "literal")
        if (parent is ArgumentSyntax argument && argument.NameColon is not null)
        {
            return argument.NameColon.Name.Identifier.Text;
        }

        // Case 5: simple assignment expression
        if (parent is AssignmentExpressionSyntax assignment
            && assignment.Left is IdentifierNameSyntax identifierName)
        {
            return identifierName.Identifier.Text;
        }

        return null;
    }
}
