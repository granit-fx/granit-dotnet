using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-SEC003 — Flags constructions of <c>Granit.Privacy.DataExport.PrivacyExportContext</c>
/// where <c>SubjectUserId</c> and <c>CallerUserId</c> are bound to different symbols.
/// </summary>
/// <remarks>
/// <para>
/// The framework's v1 privacy-export pipeline is strictly self-service: only the data
/// subject can export their own data. Construction sites that pass distinct identifiers
/// for caller and subject are reserved for the future <c>Privacy.Exports.OnBehalfOf</c>
/// path (admin DSR), which must verify the permission and audit the substitution
/// before the context reaches a provider.
/// </para>
/// <para>
/// This analyzer fires when the two argument expressions resolve to different local
/// symbols — the most common bug shape (typo, paste error). Cases the analyzer
/// deliberately doesn't catch:
/// </para>
/// <list type="bullet">
///   <item>Calls where both arguments are non-identifier expressions (e.g. literal
///         method calls). Treating those as "different" would noise up every
///         <c>Guid.NewGuid()</c>-style test construction.</item>
///   <item>The two arguments referencing the same symbol via different access paths
///         (e.g. <c>user.Id</c> vs <c>user.Id</c>) — handled by ISymbol equality.</item>
///   <item>Record <c>with</c> expressions that override one of the two fields. Those
///         need dataflow tracking that the analyzer doesn't do today.</item>
/// </list>
/// <para>
/// Suppress with <c>#pragma warning disable GRSEC005</c> after the admin-DSR
/// permission check is in place.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivacyExportSubjectSubstitutionAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRSEC005";

    private const string PrivacyExportContextFullName = "Granit.Privacy.DataExport.PrivacyExportContext";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "PrivacyExportContext subject must equal caller",
        messageFormat: "PrivacyExportContext is being constructed with SubjectUserId ({0}) "
            + "different from CallerUserId ({1}); v1 of the export pipeline is self-service only. "
            + "Pass the same identifier for both, or suppress GRSEC005 once a "
            + "Privacy.Exports.OnBehalfOf permission check guards this construction.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Self-service exports must construct PrivacyExportContext with caller "
            + "equal to subject. Mismatched identifiers are reserved for the admin DSR path "
            + "(Privacy.Exports.OnBehalfOf), which has to verify the permission and audit the "
            + "substitution before the context reaches a provider.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count < 3)
        {
            return;
        }

        ISymbol? typeSymbol = context.SemanticModel.GetSymbolInfo(creation).Symbol;
        if (typeSymbol is not IMethodSymbol ctor || ctor.MethodKind != MethodKind.Constructor)
        {
            return;
        }

        if (ctor.ContainingType.ToDisplayString() != PrivacyExportContextFullName)
        {
            return;
        }

        ArgumentSyntax? subjectArg = null;
        ArgumentSyntax? callerArg = null;

        for (int i = 0; i < creation.ArgumentList.Arguments.Count; i++)
        {
            ArgumentSyntax arg = creation.ArgumentList.Arguments[i];
            string? parameterName = arg.NameColon?.Name.Identifier.Text
                ?? (i < ctor.Parameters.Length ? ctor.Parameters[i].Name : null);

            if (parameterName == "SubjectUserId")
            {
                subjectArg = arg;
            }
            else if (parameterName == "CallerUserId")
            {
                callerArg = arg;
            }
        }

        if (subjectArg is null || callerArg is null)
        {
            return;
        }

        ISymbol? subjectSymbol = context.SemanticModel.GetSymbolInfo(subjectArg.Expression).Symbol;
        ISymbol? callerSymbol = context.SemanticModel.GetSymbolInfo(callerArg.Expression).Symbol;

        // Skip when either argument doesn't bind to a symbol (literals, method calls,
        // dynamic expressions) — over-flagging those would dwarf the true-positive rate.
        if (subjectSymbol is null || callerSymbol is null)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(subjectSymbol, callerSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            creation.GetLocation(),
            subjectArg.Expression.ToString(),
            callerArg.Expression.ToString()));
    }
}
