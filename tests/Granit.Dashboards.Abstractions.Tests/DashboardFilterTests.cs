using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the public shape of <see cref="DashboardFilter"/> + clause + the two enums
/// (P2.5). Pin the wire format — operator names match the OData / Granit.QueryEngine
/// vocabulary so downstream JSON consumers can reuse the same dictionary.
/// </summary>
public sealed class DashboardFilterTests
{
    [Fact]
    public void Construction_DefaultsAndOperationAndNotEditable()
    {
        DashboardFilter filter = new(
            Name: "CurrentCustomer",
            LabelLocalizationKey: "Dashboard:Sample.Filters.Customer",
            Clauses:
            [
                new DashboardFilterClause("customer.id", DashboardFilterOperator.Eq, "${entityId}"),
            ]);

        filter.Operation.ShouldBe(DashboardFilterOperation.And);
        filter.Editable.ShouldBeFalse();
        filter.Clauses.Count.ShouldBe(1);
    }

    [Fact]
    public void Editable_OptIn_ForToolbarSurfacing()
    {
        DashboardFilter filter = new(
            Name: "DateRange",
            LabelLocalizationKey: "Dashboard:Sample.Filters.DateRange",
            Clauses:
            [
                new DashboardFilterClause("issuedAt", DashboardFilterOperator.Gte, "2026-01-01"),
                new DashboardFilterClause("issuedAt", DashboardFilterOperator.Lt, "2027-01-01"),
            ],
            Editable: true);

        filter.Editable.ShouldBeTrue();
        filter.Clauses.Count.ShouldBe(2);
    }

    [Fact]
    public void Operation_OrCombinesClausesPermissively()
    {
        DashboardFilter filter = new(
            Name: "OpenOrEscalated",
            LabelLocalizationKey: "Dashboard:Sample.Filters.Status",
            Clauses:
            [
                new DashboardFilterClause("status", DashboardFilterOperator.Eq, "open"),
                new DashboardFilterClause("status", DashboardFilterOperator.Eq, "escalated"),
            ],
            Operation: DashboardFilterOperation.Or);

        filter.Operation.ShouldBe(DashboardFilterOperation.Or);
    }

    [Fact]
    public void Clause_AcceptsNullValue_ForAbsenceOperators()
    {
        DashboardFilterClause clause = new("archivedAt", DashboardFilterOperator.Eq, null);

        clause.Value.ShouldBeNull();
    }

    [Fact]
    public void Operator_EnumOrderingIsStable()
    {
        // Wire format stability — operator integers travel in JSON when no converter
        // is set. Names match OData / Granit.QueryEngine vocabulary; the integer
        // ordering pins the contract for both producers (server) and consumers (front).
        ((int)DashboardFilterOperator.Eq).ShouldBe(0);
        ((int)DashboardFilterOperator.Ne).ShouldBe(1);
        ((int)DashboardFilterOperator.Gt).ShouldBe(2);
        ((int)DashboardFilterOperator.Gte).ShouldBe(3);
        ((int)DashboardFilterOperator.Lt).ShouldBe(4);
        ((int)DashboardFilterOperator.Lte).ShouldBe(5);
        ((int)DashboardFilterOperator.In).ShouldBe(6);
        ((int)DashboardFilterOperator.Contains).ShouldBe(7);
        ((int)DashboardFilterOperator.StartsWith).ShouldBe(8);
    }

    [Fact]
    public void Operation_EnumOrderingIsStable()
    {
        ((int)DashboardFilterOperation.And).ShouldBe(0);
        ((int)DashboardFilterOperation.Or).ShouldBe(1);
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        DashboardFilterClause c1 = new("status", DashboardFilterOperator.Eq, "open");
        DashboardFilterClause c2 = new("status", DashboardFilterOperator.Eq, "open");

        c1.ShouldBe(c2);
    }
}
