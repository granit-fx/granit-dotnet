using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Granit.Analyzers;

/// <summary>
/// GRAPI003 — Reports a warning when a minimal API endpoint handler has an interface-typed
/// parameter without an explicit binding attribute (<c>[FromServices]</c>,
/// <c>[FromQuery]</c>, <c>[FromBody]</c>, etc.).
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core's service inference checks <see cref="System.IServiceProvider"/> at routing
/// initialization time. When a type is not registered or inference fails, ASP.NET Core falls
/// back to <c>[FromBody]</c> inference — which throws an
/// <see cref="System.InvalidOperationException"/> at startup for GET and DELETE endpoints
/// (which disallow body parameters).
/// </para>
/// <para>
/// A "minimal API endpoint handler" is identified as a <c>static</c> method whose return type
/// is (or unwraps from <c>Task&lt;T&gt;</c> / <c>ValueTask&lt;T&gt;</c> to) a type that
/// implements <c>Microsoft.AspNetCore.Http.IResult</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MinimalApiServiceParameterAnalyzer : SingleRuleAnalyzerBase
{
    /// <summary>Diagnostic identifier.</summary>
    public const string DiagnosticId = "GRAPI003";

    // ASP.NET Core interface types that bind from the HTTP request, not from DI.
    private static readonly HashSet<string> ExemptInterfaces = new HashSet<string>(
        System.StringComparer.Ordinal)
    {
        "Microsoft.AspNetCore.Http.IFormFile",
        "Microsoft.AspNetCore.Http.IFormFileCollection",
        "Microsoft.AspNetCore.Http.IResult",
    };

    // Attribute short names (with and without "Attribute" suffix) that explicitly
    // declare a binding source — any of these exempts the parameter.
    private static readonly HashSet<string> BindingAttributeNames = new HashSet<string>(
        System.StringComparer.Ordinal)
    {
        "FromServices", "FromServicesAttribute",
        "FromQuery",    "FromQueryAttribute",
        "FromRoute",    "FromRouteAttribute",
        "FromBody",     "FromBodyAttribute",
        "FromHeader",   "FromHeaderAttribute",
        "FromForm",     "FromFormAttribute",
        "AsParameters", "AsParametersAttribute",
    };

    private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
        DiagnosticId,
        title: "Minimal API service parameter missing [FromServices]",
        messageFormat: "Parameter '{0}' of interface type '{1}' must be decorated with [FromServices]. "
            + "Without it, ASP.NET Core may infer the parameter as [FromBody] and throw at startup.",
        category: "Api",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "In minimal API endpoint handlers (static methods returning an IResult-derived type), "
            + "parameters of interface type must have [FromServices] so that ASP.NET Core resolves them "
            + "from the DI container. Service inference is unreliable and may fall back to [FromBody], "
            + "which throws an InvalidOperationException at startup for GET/DELETE endpoints.");

    /// <inheritdoc/>
    protected override DiagnosticDescriptor Rule => _rule;

    /// <inheritdoc/>
    protected override void RegisterActions(AnalysisContext context)
        => context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Endpoint handler methods must be static.
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        // Return type must resolve to an IResult-derived type.
        if (!IsEndpointHandlerReturn(method.ReturnType, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
        {
            if (parameter.Type is null)
            {
                continue;
            }

            // Parameter already has an explicit binding attribute — skip.
            if (HasBindingAttribute(parameter))
            {
                continue;
            }

            TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(
                parameter.Type, context.CancellationToken);

            if (typeInfo.Type is not INamedTypeSymbol typeSymbol)
            {
                continue;
            }

            if (typeSymbol.TypeKind != TypeKind.Interface)
            {
                continue;
            }

            // Exempt known framework interfaces that bind from the request.
            string originalFqn = typeSymbol.OriginalDefinition.ToDisplayString();
            if (ExemptInterfaces.Contains(originalFqn))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                _rule,
                parameter.Type.GetLocation(),
                parameter.Identifier.Text,
                typeSymbol.Name));
        }
    }

    private static bool IsEndpointHandlerReturn(
        TypeSyntax returnTypeSyntax,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        TypeInfo typeInfo = model.GetTypeInfo(returnTypeSyntax, cancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol returnType)
        {
            return false;
        }

        ITypeSymbol unwrapped = UnwrapTaskType(returnType);
        return ImplementsIResult(unwrapped);
    }

    private static ITypeSymbol UnwrapTaskType(INamedTypeSymbol type)
    {
        if (type.TypeArguments.Length != 1)
        {
            return type;
        }

        string constructedFrom = type.ConstructedFrom?.ToDisplayString()
            ?? type.ToDisplayString();

        if (constructedFrom.StartsWith(
                "System.Threading.Tasks.Task<", System.StringComparison.Ordinal)
            || constructedFrom.StartsWith(
                "System.Threading.Tasks.ValueTask<", System.StringComparison.Ordinal))
        {
            return type.TypeArguments[0];
        }

        return type;
    }

    private static bool ImplementsIResult(ITypeSymbol type)
    {
        const string IResultFqn = "Microsoft.AspNetCore.Http.IResult";

        if (type.ToDisplayString() == IResultFqn)
        {
            return true;
        }

        return type.AllInterfaces.Any(iface => iface.ToDisplayString() == IResultFqn);
    }

    private static bool HasBindingAttribute(ParameterSyntax parameter) =>
        parameter.AttributeLists
            .SelectMany(attrList => attrList.Attributes)
            .Any(attr => BindingAttributeNames.Contains(attr.Name.ToString()));
}
