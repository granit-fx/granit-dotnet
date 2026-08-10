using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers.Internal;

/// <summary>
/// Shared helpers for migration Roslyn analyzers.
/// </summary>
internal static class MigrationAnalyzerHelpers
{
    private const string MigrationBuilderFqn =
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder";

    private const string MigrationCycleAttributeFqn =
        "Granit.Persistence.Migrations.MigrationCycleAttribute";

    private const string EfCoreMigrationFqn =
        "Microsoft.EntityFrameworkCore.Migrations.Migration";

    /// <summary>Integer value of <c>MigrationPhase.Contract</c>.</summary>
    private const int ContractPhaseValue = 2;

    // -------------------------------------------------------------------------
    // Symbol resolution helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves both <c>MigrationCycleAttribute</c> (Granit opt-in) and the EF Core
    /// <c>Migration</c> base class from the compilation. Returns <see langword="null"/> if
    /// either symbol is missing (i.e., the opt-in package is not referenced).
    /// </summary>
    internal static (INamedTypeSymbol CycleAttr, INamedTypeSymbol Migration)?
        ResolveGranitMigrationSymbols(Compilation compilation)
    {
        INamedTypeSymbol? cycleAttr = compilation
            .GetTypeByMetadataName(MigrationCycleAttributeFqn);
        if (cycleAttr is null)
        {
            return null;
        }

        INamedTypeSymbol? migration = compilation
            .GetTypeByMetadataName(EfCoreMigrationFqn);
        if (migration is null)
        {
            return null;
        }

        return (cycleAttr, migration);
    }

    /// <summary>
    /// Resolves the EF Core <c>Migration</c> base class from the compilation.
    /// Returns <see langword="null"/> when EF Core is not referenced.
    /// </summary>
    internal static INamedTypeSymbol? ResolveEfCoreMigrationSymbol(Compilation compilation) =>
        compilation.GetTypeByMetadataName(EfCoreMigrationFqn);

    // -------------------------------------------------------------------------
    // Invocation guard
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates that the current syntax node is an invocation of
    /// <paramref name="methodName"/> on <c>MigrationBuilder</c> inside a class
    /// that inherits from <paramref name="migrationBase"/>.
    /// Returns the enclosing migration class and the resolved method symbol, or
    /// <see langword="null"/> if any guard fails.
    /// </summary>
    internal static (INamedTypeSymbol MigrationClass, IMethodSymbol Method)?
        TryGetMigrationInvocation(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol migrationBase,
            string methodName)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        if (memberAccess.Name.Identifier.Text != methodName)
        {
            return null;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        if (methodSymbol.ContainingType.ToDisplayString() != MigrationBuilderFqn)
        {
            return null;
        }

        INamedTypeSymbol? migrationClass = GetContainingClass(invocation, context.SemanticModel);
        if (migrationClass is null)
        {
            return null;
        }

        if (!InheritsFromMigration(migrationClass, migrationBase))
        {
            return null;
        }

        return (migrationClass, methodSymbol);
    }

    // -------------------------------------------------------------------------
    // Argument helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds the first named argument whose label matches <paramref name="name"/>,
    /// or <see langword="null"/> if none is found.
    /// </summary>
    internal static ArgumentSyntax? FindNamedArgument(ArgumentListSyntax argList, string name)
    {
        foreach (ArgumentSyntax arg in argList.Arguments)
        {
            if (arg.NameColon?.Name.Identifier.Text == name)
            {
                return arg;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the argument list contains a named argument
    /// whose constant value evaluates to <see langword="true"/>.
    /// </summary>
    internal static bool HasNamedArgumentWithTrueValue(
        ArgumentListSyntax argList,
        string name,
        SemanticModel semanticModel)
    {
        foreach (ArgumentSyntax arg in argList.Arguments)
        {
            if (arg.NameColon?.Name.Identifier.Text != name)
            {
                continue;
            }

            Microsoft.CodeAnalysis.Optional<object?> constant =
                semanticModel.GetConstantValue(arg.Expression);
            return constant.HasValue && constant.Value is true;
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the argument list contains a named argument
    /// with the given <paramref name="name"/>.
    /// </summary>
    internal static bool HasNamedArgument(ArgumentListSyntax argList, string name)
    {
        foreach (ArgumentSyntax arg in argList.Arguments)
        {
            if (arg.NameColon?.Name.Identifier.Text == name)
            {
                return true;
            }
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Type hierarchy helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> inherits (directly or
    /// indirectly) from <paramref name="migrationBase"/>.
    /// </summary>
    internal static bool InheritsFromMigration(INamedTypeSymbol type, INamedTypeSymbol migrationBase)
    {
        INamedTypeSymbol? current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, migrationBase))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="classSymbol"/> is annotated with
    /// <c>[MigrationCycle(MigrationPhase.Contract, ...)]</c>.
    /// </summary>
    internal static bool HasContractAnnotation(
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol cycleAttrType)
    {
        foreach (AttributeData attr in classSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, cycleAttrType))
            {
                continue;
            }

            if (attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Value is int phase
                && phase == ContractPhaseValue)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Walks up the syntax tree from <paramref name="node"/> and returns the
    /// <see cref="INamedTypeSymbol"/> of the immediately enclosing class declaration,
    /// or <see langword="null"/> if none is found.
    /// </summary>
    internal static INamedTypeSymbol? GetContainingClass(SyntaxNode node, SemanticModel semanticModel)
    {
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is ClassDeclarationSyntax classDecl)
            {
                return semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            }

            current = current.Parent;
        }

        return null;
    }
}
