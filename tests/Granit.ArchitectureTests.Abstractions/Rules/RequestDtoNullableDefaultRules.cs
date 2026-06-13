using System.Reflection;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rule: a nullable constructor parameter on a <c>*Request</c> DTO in an
/// <c>.Endpoints</c> assembly must declare a default value (<c>= null</c>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Text.Json"/>'s <c>RespectRequiredConstructorParameters</c> (default
/// <see langword="true"/> since .NET 9) treats a JSON property as <c>required</c> iff its
/// constructor parameter has <b>no default value</b> — independent of nullability. So a
/// positional record parameter typed <c>string?</c> with no <c>= null</c> is emitted as
/// <c>required</c> in the OpenAPI contract while typed <c>["null","string"]</c>, which
/// <c>openapi-typescript</c> codegen renders as a required <c>string | null</c> instead of an
/// optional <c>foo?</c>. Adding <c>= null</c> drops the parameter from <c>required</c>.
/// </para>
/// <para>
/// The rule is <b>direction-aware</b>: it targets <c>*Request</c> (input) DTOs only. It must
/// never touch <c>*Response</c> (output) DTOs, where <c>required</c> + nullable correctly means
/// "the server always returns the key, and its value may be null".
/// </para>
/// <para>
/// The rare legitimately-required-but-nullable input parameter (e.g. an explicit tenancy choice
/// that must be present in the payload) is exempted via the caller-supplied set, keyed
/// <c>{RequestTypeName}.{ParameterName}</c>.
/// </para>
/// </remarks>
public static class RequestDtoNullableDefaultRules
{
    /// <summary>
    /// Every nullable constructor parameter on a <c>*Request</c> DTO must declare a default value,
    /// unless the <paramref name="exemptions"/> set lists it as a deliberate required-but-nullable input.
    /// </summary>
    /// <param name="endpointAssemblies">The <c>*.Endpoints</c> assemblies to scan.</param>
    /// <param name="exemptions">
    /// Keys (<c>{RequestTypeName}.{ParameterName}</c>) of parameters that are intentionally
    /// required despite being nullable. Each entry must carry an inline justification at its source.
    /// </param>
    public static void NullableRequestParametersMustHaveDefault(
        IEnumerable<Assembly> endpointAssemblies,
        ISet<string> exemptions)
    {
        ArgumentNullException.ThrowIfNull(endpointAssemblies);
        ArgumentNullException.ThrowIfNull(exemptions);

        NullabilityInfoContext nullability = new();
        List<string> violations = [];

        foreach (Assembly assembly in endpointAssemblies)
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (!IsRequestDto(type))
                {
                    continue;
                }

                ConstructorInfo? ctor = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault(c => c.GetParameters().Length > 0);

                if (ctor is null)
                {
                    continue;
                }

                foreach (ParameterInfo parameter in ctor.GetParameters())
                {
                    if (parameter.HasDefaultValue || !IsNullable(nullability, parameter))
                    {
                        continue;
                    }

                    string key = $"{type.Name}.{parameter.Name}";
                    if (exemptions.Contains(key))
                    {
                        continue;
                    }

                    violations.Add(
                        $"  {type.FullName}.{parameter.Name} ({DisplayType(parameter.ParameterType)}) " +
                        "— nullable input without `= null`; emitted as required in OpenAPI.");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Nullable `*Request` DTO parameters without a `= null` default are emitted as `required` "
            + "in the OpenAPI contract (System.Text.Json RespectRequiredConstructorParameters). "
            + "Add `= null` to make the input optional, or add the parameter to the exemption set "
            + "(with an inline justification) if it is deliberately required-but-nullable."
            + Environment.NewLine + string.Join(Environment.NewLine, violations.OrderBy(v => v, StringComparer.Ordinal)));
    }

    private static bool IsRequestDto(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && type.Name.EndsWith("Request", StringComparison.Ordinal)
        && type.Namespace is { } ns
        && ns.Contains(".Endpoints", StringComparison.Ordinal);

    private static bool IsNullable(NullabilityInfoContext context, ParameterInfo parameter) =>
        Nullable.GetUnderlyingType(parameter.ParameterType) is not null
        || context.Create(parameter).WriteState == NullabilityState.Nullable;

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string DisplayType(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        return underlying is not null ? $"{underlying.Name}?" : type.Name;
    }
}
