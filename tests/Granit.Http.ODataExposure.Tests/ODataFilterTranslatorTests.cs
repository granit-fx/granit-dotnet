using Granit.Http.ODataExposure.Internal;
using Granit.QueryEngine.Filtering;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.UriParser;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// Pins the #3004 <c>$filter</c> → <see cref="QueryPredicate"/> translation matrix: every
/// supported OData node shape maps to the engine's leaf/composite predicates with the exact
/// literal stringification the engine's value parser round-trips, and every unsupported shape
/// is rejected with a distinct, actionable detail (never silently dropped — a vanished leaf
/// inside OR/NOT would change result semantics).
/// </summary>
public sealed class ODataFilterTranslatorTests
{
    private static readonly IEdmModel Model = BuildModel();

    private static IEdmModel BuildModel()
    {
        ODataConventionModelBuilder builder = new();
        builder.EntitySet<TranslatorInvoice>("Invoices");
        return builder.GetEdmModel();
    }

    private static ODataFilterTranslationResult Translate(string filter)
    {
        IEdmEntityType invoiceType = Model.SchemaElements
            .OfType<IEdmEntityType>()
            .Single(t => t.Name == nameof(TranslatorInvoice));
        IEdmEntitySet entitySet = Model.EntityContainer.FindEntitySet("Invoices").ShouldNotBeNull();

        ODataQueryOptionParser parser = new(
            Model,
            invoiceType,
            entitySet,
            new Dictionary<string, string> { ["$filter"] = filter });

        return ODataFilterTranslator.Translate(parser.ParseFilter());
    }

    private static FilterCriteria ShouldBeLeaf(ODataFilterTranslationResult result)
    {
        result.RejectionDetail.ShouldBeNull(result.RejectionDetail);
        result.RejectedNodeKind.ShouldBeNull();
        return result.Predicate.ShouldBeOfType<FilterPredicate>().Criteria;
    }

    private static void ShouldBeRejected(ODataFilterTranslationResult result, string expectedDetailFragment)
    {
        result.Predicate.ShouldBeNull();
        result.RejectedNodeKind.ShouldNotBeNullOrWhiteSpace();
        result.RejectionDetail.ShouldNotBeNull();
        result.RejectionDetail.ShouldContain(expectedDetailFragment, customMessage: result.RejectionDetail);
    }

    // ── Supported leaves ──────────────────────────────────────────────────

    [Theory]
    // eq / ne on string
    [InlineData("Number eq 'A-001'", "Number", FilterOperator.Eq, "A-001")]
    [InlineData("Number ne 'A-001'", "Number", FilterOperator.Ne, "A-001")]
    // relational operators on numerics (ConvertNode around the promoted literal is unwrapped)
    [InlineData("Amount gt 100", "Amount", FilterOperator.Gt, "100")]
    [InlineData("Amount ge 100.5", "Amount", FilterOperator.Gte, "100.5")]
    [InlineData("Amount lt 100", "Amount", FilterOperator.Lt, "100")]
    [InlineData("Amount le 100", "Amount", FilterOperator.Lte, "100")]
    [InlineData("Priority eq 3", "Priority", FilterOperator.Eq, "3")]
    // literal-on-the-left comparisons are mirrored
    [InlineData("100 lt Amount", "Amount", FilterOperator.Gt, "100")]
    [InlineData("100 ge Amount", "Amount", FilterOperator.Lte, "100")]
    // booleans
    [InlineData("Active eq true", "Active", FilterOperator.Eq, "true")]
    [InlineData("Active ne false", "Active", FilterOperator.Ne, "false")]
    // Guid — invariant "D" format
    [InlineData("Id eq 3F2504E0-4F89-11D3-9A0C-0305E82C3301", "Id", FilterOperator.Eq, "3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    // DateTimeOffset — round-trip "O" format, exactly what the engine's DateTimeOffset.Parse reads
    [InlineData("PaidAt ge 2026-01-01T00:00:00Z", "PaidAt", FilterOperator.Gte, "2026-01-01T00:00:00.0000000+00:00")]
    // Edm.Date — yyyy-MM-dd
    [InlineData("IssuedOn eq 2026-05-01", "IssuedOn", FilterOperator.Eq, "2026-05-01")]
    // enum — member name (ODataEnumValue)
    [InlineData("Status eq 'Paid'", "Status", FilterOperator.Eq, "Paid")]
    // string functions
    [InlineData("contains(Number, 'A-')", "Number", FilterOperator.Contains, "A-")]
    [InlineData("startswith(Number, 'A')", "Number", FilterOperator.StartsWith, "A")]
    [InlineData("endswith(Number, '1')", "Number", FilterOperator.EndsWith, "1")]
    // in — comma-joined items
    [InlineData("Status in ('Paid','Sent')", "Status", FilterOperator.In, "Paid,Sent")]
    [InlineData("Priority in (1,2,3)", "Priority", FilterOperator.In, "1,2,3")]
    [InlineData("Number in ('a','b')", "Number", FilterOperator.In, "a,b")]
    public void Translate_SupportedLeaf_ProducesExpectedCriterion(
        string filter, string expectedField, FilterOperator expectedOperator, string expectedValue)
    {
        FilterCriteria criteria = ShouldBeLeaf(Translate(filter));

        criteria.Field.ShouldBe(expectedField);
        criteria.Operator.ShouldBe(expectedOperator);
        criteria.Value.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData("PaidAt eq null", FilterOperator.IsNull)]
    [InlineData("PaidAt ne null", FilterOperator.IsNotNull)]
    [InlineData("null eq PaidAt", FilterOperator.IsNull)]
    [InlineData("null ne PaidAt", FilterOperator.IsNotNull)]
    public void Translate_NullComparison_ProducesNullCheck(string filter, FilterOperator expectedOperator)
    {
        FilterCriteria criteria = ShouldBeLeaf(Translate(filter));

        criteria.Field.ShouldBe("PaidAt");
        criteria.Operator.ShouldBe(expectedOperator);
        criteria.Value.ShouldBe(string.Empty);
    }

    // ── Supported composites ──────────────────────────────────────────────

    [Fact]
    public void Translate_And_ProducesAndPredicate()
    {
        ODataFilterTranslationResult result = Translate("Status eq 'Paid' and Amount gt 100");

        result.RejectionDetail.ShouldBeNull(result.RejectionDetail);
        AndPredicate and = result.Predicate.ShouldBeOfType<AndPredicate>();
        and.Operands.Count.ShouldBe(2);
        ((FilterPredicate)and.Operands[0]).Criteria.Field.ShouldBe("Status");
        ((FilterPredicate)and.Operands[1]).Criteria.Field.ShouldBe("Amount");
    }

    [Fact]
    public void Translate_OrChain_ProducesNestedOrPredicates()
    {
        // OData's parser is left-associative: (a or b) or c.
        ODataFilterTranslationResult result = Translate("Status eq 'Paid' or Status eq 'Sent' or Amount gt 400");

        result.RejectionDetail.ShouldBeNull(result.RejectionDetail);
        OrPredicate outer = result.Predicate.ShouldBeOfType<OrPredicate>();
        outer.Operands.Count.ShouldBe(2);
        OrPredicate inner = outer.Operands[0].ShouldBeOfType<OrPredicate>();
        ((FilterPredicate)inner.Operands[0]).Criteria.Value.ShouldBe("Paid");
        ((FilterPredicate)inner.Operands[1]).Criteria.Value.ShouldBe("Sent");
        ((FilterPredicate)outer.Operands[1]).Criteria.Field.ShouldBe("Amount");
    }

    [Fact]
    public void Translate_NotContains_ProducesNotPredicate()
    {
        ODataFilterTranslationResult result = Translate("not contains(Number, 'A')");

        result.RejectionDetail.ShouldBeNull(result.RejectionDetail);
        NotPredicate not = result.Predicate.ShouldBeOfType<NotPredicate>();
        FilterCriteria criteria = not.Operand.ShouldBeOfType<FilterPredicate>().Criteria;
        criteria.Operator.ShouldBe(FilterOperator.Contains);
        criteria.Value.ShouldBe("A");
    }

    // ── Explicit rejections ───────────────────────────────────────────────

    [Theory]
    // lambdas
    [InlineData("Lines/any(l: l/LineAmount gt 5)", "any(")]
    [InlineData("Lines/all(l: l/LineAmount gt 5)", "all(")]
    // collection count segment
    [InlineData("Lines/$count gt 2", "$count")]
    // arithmetic operators
    [InlineData("Amount add 5 gt 10", "Arithmetic")]
    [InlineData("Amount sub 5 gt 10", "Arithmetic")]
    [InlineData("Amount mul 2 gt 10", "Arithmetic")]
    [InlineData("Amount div 2 gt 10", "Arithmetic")]
    [InlineData("Amount mod 2 eq 0", "Arithmetic")]
    // unary minus
    [InlineData("-Amount lt 10", "unary minus")]
    // flag-enum has
    [InlineData("Status has Granit.Http.ODataExposure.Tests.TranslatorInvoiceStatus'Paid'", "'has'")]
    // cross-navigation property access
    [InlineData("Customer/Name eq 'ACME'", "Cross-navigation")]
    // every other canonical function, each named in the detail
    [InlineData("tolower(Number) eq 'a'", "'tolower'")]
    [InlineData("toupper(Number) eq 'A'", "'toupper'")]
    [InlineData("trim(Number) eq 'a'", "'trim'")]
    [InlineData("concat(Number, 'x') eq 'ax'", "'concat'")]
    [InlineData("indexof(Number, 'x') eq 1", "'indexof'")]
    [InlineData("length(Number) gt 3", "'length'")]
    [InlineData("substring(Number, 1) eq 'x'", "'substring'")]
    [InlineData("year(PaidAt) eq 2026", "'year'")]
    [InlineData("month(PaidAt) eq 7", "'month'")]
    [InlineData("day(PaidAt) eq 14", "'day'")]
    [InlineData("date(PaidAt) eq 2026-07-14", "'date'")]
    [InlineData("time(PaidAt) eq 12:00:00", "'time'")]
    [InlineData("PaidAt lt now()", "'now'")]
    [InlineData("round(Amount) eq 100", "'round'")]
    [InlineData("floor(Amount) eq 100", "'floor'")]
    [InlineData("ceiling(Amount) eq 100", "'ceiling'")]
    // in — items the engine's comma-splitting In parser would not round-trip
    [InlineData("Number in ('a,b')", "comma")]
    [InlineData("PaidAt in (2026-01-01T00:00:00Z, null)", "null literal cannot appear")]
    [InlineData("Number in (' padded ')", "whitespace")]
    // bare boolean property
    [InlineData("Active", "eq true")]
    [InlineData("not Active", "eq true")]
    // no property operand at all
    [InlineData("2 eq 1", "entity-local property")]
    public void Translate_UnsupportedConstruct_IsRejectedWithActionableDetail(
        string filter, string expectedDetailFragment) =>
        ShouldBeRejected(Translate(filter), expectedDetailFragment);

    [Fact]
    public void Translate_DynamicProperty_IsRejected()
    {
        // TranslatorInvoice is an open type (DynamicProperties dictionary), so an undeclared
        // name parses to a SingleValueOpenPropertyAccessNode instead of failing EDM lookup.
        ODataFilterTranslationResult result = Translate("UndeclaredProperty eq 'x'");

        ShouldBeRejected(result, "Dynamic property");
        result.RejectionDetail.ShouldNotBeNull();
        result.RejectionDetail.ShouldContain("UndeclaredProperty");
    }

    [Fact]
    public void Translate_RejectionShortCircuits_InsideComposite()
    {
        // A rejection anywhere in the tree fails the whole translation — a silently dropped
        // OR branch would widen the result set.
        ODataFilterTranslationResult result = Translate("Status eq 'Paid' or tolower(Number) eq 'a'");

        ShouldBeRejected(result, "'tolower'");
    }
}

/// <summary>Filter-translation test entity — one property per supported literal kind, one navigation, one collection, open-type dictionary for dynamic-property rejection.</summary>
public sealed class TranslatorInvoice
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Priority { get; set; }
    public bool Active { get; set; }
    public TranslatorInvoiceStatus Status { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public TranslatorCustomer? Customer { get; set; }
    public List<TranslatorLine> Lines { get; set; } = [];
    public IDictionary<string, object?> DynamicProperties { get; set; } = new Dictionary<string, object?>();
}

public enum TranslatorInvoiceStatus
{
    Draft,
    Sent,
    Paid,
}

public sealed class TranslatorCustomer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class TranslatorLine
{
    public Guid Id { get; set; }
    public decimal LineAmount { get; set; }
}
