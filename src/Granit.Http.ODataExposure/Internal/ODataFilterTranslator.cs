using System.Globalization;
using Granit.QueryEngine.Filtering;

namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Pure translation of an OData <c>$filter</c> AST (<see cref="FilterClause"/>) into the
/// QueryEngine's strict <see cref="QueryPredicate"/> tree — the single enforcement point for
/// filterable-column whitelists and filter semantics. Anything the engine cannot express is
/// rejected explicitly with an actionable detail instead of being composed as arbitrary LINQ
/// on top of the engine (the pre-#3004 behaviour, which re-derived none of the
/// QueryDefinition's rules).
/// </summary>
/// <remarks>
/// <para>Supported constructs:</para>
/// <list type="bullet">
///   <item><c>and</c> / <c>or</c> / <c>not</c> → <see cref="QueryPredicate.And"/> / <see cref="QueryPredicate.Or"/> / <see cref="QueryPredicate.Not"/>.</item>
///   <item><c>eq</c> / <c>ne</c> → <see cref="FilterOperator.Eq"/> / <see cref="FilterOperator.Ne"/>; a <c>null</c> literal on either side becomes <see cref="FilterOperator.IsNull"/> / <see cref="FilterOperator.IsNotNull"/>.</item>
///   <item><c>gt</c> / <c>ge</c> / <c>lt</c> / <c>le</c> → <see cref="FilterOperator.Gt"/> / <see cref="FilterOperator.Gte"/> / <see cref="FilterOperator.Lt"/> / <see cref="FilterOperator.Lte"/> (operator mirrored when the literal is on the left).</item>
///   <item><c>contains</c> / <c>startswith</c> / <c>endswith</c> over an entity-local property and a string literal.</item>
///   <item><c>in</c> with a literal list → <see cref="FilterOperator.In"/> (comma-joined values).</item>
///   <item><see cref="ConvertNode"/>s are unwrapped transparently (the parser inserts them around promoted literals and nullable properties).</item>
/// </list>
/// <para>
/// Everything else — lambdas (<c>any</c>/<c>all</c>), arithmetic, <c>has</c>, unary minus,
/// cross-navigation property access, every other canonical function, dynamic properties,
/// <c>$count</c> segments — is rejected with a distinct <see cref="ODataFilterTranslationResult.RejectionDetail"/>.
/// </para>
/// <para>
/// Literals are stringified to exactly what the engine's value parser
/// (<c>FilterExpressionBuilder.ConvertValue</c>) round-trips with
/// <see cref="CultureInfo.InvariantCulture"/>: <see cref="DateTimeOffset"/> as ISO-8601
/// round-trip (<c>"O"</c>), <c>Edm.Date</c> as <c>yyyy-MM-dd</c>, enums as the member name,
/// <see cref="Guid"/>/<see cref="bool"/>/numerics invariant.
/// </para>
/// </remarks>
internal static class ODataFilterTranslator
{
    /// <summary>
    /// Translates the given <c>$filter</c> clause. Never throws for unsupported shapes —
    /// rejections are carried on the result so the caller can emit a 400 Problem + metric.
    /// </summary>
    /// <param name="filterClause">The parsed filter clause from <c>ODataQueryOptions.Filter.FilterClause</c>.</param>
    public static ODataFilterTranslationResult Translate(FilterClause filterClause)
    {
        ArgumentNullException.ThrowIfNull(filterClause);
        return TranslateBoolean(filterClause.Expression);
    }

    /// <summary>Dispatches one boolean-valued node of the AST.</summary>
    private static ODataFilterTranslationResult TranslateBoolean(QueryNode node) =>
        Unwrap(node) switch
        {
            BinaryOperatorNode binary => TranslateBinary(binary),
            UnaryOperatorNode unary => TranslateUnary(unary),
            SingleValueFunctionCallNode function => TranslateFunction(function),
            InNode inNode => TranslateIn(inNode),
            AnyNode => ODataFilterTranslationResult.Rejected(
                nameof(AnyNode),
                "The any(...) lambda operator is not supported. Filter on entity-local columns only; ask the API owner to expose the aggregated value as a dedicated column if you need it."),
            AllNode => ODataFilterTranslationResult.Rejected(
                nameof(AllNode),
                "The all(...) lambda operator is not supported. Filter on entity-local columns only; ask the API owner to expose the aggregated value as a dedicated column if you need it."),
            CountNode => ODataFilterTranslationResult.Rejected(
                nameof(CountNode),
                "Filtering on a collection $count segment is not supported. Ask the API owner to expose the count as a dedicated column if you need it."),
            SingleValueOpenPropertyAccessNode open => ODataFilterTranslationResult.Rejected(
                nameof(SingleValueOpenPropertyAccessNode),
                $"Dynamic property '{open.Name}' is not supported — only properties declared in $metadata can be filtered."),
            SingleValuePropertyAccessNode property => ODataFilterTranslationResult.Rejected(
                nameof(SingleValuePropertyAccessNode),
                $"A bare boolean property is not supported as a filter — compare it explicitly: '{property.Property.Name} eq true'."),
            var other => ODataFilterTranslationResult.Rejected(
                other.GetType().Name,
                $"The OData construct '{other.GetType().Name}' is not supported in $filter on this feed."),
        };

    private static ODataFilterTranslationResult TranslateBinary(BinaryOperatorNode binary) =>
        binary.OperatorKind switch
        {
            BinaryOperatorKind.And => TranslateComposite(binary, static (l, r) => QueryPredicate.And(l, r)),
            BinaryOperatorKind.Or => TranslateComposite(binary, static (l, r) => QueryPredicate.Or(l, r)),
            BinaryOperatorKind.Equal => TranslateEquality(binary, negated: false),
            BinaryOperatorKind.NotEqual => TranslateEquality(binary, negated: true),
            BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual
                or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual =>
                TranslateComparison(binary),
            BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply
                or BinaryOperatorKind.Divide or BinaryOperatorKind.Modulo =>
                RejectArithmetic(),
            BinaryOperatorKind.Has => ODataFilterTranslationResult.Rejected(
                nameof(BinaryOperatorNode),
                "The 'has' flag-enum operator is not supported. Use 'eq' against a single enum member instead."),
            _ => ODataFilterTranslationResult.Rejected(
                nameof(BinaryOperatorNode),
                $"The OData binary operator '{binary.OperatorKind}' is not supported in $filter on this feed."),
        };

    private static ODataFilterTranslationResult TranslateComposite(
        BinaryOperatorNode binary,
        Func<QueryPredicate, QueryPredicate, QueryPredicate> combine)
    {
        ODataFilterTranslationResult left = TranslateBoolean(binary.Left);
        if (left.RejectionDetail is not null)
        {
            return left;
        }

        ODataFilterTranslationResult right = TranslateBoolean(binary.Right);
        if (right.RejectionDetail is not null)
        {
            return right;
        }

        return ODataFilterTranslationResult.Success(combine(left.Predicate!, right.Predicate!));
    }

    private static ODataFilterTranslationResult TranslateUnary(UnaryOperatorNode unary)
    {
        if (unary.OperatorKind != UnaryOperatorKind.Not)
        {
            return ODataFilterTranslationResult.Rejected(
                nameof(UnaryOperatorNode),
                "The unary minus operator is not supported — compare against the literal value directly.");
        }

        ODataFilterTranslationResult operand = TranslateBoolean(unary.Operand);
        return operand.RejectionDetail is not null
            ? operand
            : ODataFilterTranslationResult.Success(QueryPredicate.Not(operand.Predicate!));
    }

    /// <summary>
    /// <c>eq</c> / <c>ne</c>: a <c>null</c> literal on either side becomes an
    /// <see cref="FilterOperator.IsNull"/> / <see cref="FilterOperator.IsNotNull"/> null check
    /// on the property operand; otherwise a plain Eq/Ne leaf (property on either side).
    /// </summary>
    private static ODataFilterTranslationResult TranslateEquality(BinaryOperatorNode binary, bool negated)
    {
        QueryNode left = Unwrap(binary.Left);
        QueryNode right = Unwrap(binary.Right);

        bool leftIsNull = left is ConstantNode { Value: null };
        bool rightIsNull = right is ConstantNode { Value: null };

        if (leftIsNull || rightIsNull)
        {
            if (leftIsNull && rightIsNull)
            {
                return ODataFilterTranslationResult.Rejected(
                    nameof(BinaryOperatorNode),
                    "Comparing null to null is not supported — one side of eq/ne must be an entity-local property.");
            }

            QueryNode propertyOperand = leftIsNull ? right : left;
            if (!TryResolveEntityLocalProperty(propertyOperand, out string nullCheckedField, out ODataFilterTranslationResult? nullRejection))
            {
                return nullRejection
                    ?? ClassifyOperandRejection(propertyOperand)
                    ?? ODataFilterTranslationResult.Rejected(
                        nameof(BinaryOperatorNode),
                        "A null comparison must have an entity-local property on the other side.");
            }

            return ODataFilterTranslationResult.Success(
                QueryPredicate.Criterion(nullCheckedField, negated ? FilterOperator.IsNotNull : FilterOperator.IsNull));
        }

        if (!TryResolvePropertyAndConstant(left, right, out string field, out ConstantNode? constant, out _,
                out ODataFilterTranslationResult? rejection))
        {
            return rejection!;
        }

        if (!TryStringifyConstant((ConstantNode)constant!, out string? value, out ODataFilterTranslationResult? constantRejection))
        {
            return constantRejection!;
        }

        return ODataFilterTranslationResult.Success(
            QueryPredicate.Criterion(field, negated ? FilterOperator.Ne : FilterOperator.Eq, value!));
    }

    /// <summary>
    /// <c>gt</c> / <c>ge</c> / <c>lt</c> / <c>le</c>. A <c>null</c> literal is rejected (null has
    /// no ordering — the engine's range operators require a value). When the literal is on the
    /// left (<c>100 lt Amount</c>) the operator is mirrored (<c>Amount gt 100</c>).
    /// </summary>
    private static ODataFilterTranslationResult TranslateComparison(BinaryOperatorNode binary)
    {
        QueryNode left = Unwrap(binary.Left);
        QueryNode right = Unwrap(binary.Right);

        if (left is ConstantNode { Value: null } || right is ConstantNode { Value: null })
        {
            return ODataFilterTranslationResult.Rejected(
                nameof(BinaryOperatorNode),
                $"A null literal cannot be ordered with '{binary.OperatorKind}' — use 'eq null' or 'ne null' for null checks.");
        }

        if (!TryResolvePropertyAndConstant(left, right, out string field, out ConstantNode? constant, out bool mirrored,
                out ODataFilterTranslationResult? rejection))
        {
            return rejection!;
        }

        if (!TryStringifyConstant((ConstantNode)constant!, out string? value, out ODataFilterTranslationResult? constantRejection))
        {
            return constantRejection!;
        }

        FilterOperator op = (binary.OperatorKind, mirrored) switch
        {
            (BinaryOperatorKind.GreaterThan, false) or (BinaryOperatorKind.LessThan, true) => FilterOperator.Gt,
            (BinaryOperatorKind.GreaterThanOrEqual, false) or (BinaryOperatorKind.LessThanOrEqual, true) => FilterOperator.Gte,
            (BinaryOperatorKind.LessThan, false) or (BinaryOperatorKind.GreaterThan, true) => FilterOperator.Lt,
            _ => FilterOperator.Lte,
        };

        return ODataFilterTranslationResult.Success(QueryPredicate.Criterion(field, op, value!));
    }

    /// <summary>
    /// <c>contains</c> / <c>startswith</c> / <c>endswith</c> over an entity-local property and a
    /// string literal. Every other canonical function is rejected by name.
    /// </summary>
    private static ODataFilterTranslationResult TranslateFunction(SingleValueFunctionCallNode function)
    {
        FilterOperator? op = function.Name.ToLowerInvariant() switch
        {
            "contains" => FilterOperator.Contains,
            "startswith" => FilterOperator.StartsWith,
            "endswith" => FilterOperator.EndsWith,
            _ => null,
        };

        if (op is null)
        {
            return ODataFilterTranslationResult.Rejected(
                nameof(SingleValueFunctionCallNode),
                $"The OData function '{function.Name}' is not supported. Supported functions: contains(Property, 'value'), startswith(Property, 'value'), endswith(Property, 'value') over an entity-local string column.");
        }

        QueryNode[] parameters = [.. function.Parameters];
        if (parameters.Length != 2)
        {
            return RejectFunctionShape(function.Name);
        }

        if (!TryResolveEntityLocalProperty(parameters[0], out string field, out ODataFilterTranslationResult? rejection))
        {
            return rejection ?? RejectFunctionShape(function.Name);
        }

        if (Unwrap(parameters[1]) is not ConstantNode { Value: string text })
        {
            return RejectFunctionShape(function.Name);
        }

        return ODataFilterTranslationResult.Success(QueryPredicate.Criterion(field, op.Value, text));
    }

    /// <summary>
    /// <c>in</c> with a literal list. The engine's <see cref="FilterOperator.In"/> parser splits
    /// the criterion value on commas (trimming entries and dropping empty ones), so items that
    /// would not round-trip — strings containing a comma, empty/whitespace-only strings, strings
    /// with leading or trailing whitespace, and <c>null</c> — are rejected instead of silently
    /// changing the match semantics.
    /// </summary>
    private static ODataFilterTranslationResult TranslateIn(InNode inNode)
    {
        if (!TryResolveEntityLocalProperty(inNode.Left, out string field, out ODataFilterTranslationResult? rejection))
        {
            return rejection ?? ODataFilterTranslationResult.Rejected(
                nameof(InNode),
                "The left operand of 'in' must be an entity-local property.");
        }

        if (inNode.Right is not CollectionConstantNode collection || collection.Collection.Count == 0)
        {
            return ODataFilterTranslationResult.Rejected(
                nameof(InNode),
                "The right operand of 'in' must be a non-empty parenthesised list of literals, e.g. Status in ('Paid','Sent').");
        }

        List<string> items = new(collection.Collection.Count);
        foreach (ConstantNode item in collection.Collection)
        {
            if (!TryStringifyConstant(item, out string? value, out ODataFilterTranslationResult? itemRejection))
            {
                return itemRejection!;
            }

            if (value is null)
            {
                return ODataFilterTranslationResult.Rejected(
                    nameof(InNode),
                    $"A null literal cannot appear in an 'in' list — combine with 'or {field} eq null' instead.");
            }

            if (value.Contains(',', StringComparison.Ordinal))
            {
                return ODataFilterTranslationResult.Rejected(
                    nameof(InNode),
                    $"The 'in' list item '{value}' contains a comma, which the query engine uses as the list separator — use separate 'eq' comparisons combined with 'or' instead.");
            }

            if (value.Length == 0 || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return ODataFilterTranslationResult.Rejected(
                    nameof(InNode),
                    "An 'in' list item that is empty, whitespace-only, or has leading/trailing whitespace would not round-trip through the query engine's list parser — use separate 'eq' comparisons combined with 'or' instead.");
            }

            items.Add(value);
        }

        return ODataFilterTranslationResult.Success(
            QueryPredicate.Criterion(field, FilterOperator.In, string.Join(',', items)));
    }

    /// <summary>
    /// Finds the (property, literal) pair of a binary comparison, accepting either operand order.
    /// <paramref name="mirrored"/> is <see langword="true"/> when the property sat on the right
    /// (the caller must mirror relational operators). Produces the most specific rejection
    /// available when neither shape matches (cross-navigation, arithmetic, function, …).
    /// </summary>
    private static bool TryResolvePropertyAndConstant(
        QueryNode left,
        QueryNode right,
        out string field,
        out ConstantNode? constant,
        out bool mirrored,
        out ODataFilterTranslationResult? rejection)
    {
        constant = null;
        mirrored = false;

        if (TryResolveEntityLocalProperty(left, out field, out ODataFilterTranslationResult? leftRejection)
            && right is ConstantNode rightConstant)
        {
            constant = rightConstant;
            rejection = null;
            return true;
        }

        if (TryResolveEntityLocalProperty(right, out field, out ODataFilterTranslationResult? rightRejection)
            && left is ConstantNode leftConstant)
        {
            constant = leftConstant;
            mirrored = true;
            rejection = null;
            return true;
        }

        rejection = leftRejection ?? rightRejection
            ?? ClassifyOperandRejection(left) ?? ClassifyOperandRejection(right)
            ?? ODataFilterTranslationResult.Rejected(
                nameof(BinaryOperatorNode),
                "A comparison must have an entity-local property on one side and a literal on the other.");
        return false;
    }

    /// <summary>
    /// Resolves an operand to an entity-local property name: a
    /// <see cref="SingleValuePropertyAccessNode"/> whose source (through
    /// <see cref="ConvertNode"/>s) is the entity range variable. Cross-navigation access
    /// (<c>Customer/Name</c>) and dynamic properties produce a specific rejection; any other
    /// shape returns <see langword="false"/> with a <see langword="null"/> rejection so the
    /// caller can supply context.
    /// </summary>
    private static bool TryResolveEntityLocalProperty(
        QueryNode node,
        out string field,
        out ODataFilterTranslationResult? rejection)
    {
        field = string.Empty;
        rejection = null;

        switch (Unwrap(node))
        {
            case SingleValueOpenPropertyAccessNode open:
                rejection = ODataFilterTranslationResult.Rejected(
                    nameof(SingleValueOpenPropertyAccessNode),
                    $"Dynamic property '{open.Name}' is not supported — only properties declared in $metadata can be filtered.");
                return false;

            case SingleValuePropertyAccessNode property when Unwrap(property.Source) is ResourceRangeVariableReferenceNode:
                field = property.Property.Name;
                return true;

            case SingleValuePropertyAccessNode property:
                rejection = ODataFilterTranslationResult.Rejected(
                    nameof(SingleValuePropertyAccessNode),
                    $"Cross-navigation property access ('.../{property.Property.Name}') is not supported — filter on the EntitySet's own columns; ask the API owner to expose the related value as a dedicated column if you need it.");
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Maps a non-property, non-literal operand to the most actionable rejection: arithmetic,
    /// unary minus, unsupported function. Returns <see langword="null"/> when no specific
    /// classification applies.
    /// </summary>
    private static ODataFilterTranslationResult? ClassifyOperandRejection(QueryNode node) =>
        Unwrap(node) switch
        {
            BinaryOperatorNode
            {
                OperatorKind: BinaryOperatorKind.Add or BinaryOperatorKind.Subtract
                    or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Modulo,
            } => RejectArithmetic(),
            UnaryOperatorNode { OperatorKind: UnaryOperatorKind.Negate } => ODataFilterTranslationResult.Rejected(
                nameof(UnaryOperatorNode),
                "The unary minus operator is not supported — compare against the literal value directly."),
            SingleValueFunctionCallNode function => ODataFilterTranslationResult.Rejected(
                nameof(SingleValueFunctionCallNode),
                $"The OData function '{function.Name}' is not supported. Supported functions: contains(Property, 'value'), startswith(Property, 'value'), endswith(Property, 'value') over an entity-local string column."),
            CountNode => ODataFilterTranslationResult.Rejected(
                nameof(CountNode),
                "Filtering on a collection $count segment is not supported. Ask the API owner to expose the count as a dedicated column if you need it."),
            _ => null,
        };

    /// <summary>
    /// Stringifies a literal to the exact format the engine's value parser
    /// (<c>FilterExpressionBuilder.ConvertValue</c>, invariant culture) round-trips.
    /// A <c>null</c> literal yields <paramref name="value"/> = <see langword="null"/> with
    /// success — the caller decides whether null is meaningful in its position.
    /// </summary>
    private static bool TryStringifyConstant(
        ConstantNode constant,
        out string? value,
        out ODataFilterTranslationResult? rejection)
    {
        rejection = null;
        switch (constant.Value)
        {
            case null:
                value = null;
                return true;
            case string s:
                value = s;
                return true;
            case bool b:
                value = b ? "true" : "false";
                return true;
            case Guid g:
                value = g.ToString("D");
                return true;
            case DateTimeOffset dto:
                value = dto.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case Date d:
                // Edm.Date — the engine parses DateOnly with yyyy-MM-dd.
                value = string.Create(CultureInfo.InvariantCulture, $"{d.Year:D4}-{d.Month:D2}-{d.Day:D2}");
                return true;
            case DateOnly dateOnly:
                value = dateOnly.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case TimeOnly timeOnly:
                value = timeOnly.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case TimeOfDay timeOfDay:
                // Edm.TimeOfDay ToString emits HH:mm:ss.fffffff — parseable by TimeOnly.Parse.
                value = timeOfDay.ToString();
                return true;
            case ODataEnumValue enumValue:
                value = enumValue.Value;
                return true;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                value = ((IFormattable)constant.Value).ToString(null, CultureInfo.InvariantCulture);
                return true;
            default:
                value = null;
                rejection = ODataFilterTranslationResult.Rejected(
                    nameof(ConstantNode),
                    $"A literal of type '{constant.Value.GetType().Name}' is not supported in $filter on this feed.");
                return false;
        }
    }

    private static ODataFilterTranslationResult RejectArithmetic() =>
        ODataFilterTranslationResult.Rejected(
            nameof(BinaryOperatorNode),
            "Arithmetic operators (add, sub, mul, div, mod) are not supported in $filter — compare the column against a pre-computed literal instead.");

    private static ODataFilterTranslationResult RejectFunctionShape(string functionName) =>
        ODataFilterTranslationResult.Rejected(
            nameof(SingleValueFunctionCallNode),
            $"The '{functionName}' function requires the shape {functionName}(Property, 'literal') with an entity-local string property and a string literal.");

    /// <summary>
    /// Transparently unwraps <see cref="ConvertNode"/>s — the parser inserts them around
    /// promoted literals (<c>Amount gt 100</c> → int-to-decimal) and nullable property access.
    /// </summary>
    private static QueryNode Unwrap(QueryNode node)
    {
        while (node is ConvertNode convert)
        {
            node = convert.Source;
        }

        return node;
    }
}
