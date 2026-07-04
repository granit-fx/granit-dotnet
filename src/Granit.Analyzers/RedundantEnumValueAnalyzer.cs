using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GRENUM001 — Reports a redundant explicit value on an enum member when the whole enum
/// merely restates the implicit ordinal sequence (<c>= 0, 1, 2, …</c>).
/// </summary>
/// <remarks>
/// <para>
/// Granit persists enum properties as their PascalCase string name (ADR-059) and serialises
/// them by name on the wire (<c>JsonStringEnumConverter</c>). A sequential-from-zero explicit
/// value is therefore inert — it changes neither the stored value nor the JSON — while
/// misleadingly implying a stable numeric contract, which discourages the safe
/// "append a member anywhere" refactor the string-persistence convention is designed to enable.
/// </para>
/// <para>
/// The analyzer is deliberately conservative. It fires only when <em>every</em> member carries a
/// plain decimal integer literal equal to its zero-based position. It never fires on:
/// </para>
/// <list type="bullet">
///   <item><c>[Flags]</c> enums — bitmask composition needs explicit power-of-two values.</item>
///   <item>Enums consumed by a <c>[PersistAsInt]</c> property or field anywhere in the
///     compilation — there the integer is the persisted contract and reordering corrupts data.</item>
///   <item>Enums with a gap, a non-zero start, or a non-literal initializer
///     (<c>1 &lt;&lt; 2</c>, <c>Other</c>, <c>301</c>) — a deliberate numeric contract.</item>
/// </list>
/// <para>Suppress with <c>[SuppressMessage("Design", "GRENUM001")]</c> for the rare enum whose
/// values are load-bearing in a way the analyzer cannot see (e.g. an external numeric protocol).</para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantEnumValueAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRENUM001";

    private const string PersistAsIntAttributeMetadataName = "Granit.Domain.PersistAsIntAttribute";
    private const string FlagsAttributeMetadataName = "System.FlagsAttribute";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Redundant explicit enum value",
        messageFormat: "Enum member '{0}' explicitly assigns its implicit ordinal value; omit '= {1}' — "
            + "Granit persists enums by name (ADR-059), so explicit values matter only for "
            + "[Flags] or [PersistAsInt] enums",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Granit persists enum properties as their PascalCase string name and serialises "
            + "them by name on the wire (ADR-059). Explicit sequential-from-zero values are inert and "
            + "misleadingly imply a numeric contract, discouraging the safe 'append a member anywhere' "
            + "refactor the string-persistence convention enables. Reserve explicit values for [Flags] "
            + "bitmasks and [PersistAsInt] columns, where the integer is a real contract.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterCompilationStartAction(OnCompilationStart);

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        Compilation compilation = context.Compilation;
        INamedTypeSymbol? persistAsIntAttribute =
            compilation.GetTypeByMetadataName(PersistAsIntAttributeMetadataName);

        // Enum types whose underlying integer IS a persisted contract because a [PersistAsInt]
        // member somewhere in the compilation stores them as an integer column. Computed on first
        // use, so a single walk covers all enum declarations regardless of analysis order.
        var persistedAsIntEnums = new Lazy<ImmutableHashSet<INamedTypeSymbol>>(
            () => CollectPersistedAsIntEnums(compilation, persistAsIntAttribute));

        context.RegisterSyntaxNodeAction(
            syntaxContext => AnalyzeEnum(syntaxContext, persistedAsIntEnums),
            SyntaxKind.EnumDeclaration);
    }

    private static void AnalyzeEnum(SyntaxNodeAnalysisContext context, Lazy<ImmutableHashSet<INamedTypeSymbol>> persistedAsIntEnums)
    {
        var enumDeclaration = (EnumDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(enumDeclaration, context.CancellationToken)
            is not INamedTypeSymbol enumSymbol)
        {
            return;
        }

        // [Flags] enums need explicit power-of-two values — never redundant.
        if (enumSymbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == FlagsAttributeMetadataName))
        {
            return;
        }

        SeparatedSyntaxList<EnumMemberDeclarationSyntax> members = enumDeclaration.Members;
        if (members.Count == 0)
        {
            return;
        }

        var redundant = new List<Diagnostic>(members.Count);

        for (int index = 0; index < members.Count; index++)
        {
            EnumMemberDeclarationSyntax member = members[index];
            EqualsValueClauseSyntax? equalsValue = member.EqualsValue;

            // A member without an explicit value means this is not the fully-restated pattern.
            if (equalsValue is null)
            {
                return;
            }

            // Only plain decimal integer literals count. `1 << 2`, `A | B`, `Other`, `-1`, `0x01`
            // all signal a deliberate value and disqualify the whole enum.
            if (equalsValue.Value is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.NumericLiteralExpression)
                || !IsPlainDecimalLiteral(literal.Token.Text))
            {
                return;
            }

            Optional<object?> constant = context.SemanticModel.GetConstantValue(literal, context.CancellationToken);
            if (!constant.HasValue || ToInt64(constant.Value) is not { } value || value != index)
            {
                return;
            }

            redundant.Add(Diagnostic.Create(
                _rule, equalsValue.GetLocation(), member.Identifier.ValueText, index));
        }

        // The integer is a persisted contract for this enum — leave the explicit values alone.
        if (persistedAsIntEnums.Value.Contains(enumSymbol))
        {
            return;
        }

        foreach (Diagnostic diagnostic in redundant)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static ImmutableHashSet<INamedTypeSymbol> CollectPersistedAsIntEnums(
        Compilation compilation,
        INamedTypeSymbol? persistAsIntAttribute)
    {
        if (persistAsIntAttribute is null)
        {
            return ImmutableHashSet<INamedTypeSymbol>.Empty;
        }

        ImmutableHashSet<INamedTypeSymbol>.Builder builder =
            ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (INamedTypeSymbol type in EnumerateNamedTypes(compilation.GlobalNamespace))
        {
            foreach (ISymbol member in type.GetMembers())
            {
                ITypeSymbol? memberType = member switch
                {
                    IPropertySymbol property => property.Type,
                    IFieldSymbol field => field.Type,
                    _ => null,
                };

                if (memberType is null)
                {
                    continue;
                }

                bool hasPersistAsInt = member.GetAttributes().Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, persistAsIntAttribute));

                if (hasPersistAsInt && UnwrapEnum(memberType) is { } enumType)
                {
                    builder.Add(enumType);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol root)
    {
        foreach (INamespaceOrTypeSymbol member in root.GetMembers())
        {
            if (member is INamespaceSymbol childNamespace)
            {
                foreach (INamedTypeSymbol nested in EnumerateNamedTypes(childNamespace))
                {
                    yield return nested;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;

                foreach (INamedTypeSymbol nested in EnumerateNestedTypes(type))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
    {
        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            yield return nested;

            foreach (INamedTypeSymbol deeper in EnumerateNestedTypes(nested))
            {
                yield return deeper;
            }
        }
    }

    private static bool IsPlainDecimalLiteral(string tokenText) =>
        tokenText.Length > 0 && tokenText.All(static character => character is >= '0' and <= '9');

    private static INamedTypeSymbol? UnwrapEnum(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length == 1
            && nullable.TypeArguments[0] is INamedTypeSymbol underlying)
        {
            type = underlying;
        }

        return type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType ? enumType : null;
    }

    private static long? ToInt64(object? value) => value switch
    {
        sbyte number => number,
        byte number => number,
        short number => number,
        ushort number => number,
        int number => number,
        uint number => number,
        long number => number,
        ulong number when number <= long.MaxValue => (long)number,
        _ => null,
    };
}
