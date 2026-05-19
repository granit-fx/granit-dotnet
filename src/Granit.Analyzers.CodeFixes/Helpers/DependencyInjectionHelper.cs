using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Granit.Analyzers.CodeFixes.Helpers;

/// <summary>
/// Shared helper for GRSEC001, GRSEC002, and GRSEC004 CodeFix providers.
/// Handles field injection, constructor parameter addition, using directive management,
/// and static context detection.
/// All methods operate on immutable syntax trees — callers must track nodes via <see cref="SyntaxAnnotation"/>.
/// </summary>
internal static class DependencyInjectionHelper
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="node"/> is inside a <c>static</c> method or property,
    /// meaning constructor-based DI cannot be applied.
    /// </summary>
    internal static bool IsInStaticContext(SyntaxNode node)
    {
        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax method)
            {
                return method.Modifiers.Any(SyntaxKind.StaticKeyword);
            }

            if (ancestor is PropertyDeclarationSyntax property)
            {
                return property.Modifiers.Any(SyntaxKind.StaticKeyword);
            }

            if (ancestor is ClassDeclarationSyntax)
            {
                break;
            }
        }

        return false;
    }


    /// <summary>
    /// Ensures a <c>private readonly</c> field exists in the class. Returns the modified class if added.
    /// </summary>
    internal static ClassDeclarationSyntax EnsureFieldExists(
        ClassDeclarationSyntax classDecl,
        string typeName,
        string fieldName)
    {
        // Check if the field already exists
        bool fieldExists = classDecl.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        if (fieldExists)
        {
            return classDecl;
        }

        // Create the field declaration node
        FieldDeclarationSyntax field = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.ParseTypeName(typeName),
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(fieldName))))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        // Insert before the first constructor, or at the beginning of members
        int insertIndex = 0;
        for (int i = 0; i < classDecl.Members.Count; i++)
        {
            if (classDecl.Members[i] is ConstructorDeclarationSyntax)
            {
                insertIndex = i;
                break;
            }

            if (i == classDecl.Members.Count - 1)
            {
                insertIndex = 0;
            }
        }

        return classDecl.WithMembers(classDecl.Members.Insert(insertIndex, field));
    }

    /// <summary>
    /// Ensures the constructor has a parameter of the given type and assigns it to the field.
    /// Creates a constructor if none exists.
    /// </summary>
    internal static ClassDeclarationSyntax EnsureConstructorParameter(
        ClassDeclarationSyntax classDecl,
        string typeName,
        string paramName,
        string fieldName)
    {
        ConstructorDeclarationSyntax? existingCtor = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        // Create the assignment statement node
        StatementSyntax assignment = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(fieldName),
                SyntaxFactory.IdentifierName(paramName)));

        // Build the parameter: TypeName paramName
        ParameterSyntax parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
            .WithType(SyntaxFactory.ParseTypeName(typeName));

        if (existingCtor is null)
        {
            // Create a new constructor
            SyntaxKind visibility = classDecl.Modifiers.Any(SyntaxKind.PublicKeyword)
                ? SyntaxKind.PublicKeyword
                : SyntaxKind.InternalKeyword;

            ConstructorDeclarationSyntax newCtor = SyntaxFactory.ConstructorDeclaration(classDecl.Identifier.WithoutTrivia())
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(visibility)))
                .WithParameterList(SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(parameter)))
                .WithBody(SyntaxFactory.Block(assignment));

            // Insert after the last field, or at the beginning
            int insertIndex = 0;
            for (int i = classDecl.Members.Count - 1; i >= 0; i--)
            {
                if (classDecl.Members[i] is FieldDeclarationSyntax)
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            return classDecl.WithMembers(classDecl.Members.Insert(insertIndex, newCtor));
        }

        // Check if the parameter type already exists in the constructor
        bool paramExists = existingCtor.ParameterList.Parameters
            .Any(p => p.Type?.ToString() == typeName);

        if (paramExists)
        {
            return classDecl;
        }

        // Add the parameter to the existing constructor
        SeparatedSyntaxList<ParameterSyntax> existingParams = existingCtor.ParameterList.Parameters;
        SeparatedSyntaxList<ParameterSyntax> newParams = existingParams.Count == 0
            ? SyntaxFactory.SingletonSeparatedList(parameter)
            : existingParams.Add(parameter);

        ConstructorDeclarationSyntax updatedCtor = existingCtor
            .WithParameterList(existingCtor.ParameterList.WithParameters(newParams));

        // Add the assignment to the constructor body
        if (updatedCtor.Body is not null)
        {
            updatedCtor = updatedCtor.WithBody(
                updatedCtor.Body.AddStatements(assignment));
        }
        else
        {
            updatedCtor = updatedCtor
                .WithBody(SyntaxFactory.Block(assignment))
                .WithExpressionBody(null)
                .WithSemicolonToken(default);
        }

        return classDecl.ReplaceNode(existingCtor, updatedCtor);
    }

    /// <summary>
    /// Ensures a using directive exists at the compilation unit level.
    /// </summary>
    internal static CompilationUnitSyntax EnsureUsingDirective(
        CompilationUnitSyntax root,
        string namespaceName)
    {
        bool alreadyExists = root.Usings
            .Any(u => u.Name?.ToString() == namespaceName);

        if (alreadyExists)
        {
            return root;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        return root.AddUsings(usingDirective);
    }
}
