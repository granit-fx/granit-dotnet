// =============================================================================
// DescriptorTests - Immutable filtering descriptor metadata
// =============================================================================
// Verifies:
//   - FilterGroupDescriptor construction and required properties
//   - PresetDescriptor construction, defaults, and expression predicate
//   - QuickFilterDescriptor construction, defaults, and expression predicate
//   - AggregateDescriptor construction and required properties
//   - GroupByDescriptor construction and required properties
//   - DateFilterDescriptor construction and default period
// =============================================================================

using System.Linq.Expressions;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.Filtering;

public sealed class FilterGroupDescriptorTests
{
    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        Expression<Func<string, bool>> predicate = s => s.Length > 0;

        FilterGroupDescriptor descriptor = new()
        {
            Name = "Status",
            Presets =
            [
                new PresetDescriptor
                {
                    Name = "Active",
                    Predicate = predicate,
                },
            ],
        };

        descriptor.Name.ShouldBe("Status");
        descriptor.Presets.Count.ShouldBe(1);
    }

    [Fact]
    public void Label_DefaultsToNull()
    {
        FilterGroupDescriptor descriptor = new()
        {
            Name = "Category",
            Presets = [],
        };

        descriptor.Label.ShouldBeNull();
    }

    [Fact]
    public void Label_CanBeSet()
    {
        FilterGroupDescriptor descriptor = new()
        {
            Name = "Status",
            Label = "Statut",
            Presets = [],
        };

        descriptor.Label.ShouldBe("Statut");
    }

    [Fact]
    public void MultiplePresets_ArePreserved()
    {
        Expression<Func<string, bool>> activePredicate = s => s == "Active";
        Expression<Func<string, bool>> archivedPredicate = s => s == "Archived";

        FilterGroupDescriptor descriptor = new()
        {
            Name = "Status",
            Presets =
            [
                new PresetDescriptor { Name = "Active", Predicate = activePredicate, IsDefault = true },
                new PresetDescriptor { Name = "Archived", Predicate = archivedPredicate },
            ],
        };

        descriptor.Presets.Count.ShouldBe(2);
        descriptor.Presets[0].Name.ShouldBe("Active");
        descriptor.Presets[1].Name.ShouldBe("Archived");
    }
}

public sealed class PresetDescriptorTests
{
    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        PresetDescriptor descriptor = new()
        {
            Name = "Positive",
            Predicate = predicate,
        };

        descriptor.Name.ShouldBe("Positive");
        descriptor.Predicate.ShouldNotBeNull();
    }

    [Fact]
    public void Label_DefaultsToNull()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        PresetDescriptor descriptor = new()
        {
            Name = "Active",
            Predicate = predicate,
        };

        descriptor.Label.ShouldBeNull();
    }

    [Fact]
    public void IsDefault_DefaultsToFalse()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        PresetDescriptor descriptor = new()
        {
            Name = "Active",
            Predicate = predicate,
        };

        descriptor.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        Expression<Func<string, bool>> predicate = s => s.Length > 0;

        PresetDescriptor descriptor = new()
        {
            Name = "Active",
            Label = "Actif",
            IsDefault = true,
            Predicate = predicate,
        };

        descriptor.Label.ShouldBe("Actif");
        descriptor.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Predicate_IsLambdaExpression()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        PresetDescriptor descriptor = new()
        {
            Name = "Test",
            Predicate = predicate,
        };

        descriptor.Predicate.ShouldBeAssignableTo<LambdaExpression>();
    }
}

public sealed class QuickFilterDescriptorTests
{
    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        Expression<Func<string, bool>> predicate = s => s == "mine";

        QuickFilterDescriptor descriptor = new()
        {
            Name = "MyItems",
            Predicate = predicate,
        };

        descriptor.Name.ShouldBe("MyItems");
        descriptor.Predicate.ShouldNotBeNull();
    }

    [Fact]
    public void Label_DefaultsToNull()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        QuickFilterDescriptor descriptor = new()
        {
            Name = "Active",
            Predicate = predicate,
        };

        descriptor.Label.ShouldBeNull();
    }

    [Fact]
    public void IsDefault_DefaultsToFalse()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        QuickFilterDescriptor descriptor = new()
        {
            Name = "Active",
            Predicate = predicate,
        };

        descriptor.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        Expression<Func<string, bool>> predicate = s => s.Length > 0;

        QuickFilterDescriptor descriptor = new()
        {
            Name = "MyAppointments",
            Label = "Mes rendez-vous",
            IsDefault = true,
            Predicate = predicate,
        };

        descriptor.Label.ShouldBe("Mes rendez-vous");
        descriptor.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Predicate_IsLambdaExpression()
    {
        Expression<Func<int, bool>> predicate = x => x > 0;

        QuickFilterDescriptor descriptor = new()
        {
            Name = "Test",
            Predicate = predicate,
        };

        descriptor.Predicate.ShouldBeAssignableTo<LambdaExpression>();
    }
}

public sealed class AggregateDescriptorTests
{
    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        AggregateDescriptor descriptor = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            Function = AggregateFunction.Sum,
            Alias = "totalAmount",
        };

        descriptor.PropertyName.ShouldBe("Amount");
        descriptor.ClrType.ShouldBe(typeof(decimal));
        descriptor.Function.ShouldBe(AggregateFunction.Sum);
        descriptor.Alias.ShouldBe("totalAmount");
    }

    [Fact]
    public void DifferentAggregateFunctions_AreSupported()
    {
        AggregateDescriptor sum = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            Function = AggregateFunction.Sum,
            Alias = "sum",
        };
        AggregateDescriptor avg = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            Function = AggregateFunction.Avg,
            Alias = "avg",
        };
        AggregateDescriptor max = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            Function = AggregateFunction.Max,
            Alias = "max",
        };
        AggregateDescriptor min = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            Function = AggregateFunction.Min,
            Alias = "min",
        };
        AggregateDescriptor count = new()
        {
            PropertyName = "Amount",
            ClrType = typeof(decimal),
            Function = AggregateFunction.Count,
            Alias = "count",
        };

        sum.Function.ShouldBe(AggregateFunction.Sum);
        avg.Function.ShouldBe(AggregateFunction.Avg);
        max.Function.ShouldBe(AggregateFunction.Max);
        min.Function.ShouldBe(AggregateFunction.Min);
        count.Function.ShouldBe(AggregateFunction.Count);
    }

    [Fact]
    public void ClrType_SupportsIntegerTypes()
    {
        AggregateDescriptor descriptor = new()
        {
            PropertyName = "Count",
            ClrType = typeof(int),
            Function = AggregateFunction.Count,
            Alias = "total",
        };

        descriptor.ClrType.ShouldBe(typeof(int));
    }
}

public sealed class GroupByDescriptorTests
{
    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        GroupByDescriptor descriptor = new()
        {
            PropertyName = "Status",
            ClrType = typeof(string),
        };

        descriptor.PropertyName.ShouldBe("Status");
        descriptor.ClrType.ShouldBe(typeof(string));
    }

    [Fact]
    public void ClrType_SupportsEnumTypes()
    {
        GroupByDescriptor descriptor = new()
        {
            PropertyName = "Priority",
            ClrType = typeof(DayOfWeek),
        };

        descriptor.ClrType.ShouldBe(typeof(DayOfWeek));
    }

    [Fact]
    public void ClrType_SupportsGuid()
    {
        GroupByDescriptor descriptor = new()
        {
            PropertyName = "CategoryId",
            ClrType = typeof(Guid),
        };

        descriptor.ClrType.ShouldBe(typeof(Guid));
    }
}

public sealed class DateFilterDescriptorTests
{
    [Fact]
    public void RequiredProperties_ArePreserved()
    {
        DateFilterDescriptor descriptor = new()
        {
            PropertyName = "CreatedAt",
            ClrType = typeof(DateTimeOffset),
        };

        descriptor.PropertyName.ShouldBe("CreatedAt");
        descriptor.ClrType.ShouldBe(typeof(DateTimeOffset));
    }

    [Fact]
    public void DefaultPeriod_DefaultsToFirstEnumValue()
    {
        DateFilterDescriptor descriptor = new()
        {
            PropertyName = "CreatedAt",
            ClrType = typeof(DateTimeOffset),
        };

        descriptor.DefaultPeriod.ShouldBe(default(DatePeriod));
    }

    [Fact]
    public void DefaultPeriod_CanBeSet()
    {
        DateFilterDescriptor descriptor = new()
        {
            PropertyName = "CreatedAt",
            ClrType = typeof(DateTimeOffset),
            DefaultPeriod = DatePeriod.ThisYear,
        };

        descriptor.DefaultPeriod.ShouldBe(DatePeriod.ThisYear);
    }

    [Fact]
    public void ClrType_SupportsDateOnly()
    {
        DateFilterDescriptor descriptor = new()
        {
            PropertyName = "BirthDate",
            ClrType = typeof(DateOnly),
        };

        descriptor.ClrType.ShouldBe(typeof(DateOnly));
    }

    [Fact]
    public void ClrType_SupportsNullableDateTimeOffset()
    {
        DateFilterDescriptor descriptor = new()
        {
            PropertyName = "DeletedAt",
            ClrType = typeof(DateTimeOffset?),
        };

        descriptor.ClrType.ShouldBe(typeof(DateTimeOffset?));
    }
}
