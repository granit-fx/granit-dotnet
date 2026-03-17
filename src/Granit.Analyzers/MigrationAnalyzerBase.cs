using Granit.Analyzers.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// Abstract base for migration analyzers. Extends <see cref="SingleRuleAnalyzerBase"/>
/// and wires <see cref="SingleRuleAnalyzerBase.RegisterActions"/> to
/// <see cref="OnCompilationStart"/> via <c>RegisterCompilationStartAction</c>.
/// </summary>
public abstract class MigrationAnalyzerBase : SingleRuleAnalyzerBase
{
    /// <inheritdoc/>
    protected sealed override void RegisterActions(AnalysisContext context)
        => context.RegisterCompilationStartAction(OnCompilationStart);

    /// <summary>
    /// Resolves required symbols and registers the syntax node action.
    /// </summary>
    protected abstract void OnCompilationStart(CompilationStartAnalysisContext context);
}

/// <summary>
/// Base for migration analyzers that opt in to <c>Granit.Persistence.Migrations</c>.
/// Resolves <c>MigrationCycleAttribute</c> and the EF Core <c>Migration</c> base class,
/// filters invocations to <see cref="TargetMethodName"/>, then delegates to
/// <see cref="AnalyzeMigrationInvocation"/>.
/// </summary>
public abstract class GranitMigrationAnalyzerBase : MigrationAnalyzerBase
{
    /// <summary>The <c>MigrationBuilder</c> method name this analyzer targets (e.g. "DropColumn").</summary>
    protected abstract string TargetMethodName { get; }

    /// <inheritdoc/>
    protected sealed override void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        (INamedTypeSymbol CycleAttr, INamedTypeSymbol Migration)? symbols =
            MigrationAnalyzerHelpers.ResolveGranitMigrationSymbols(context.Compilation);
        if (symbols is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext =>
            {
                (INamedTypeSymbol MigrationClass, IMethodSymbol Method)? result =
                    MigrationAnalyzerHelpers.TryGetMigrationInvocation(
                        nodeContext, symbols.Value.Migration, TargetMethodName);
                if (result is null)
                {
                    return;
                }

                AnalyzeMigrationInvocation(nodeContext, result.Value, symbols.Value.CycleAttr);
            },
            SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// Analyzes a validated <see cref="TargetMethodName"/> invocation inside a Granit migration.
    /// Called only when <see cref="MigrationAnalyzerHelpers.TryGetMigrationInvocation"/> succeeds.
    /// </summary>
    protected abstract void AnalyzeMigrationInvocation(
        SyntaxNodeAnalysisContext context,
        (INamedTypeSymbol MigrationClass, IMethodSymbol Method) migration,
        INamedTypeSymbol cycleAttrType);
}

/// <summary>
/// Base for migration analyzers that require only EF Core (no Granit opt-in).
/// Resolves the EF Core <c>Migration</c> base class symbol, filters invocations
/// to <see cref="TargetMethodName"/>, then delegates to <see cref="AnalyzeMigrationInvocation"/>.
/// </summary>
public abstract class EfCoreMigrationAnalyzerBase : MigrationAnalyzerBase
{
    /// <summary>The <c>MigrationBuilder</c> method name this analyzer targets (e.g. "RenameColumn").</summary>
    protected abstract string TargetMethodName { get; }

    /// <inheritdoc/>
    protected sealed override void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        INamedTypeSymbol? migrationSymbol =
            MigrationAnalyzerHelpers.ResolveEfCoreMigrationSymbol(context.Compilation);
        if (migrationSymbol is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext =>
            {
                (INamedTypeSymbol MigrationClass, IMethodSymbol Method)? result =
                    MigrationAnalyzerHelpers.TryGetMigrationInvocation(
                        nodeContext, migrationSymbol, TargetMethodName);
                if (result is null)
                {
                    return;
                }

                AnalyzeMigrationInvocation(nodeContext, result.Value);
            },
            SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// Analyzes a validated <see cref="TargetMethodName"/> invocation inside an EF Core migration.
    /// Called only when <see cref="MigrationAnalyzerHelpers.TryGetMigrationInvocation"/> succeeds.
    /// </summary>
    protected abstract void AnalyzeMigrationInvocation(
        SyntaxNodeAnalysisContext context,
        (INamedTypeSymbol MigrationClass, IMethodSymbol Method) migration);
}
