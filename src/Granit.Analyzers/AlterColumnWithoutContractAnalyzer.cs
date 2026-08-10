using Granit.Analyzers.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-MIGA004 — Reports a warning when <c>AlterColumn</c> is called with an <c>oldClrType</c>
/// argument that differs from the target type argument, and the migration class is not annotated
/// with <c>[MigrationCycle(MigrationPhase.Contract, ...)]</c>.
/// </summary>
/// <remarks>
/// Changing a column's CLR type (e.g., <c>int</c> → <c>string</c>) is a breaking schema change
/// that requires careful coordination in the Expand &amp; Contract pattern.
/// When <c>oldClrType</c> is absent (constraint-only modification), the rule does not fire.
/// Opt-in: only activates when <c>Granit.Persistence.Migrations</c> is referenced.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AlterColumnWithoutContractAnalyzer : GranitMigrationAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRMIGA004";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "AlterColumn with a type change requires a Contract-phase annotation",
        messageFormat: "Migration '{0}' changes a column type without [MigrationCycle(MigrationPhase.Contract, ...)]. "
            + "Type changes must be performed in the Contract phase.",
        category: "Migrations",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Changing a column's CLR type is a breaking change that must be coordinated "
            + "through the Expand & Contract pattern. Annotate the migration class with "
            + "[MigrationCycle(MigrationPhase.Contract, \"cycle-id\")] to suppress this warning.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override string TargetMethodName => "AlterColumn";

    /// <inheritdoc/>
    protected override void AnalyzeMigrationInvocation(
        SyntaxNodeAnalysisContext context,
        (INamedTypeSymbol MigrationClass, IMethodSymbol Method) migration,
        INamedTypeSymbol cycleAttrType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Find the oldClrType argument — if absent, this is a constraint-only change.
        ArgumentSyntax? oldClrTypeArg =
            MigrationAnalyzerHelpers.FindNamedArgument(invocation.ArgumentList, "oldClrType");
        if (oldClrTypeArg is null)
        {
            return;
        }

        // Compare oldClrType with the generic type argument T of AlterColumn<T>.
        if (migration.Method.TypeArguments.Length == 0)
        {
            return;
        }

        ITypeSymbol targetType = migration.Method.TypeArguments[0];

        // oldClrType must be typeof(SomeType) to be statically comparable.
        if (oldClrTypeArg.Expression is not TypeOfExpressionSyntax typeofExpr)
        {
            return;
        }

        ITypeSymbol? oldType = context.SemanticModel.GetTypeInfo(typeofExpr.Type, context.CancellationToken).Type;
        if (oldType is null)
        {
            return;
        }

        // Same type → not a type change, no warning.
        if (SymbolEqualityComparer.Default.Equals(targetType, oldType))
        {
            return;
        }

        // Type changed — require Contract annotation.
        if (MigrationAnalyzerHelpers.HasContractAnnotation(migration.MigrationClass, cycleAttrType))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(_rule, invocation.GetLocation(), migration.MigrationClass.Name));
    }
}
