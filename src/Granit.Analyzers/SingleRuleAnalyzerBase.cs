using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// Abstract base for Roslyn analyzers that expose exactly one <see cref="DiagnosticDescriptor"/>.
/// Provides <see cref="SupportedDiagnostics"/> and the <see cref="Initialize"/> scaffold
/// (concurrent execution enabled, generated code excluded), then delegates to
/// <see cref="RegisterActions"/> for action registration.
/// </summary>
public abstract class SingleRuleAnalyzerBase : DiagnosticAnalyzer
{
    /// <summary>Gets the single diagnostic descriptor for this analyzer.</summary>
    protected abstract DiagnosticDescriptor Rule { get; }

    /// <inheritdoc/>
    public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public sealed override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        RegisterActions(context);
    }

    /// <summary>
    /// Registers syntax node or compilation-start actions on the provided context.
    /// Called once per analysis session after the standard setup in <see cref="Initialize"/>.
    /// </summary>
    protected abstract void RegisterActions(AnalysisContext context);
}
