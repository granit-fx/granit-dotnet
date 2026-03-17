using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GR-MOD001 — Reports an error when a type from another module's internal namespace
/// is referenced instead of using the module's <c>.Contracts</c> namespace.
/// </summary>
/// <remarks>
/// In a modular monolith, modules must communicate through their <c>.Contracts</c>
/// namespace. Direct references to internal module types create tight coupling
/// and violate module boundary isolation.
/// Opt-in: only activates when <c>*.Modules.*</c> namespaces are present in the compilation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrossModuleReferenceAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRMOD001";

    /// <summary>Property key for the target module name stored in diagnostic properties.</summary>
    public const string TargetModuleProperty = "TargetModule";

    /// <summary>Property key for the source module name stored in diagnostic properties.</summary>
    public const string SourceModuleProperty = "SourceModule";

    private const string ModulesSegment = ".Modules.";
    private const string ContractsSegment = "Contracts";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Cross-module reference to internal type — use Contracts",
        messageFormat: "Type '{0}' from module '{1}' cannot be referenced from module '{2}'. "
            + "Use types from the corresponding .Contracts namespace.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "In a modular monolith, modules must communicate through their .Contracts "
            + "namespace. Direct references to internal module types create tight coupling "
            + "and violate module boundary isolation.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterCompilationStartAction(compilationContext =>
        {
            if (!HasModulesNamespace(compilationContext.Compilation.GlobalNamespace))
            {
                return;
            }

            var sourceModuleCache = new ConcurrentDictionary<SyntaxTree, string?>();

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeTypeReference(nodeContext, sourceModuleCache),
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName);
        });

    private static void AnalyzeTypeReference(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, string?> sourceModuleCache)
    {
        // Avoid double-reporting on qualified names: only analyze the rightmost identifier.
        if (context.Node.Parent is QualifiedNameSyntax qualifiedName
            && qualifiedName.Left == context.Node)
        {
            return;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken);
        ISymbol? symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (symbol is INamedTypeSymbol namedType)
        {
            CheckTypeReference(context, namedType, sourceModuleCache);
        }
    }

    private static void CheckTypeReference(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol referencedType,
        ConcurrentDictionary<SyntaxTree, string?> sourceModuleCache)
    {
        string? targetNamespace = referencedType.ContainingNamespace?.ToDisplayString();
        if (targetNamespace is null)
        {
            return;
        }

        (string ModuleName, bool IsContracts)? targetInfo = ExtractModuleInfo(targetNamespace);
        if (targetInfo is null)
        {
            return;
        }

        if (targetInfo.Value.IsContracts)
        {
            return;
        }

        string? sourceModule = sourceModuleCache.GetOrAdd(
            context.Node.SyntaxTree,
            tree => GetSourceModule(tree));

        if (sourceModule is null)
        {
            return;
        }

        if (string.Equals(sourceModule, targetInfo.Value.ModuleName, StringComparison.Ordinal))
        {
            return;
        }

        var properties = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, string?>(TargetModuleProperty, targetInfo.Value.ModuleName),
            new KeyValuePair<string, string?>(SourceModuleProperty, sourceModule)
        });

        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            context.Node.GetLocation(),
            properties,
            referencedType.Name,
            targetInfo.Value.ModuleName,
            sourceModule));
    }

    private static string? GetSourceModule(SyntaxTree tree)
    {
        SyntaxNode root = tree.GetRoot();

        // File-scoped namespace declaration.
        FileScopedNamespaceDeclarationSyntax? fileScoped = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScoped is not null)
        {
            return ExtractModuleInfo(fileScoped.Name.ToString())?.ModuleName;
        }

        // Block namespace declaration.
        NamespaceDeclarationSyntax? blockNamespace = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (blockNamespace is not null)
        {
            return ExtractModuleInfo(blockNamespace.Name.ToString())?.ModuleName;
        }

        return null;
    }

    internal static (string ModuleName, bool IsContracts)? ExtractModuleInfo(string namespaceName)
    {
        int modulesIndex = namespaceName.IndexOf(ModulesSegment, StringComparison.Ordinal);
        if (modulesIndex < 0)
        {
            return null;
        }

        int moduleStart = modulesIndex + ModulesSegment.Length;
        int nextDot = namespaceName.IndexOf('.', moduleStart);

        string moduleName = nextDot >= 0
            ? namespaceName.Substring(moduleStart, nextDot - moduleStart)
            : namespaceName.Substring(moduleStart);

        if (moduleName.Length == 0)
        {
            return null;
        }

        bool isContracts = false;
        if (nextDot >= 0)
        {
            string afterModule = namespaceName.Substring(nextDot + 1);
            isContracts = afterModule.Equals(ContractsSegment, StringComparison.Ordinal)
                || afterModule.StartsWith(ContractsSegment + ".", StringComparison.Ordinal);
        }

        return (moduleName, isContracts);
    }

    private static bool HasModulesNamespace(INamespaceSymbol globalNamespace) =>
        globalNamespace.GetNamespaceMembers().Any(HasModulesNamespaceRecursive);

    private static bool HasModulesNamespaceRecursive(INamespaceSymbol ns)
    {
        if (ns.Name == "Modules" && ns.GetNamespaceMembers().Any())
        {
            return true;
        }

        if (ns.GetNamespaceMembers().Any(HasModulesNamespaceRecursive))
        {
            return true;
        }

        return false;
    }
}
