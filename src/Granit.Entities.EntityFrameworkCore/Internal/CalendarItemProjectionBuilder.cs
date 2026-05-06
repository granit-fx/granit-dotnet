using System.Linq.Expressions;
using System.Reflection;
using Granit.Entities.Layouts;

namespace Granit.Entities.EntityFrameworkCore.Internal;

/// <summary>
/// Builds the <c>Expression&lt;Func&lt;TEntity, CalendarItemResponse&gt;&gt;</c> the runner
/// passes to <c>IQueryable.Select</c>. The projection keys on the property names
/// declared by the layout — never on hard-coded names — so the same builder serves
/// any entity that exposes a calendar layout.
/// </summary>
/// <remarks>
/// <para>
/// Field-by-field semantics (locked by tests so EF Core SQL stays predictable):
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>Id</c> — required <c>Guid Id</c> property on the entity
///     (<c>Granit.Entity</c> base type guarantees it).
///   </description></item>
///   <item><description>
///     <c>Start</c> — projected from <c>layout.StartPropertyName</c>
///     (<see cref="DateTimeOffset"/>).
///   </description></item>
///   <item><description>
///     <c>End</c> — projected from <c>layout.EndPropertyName</c>
///     (<see cref="DateTimeOffset"/>?), or <see langword="null"/> when the layout
///     omits an end field (point-in-time events).
///   </description></item>
///   <item><description>
///     <c>Title</c> — first non-null of: <c>layout.TitlePropertyName</c>,
///     <c>descriptor.DisplayProperty</c>, <c>Id.ToString()</c>. Always rendered as
///     a non-null <see cref="string"/> via <see cref="object.ToString"/> on the
///     selected property.
///   </description></item>
///   <item><description>
///     <c>Color</c> — projected from <c>layout.ColorByPropertyName</c> via
///     <see cref="object.ToString"/> (handles enums, ints, strings uniformly), or
///     <see langword="null"/> when no color field is declared.
///   </description></item>
/// </list>
/// </remarks>
internal static class CalendarItemProjectionBuilder
{
    private static readonly MethodInfo ObjectToStringMethod =
        typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes)!;

    private static readonly ConstructorInfo CalendarItemResponseCtor =
        typeof(CalendarItemResponse).GetConstructor(
            [typeof(Guid), typeof(DateTimeOffset), typeof(DateTimeOffset?), typeof(string), typeof(string)])!;

    /// <summary>
    /// Builds the projection expression. Fails fast at host startup if any named
    /// property is missing or has an unsupported type — the same defensive guard
    /// rationale as <c>CalendarRangeFilterBuilder</c>.
    /// </summary>
    public static Expression<Func<TEntity, CalendarItemResponse>> Build<TEntity>(
        CalendarLayoutDescriptor layout,
        string? displayProperty)
    {
        ArgumentNullException.ThrowIfNull(layout);

        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "e");

        MemberExpression idExpression = ResolveIdAccess(entity);
        MemberExpression startExpression = BuildStart(entity, layout.StartPropertyName);
        Expression endExpression = BuildEnd(entity, layout.EndPropertyName);
        Expression titleExpression = BuildTitle(entity, layout.TitlePropertyName, displayProperty);
        Expression colorExpression = BuildColor(entity, layout.ColorByPropertyName);

        NewExpression body = Expression.New(
            CalendarItemResponseCtor,
            idExpression,
            startExpression,
            endExpression,
            titleExpression,
            colorExpression);

        return Expression.Lambda<Func<TEntity, CalendarItemResponse>>(body, entity);
    }

    private static MemberExpression ResolveIdAccess(ParameterExpression entity)
    {
        PropertyInfo? id = entity.Type.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
        if (id is null || id.PropertyType != typeof(Guid))
        {
            throw new InvalidOperationException(
                $"Calendar projection requires '{entity.Type.FullName}' to expose a public Guid Id property "
                + "(inheriting from Granit.Domain.Entity guarantees this).");
        }

        return Expression.Property(entity, id);
    }

    private static MemberExpression BuildStart(ParameterExpression entity, string startPropertyName)
    {
        MemberExpression startMember = ResolvePropertyAccess(entity, startPropertyName, "StartField");
        if (startMember.Type != typeof(DateTimeOffset))
        {
            throw new InvalidOperationException(
                $"Calendar StartField '{startPropertyName}' on '{entity.Type.FullName}' is type '{startMember.Type.FullName}', "
                + "expected DateTimeOffset.");
        }

        return startMember;
    }

    private static Expression BuildEnd(ParameterExpression entity, string? endPropertyName)
    {
        if (endPropertyName is null)
        {
            return Expression.Constant(null, typeof(DateTimeOffset?));
        }

        MemberExpression endMember = ResolvePropertyAccess(entity, endPropertyName, "EndField");
        if (endMember.Type == typeof(DateTimeOffset?))
        {
            return endMember;
        }

        if (endMember.Type == typeof(DateTimeOffset))
        {
            return Expression.Convert(endMember, typeof(DateTimeOffset?));
        }

        throw new InvalidOperationException(
            $"Calendar EndField '{endPropertyName}' on '{entity.Type.FullName}' is type '{endMember.Type.FullName}', "
            + "expected DateTimeOffset or DateTimeOffset?.");
    }

    private static Expression BuildTitle(
        ParameterExpression entity,
        string? titlePropertyName,
        string? displayProperty)
    {
        // Resolve order: TitleField → entity.DisplayProperty → Id.ToString()
        string? selectedProperty = titlePropertyName ?? displayProperty;

        if (selectedProperty is null)
        {
            // Final fallback: project Id.ToString() so the wire shape always
            // carries a non-null string (renderers crash on null titles).
            PropertyInfo idProp = entity.Type.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)!;
            return Expression.Call(Expression.Property(entity, idProp), ObjectToStringMethod);
        }

        MemberExpression titleMember = ResolvePropertyAccess(entity, selectedProperty, "TitleField");

        if (titleMember.Type == typeof(string))
        {
            // Coalesce nullable-but-not-flagged-nullable references to "" so the
            // wire shape never carries a null title.
            return Expression.Coalesce(titleMember, Expression.Constant(string.Empty));
        }

        // Non-string property (rare — TitleField selector is typed as Func<T,string>
        // in the DSL, but DisplayProperty fallback can name any property). Use
        // ToString() so EF translates it to a CAST when possible.
        return Expression.Call(titleMember, ObjectToStringMethod);
    }

    private static Expression BuildColor(ParameterExpression entity, string? colorByPropertyName)
    {
        if (colorByPropertyName is null)
        {
            return Expression.Constant(null, typeof(string));
        }

        MemberExpression colorMember = ResolvePropertyAccess(entity, colorByPropertyName, "ColorBy");

        if (colorMember.Type == typeof(string))
        {
            return colorMember;
        }

        // Enums / ints / Guids / etc. — ToString() so EF Core projects a CAST AS
        // VARCHAR (Postgres) or equivalent. Reference types call .ToString() at
        // materialisation time.
        Expression toStringCall = Expression.Call(colorMember, ObjectToStringMethod);

        // Wrap nullable value types so EF can translate the null-check correctly.
        if (Nullable.GetUnderlyingType(colorMember.Type) is not null)
        {
            // colorMember.HasValue ? colorMember.Value.ToString() : null
            return Expression.Condition(
                Expression.Property(colorMember, "HasValue"),
                Expression.Call(Expression.Property(colorMember, "Value"), ObjectToStringMethod),
                Expression.Constant(null, typeof(string)));
        }

        return toStringCall;
    }

    private static MemberExpression ResolvePropertyAccess(
        ParameterExpression entity,
        string propertyName,
        string dslSlot)
    {
        PropertyInfo? property = entity.Type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Calendar {dslSlot}='{propertyName}' is not a public instance property on '{entity.Type.FullName}'. "
                + "The descriptor was likely built for a different type, or the property was renamed since the EntityDefinition was declared.");
        }

        return Expression.Property(entity, property);
    }
}
