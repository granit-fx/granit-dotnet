#nullable disable
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GRSEC011 — Reports an error when a <c>[LoggerMessage]</c> template contains a
/// placeholder whose name matches a PII-indicative pattern (e.g., <c>{Email}</c>,
/// <c>{IpAddress}</c>, <c>{UserName}</c>).
/// </summary>
/// <remarks>
/// PII in structured logs violates GDPR Art. 5 (data minimization) and can expose
/// personal data to log aggregation systems (ELK, Loki, Application Insights).
/// Use identifiers (userId, correlationId) instead of PII values.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggerMessagePiiAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC011";

    private static readonly string[] PiiPatterns =
    {
        "email",
        "mail",
        "phone",
        "mobile",
        "address",
        "street",
        "city",
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
        "token",
        "postal",
        "zip",
        "avatar",
        "photo"
    };

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "PII-indicative placeholder in LoggerMessage template",
        messageFormat: "LoggerMessage template contains PII placeholder '{{{0}}}'. Log identifiers (userId, correlationId) instead of PII values. GDPR Art. 5 — data minimization.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "LoggerMessage templates must not contain PII-indicative placeholders. "
            + "Structured log fields flow to log aggregation systems (ELK, Loki, Application Insights) "
            + "where they are indexed and searchable — exposing PII. "
            + "Log identifiers (userId, correlationId) instead of personal data.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        // Check if this is [LoggerMessage(...)]
        string attributeName = attribute.Name.ToString();
        if (attributeName != "LoggerMessage" && attributeName != "LoggerMessageAttribute")
        {
            return;
        }

        // Find the Message parameter value
        string messageTemplate = GetMessageParameter(attribute);
        if (messageTemplate.Length == 0)
        {
            return;
        }

        // Parse placeholders: {PlaceholderName} or {PlaceholderName:format}
        // Skip escaped braces: {{ and }}
        int startIndex = 0;
        while (startIndex < messageTemplate.Length)
        {
            int openBrace = messageTemplate.IndexOf('{', startIndex);
            if (openBrace < 0)
            {
                break;
            }

            // Skip escaped braces (double open-brace is an escape sequence)
            if (openBrace + 1 < messageTemplate.Length && messageTemplate[openBrace + 1] == '{')
            {
                startIndex = openBrace + 2;
                continue;
            }

            int closeBrace = messageTemplate.IndexOf('}', openBrace + 1);
            if (closeBrace < 0)
            {
                break;
            }

            string placeholder = messageTemplate.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Strip format specifier: {Value:F2} → Value
            int colonIndex = placeholder.IndexOf(':');
            if (colonIndex >= 0)
            {
                placeholder = placeholder.Substring(0, colonIndex);
            }

            if (IsPiiPlaceholder(placeholder))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(_rule, attribute.GetLocation(), placeholder));
            }

            startIndex = closeBrace + 1;
        }
    }

    private static string GetMessageParameter(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return string.Empty;
        }

        // First: check named argument — Message = "..."
        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is not null
                && argument.NameEquals.Name.Identifier.Text == "Message"
                && argument.Expression is LiteralExpressionSyntax namedLiteral
                && namedLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return namedLiteral.Token.ValueText;
            }
        }

        // Second: check positional form — [LoggerMessage(eventId, LogLevel, "template")]
        // The message template is the 3rd positional argument (index 2).
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
        if (arguments.Count >= 3
            && arguments[2].NameEquals is null
            && arguments[2].NameColon is null
            && arguments[2].Expression is LiteralExpressionSyntax positionalLiteral
            && positionalLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return positionalLiteral.Token.ValueText;
        }

        return string.Empty;
    }

    private static bool IsPiiPlaceholder(string name)
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
