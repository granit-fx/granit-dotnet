using System.Reflection;
using Granit.Domain;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Persistence.EntityFrameworkCore.Conventions;

/// <summary>
/// Native form of the legacy enum-as-string pass (#3158):
/// every enum property persists as its PascalCase name in a varchar sized to the longest
/// value (min 20). Explicit <c>HasConversion</c>/<c>[PersistAsInt]</c>/<c>[Flags]</c>
/// opt-outs match the legacy pass; explicit configuration wins via configuration sources.
/// </summary>
internal sealed class GranitEnumStringConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (IConventionProperty property in entityType.GetDeclaredProperties())
            {
                Apply(property);
            }
        }
    }

    private static void Apply(IConventionProperty property)
    {
        Type underlying = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (!underlying.IsEnum)
        {
            return;
        }

        // Same guards as the legacy pass: an explicit converter or provider CLR type wins.
        if (property.GetValueConverter() is not null || property.GetProviderClrType() is not null)
        {
            return;
        }

        if (property.PropertyInfo?.GetCustomAttribute<PersistAsIntAttribute>() is not null
            || underlying.GetCustomAttribute<FlagsAttribute>() is not null)
        {
            return;
        }

        Type converterType = typeof(EnumToStringConverter<>).MakeGenericType(underlying);
        property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);

        if (property.GetMaxLength() is null)
        {
            int longestName = Enum.GetNames(underlying).Max(name => name.Length);
            property.SetMaxLength(Math.Max(20, longestName + 4));
        }
    }
}
