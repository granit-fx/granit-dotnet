using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Granit.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Granit.Persistence.ValueConverters;

/// <summary>
/// EF Core <see cref="ValueConverter{TModel,TProvider}"/> for <see cref="SingleValueObject{T}"/>
/// subclasses. Maps the value object to its underlying primitive — same column type, no migration.
/// </summary>
/// <typeparam name="TValueObject">The concrete <see cref="SingleValueObject{T}"/> subclass.</typeparam>
/// <typeparam name="TPrimitive">The underlying primitive type.</typeparam>
[SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated via Activator.CreateInstance in ApplyGranitConventions.")]
internal sealed class SingleValueObjectConverter<TValueObject, TPrimitive>()
    : ValueConverter<TValueObject, TPrimitive>(
        vo => vo.Value,
        primitive => Reconstruct(primitive))
    where TValueObject : SingleValueObject<TPrimitive>
    where TPrimitive : notnull
{
    // Reconstructs a SingleValueObject<T> from its primitive using reflection.
    // Called by EF Core during materialization — one allocation per row, acceptable.
    private static TValueObject Reconstruct(TPrimitive primitive)
    {
        var instance = (TValueObject)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TValueObject));

        PropertyInfo valueProperty = typeof(TValueObject).GetProperty(nameof(SingleValueObject<TPrimitive>.Value))!;
        valueProperty.SetValue(instance, primitive);

        return instance;
    }
}
