using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core.Detectors;


public sealed class FactoryMethodDetector : IPatternDetector
{
    private const string Name = "Factory Method";
    private const int MaxLocalDepth = 5;

    public string PatternName => Name;

    public IEnumerable<DetectionResult> Detect(Compilation compilation)
    {
        var types = AllTypes(compilation.Assembly.GlobalNamespace).ToList();

        foreach (var type in types)
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (IsCandidate(method))
                {
                    yield return Evaluate(compilation, types, method);
                }
            }
        }
    }

    private static bool IsCandidate(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary
        && !method.IsStatic
        && !method.IsOverride
        && method.DeclaringSyntaxReferences.Length > 0
        && (IsPolymorphic(method) || ReturnsAbstractType(method));

    private static bool IsPolymorphic(IMethodSymbol method) =>
        method.IsVirtual || method.IsAbstract || method.ContainingType.TypeKind == TypeKind.Interface;

    private static bool ReturnsAbstractType(IMethodSymbol method) =>
        method.ReturnType is INamedTypeSymbol type
        && (type.TypeKind == TypeKind.Interface || (type.TypeKind == TypeKind.Class && type.IsAbstract));

    private static DetectionResult Evaluate(Compilation compilation, IReadOnlyList<INamedTypeSymbol> types, IMethodSymbol method)
    {
        var conditions = new List<ConditionResult>();

        // F1 (2.7)
        var polymorphic = IsPolymorphic(method);
        conditions.Add(new ConditionResult(
            "F1", "Метод поліморфний (virtual, abstract або член інтерфейсу)", polymorphic,
            polymorphic ? "Метод можна перевизначити чи реалізувати." : "Метод не є віртуальним, абстрактним чи інтерфейсним."));

        // F2 (2.8)
        var abstractReturn = ReturnsAbstractType(method);
        conditions.Add(new ConditionResult(
            "F2", "Тип повернення є інтерфейсом або абстрактним класом", abstractReturn,
            abstractReturn
                ? $"Тип повернення: {method.ReturnType.ToDisplayString()}."
                : $"Тип повернення {method.ReturnType.ToDisplayString()} не є інтерфейсом чи абстрактним класом."));

        // F3 (2.9)
        var implementations = FindImplementations(types, method);
        var creators = implementations.Select(i => i.ContainingType).Distinct(SymbolEqualityComparer.Default).Count();
        conditions.Add(new ConditionResult(
            "F3", "Є реалізація методу в похідному класі (перевизначення чи реалізація інтерфейсу)", implementations.Count > 0,
            implementations.Count > 0
                ? $"Реалізацій: {implementations.Count}; класів-створювачів: {creators}."
                : "Перевизначень чи реалізацій у компіляції не знайдено."));

        // F4 (2.10)
        var created = CollectCreatedTypes(compilation, implementations);
        var compatible = method.ReturnType is INamedTypeSymbol returnType
            ? created.Where(t => t.IsReferenceType && !t.IsAbstract && !Same(t, returnType) && IsSubtype(t, returnType))
                .Select(t => t.ToDisplayString()).OrderBy(n => n).ToList()
            : new List<string>();
        conditions.Add(new ConditionResult(
            "F4", "Реалізація повертає новий екземпляр конкретного типу, сумісного з типом результату", compatible.Count > 0,
            compatible.Count > 0
                ? $"Створювані типи: {string.Join(", ", compatible)}."
                : "Не знайдено створення конкретного типу (new T(...) безпосередньо або через локальну змінну), сумісного з типом результату."));

        var location = method.Locations.FirstOrDefault(l => l.IsInSource);
        var path = location is null ? string.Empty : location.GetLineSpan().Path;
        var line = location is null ? 0 : location.GetLineSpan().StartLinePosition.Line + 1;

        return new DetectionResult(Name, method.ToDisplayString(), path, line, conditions);
    }

    private static List<IMethodSymbol> FindImplementations(IReadOnlyList<INamedTypeSymbol> types, IMethodSymbol method)
    {
        var result = new List<IMethodSymbol>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var container = method.ContainingType;

        foreach (var type in types.Where(t => t.TypeKind == TypeKind.Class))
        {
            if (container.TypeKind == TypeKind.Interface)
            {
                if (!type.AllInterfaces.Any(i => Same(i, container)))
                {
                    continue;
                }

                if (type.FindImplementationForInterfaceMember(method) is IMethodSymbol implementation
                    && IsConcreteSourceMethod(implementation)
                    && seen.Add(implementation))
                {
                    result.Add(implementation);
                }
            }
            else
            {
                // Форма А: ланцюг OverriddenMethod містить базовий метод
                foreach (var candidate in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if (candidate.IsOverride
                        && IsConcreteSourceMethod(candidate)
                        && OverrideChainContains(candidate, method)
                        && seen.Add(candidate))
                    {
                        result.Add(candidate);
                    }
                }
            }
        }

        return result;
    }

    private static bool IsConcreteSourceMethod(IMethodSymbol method) =>
        !method.IsAbstract && method.DeclaringSyntaxReferences.Length > 0;

    private static bool OverrideChainContains(IMethodSymbol candidate, IMethodSymbol method)
    {
        for (var current = candidate.OverriddenMethod; current is not null; current = current.OverriddenMethod)
        {
            if (Same(current.OriginalDefinition, method.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<INamedTypeSymbol> CollectCreatedTypes(Compilation compilation, IEnumerable<IMethodSymbol> implementations)
    {
        var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var implementation in implementations)
        {
            foreach (var reference in implementation.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not MethodDeclarationSyntax declaration)
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(declaration.SyntaxTree);

                foreach (var expression in ReturnExpressions(declaration))
                {
                    var created = CreatedTypes(expression, model, declaration, 0);
                    if (created is not null)
                    {
                        result.UnionWith(created);
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<ExpressionSyntax> ReturnExpressions(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody is not null)
        {
            yield return method.ExpressionBody.Expression;
        }

        if (method.Body is null)
        {
            yield break;
        }

        foreach (var statement in method.Body.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (statement.Expression is not null && BelongsTo(statement, method))
            {
                yield return statement.Expression;
            }
        }
    }

    private static bool BelongsTo(SyntaxNode node, MethodDeclarationSyntax method)
    {
        var owner = node.Ancestors().First(a =>
            a is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
        return owner == method;
    }

    private static HashSet<INamedTypeSymbol>? CreatedTypes(ExpressionSyntax expression, SemanticModel model, MethodDeclarationSyntax method, int depth)
    {
        if (depth > MaxLocalDepth)
        {
            return null;
        }

        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return CreatedTypes(parenthesized.Expression, model, method, depth);

            case CastExpressionSyntax cast:
                return CreatedTypes(cast.Expression, model, method, depth);

            case ObjectCreationExpressionSyntax:
            case ImplicitObjectCreationExpressionSyntax:
            {
                var info = model.GetTypeInfo(expression);
                return (info.Type ?? info.ConvertedType) is INamedTypeSymbol createdType
                    ? new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { createdType }
                    : null;
            }

            case ConditionalExpressionSyntax conditional:
            {
                var whenTrue = CreatedTypes(conditional.WhenTrue, model, method, depth);
                var whenFalse = CreatedTypes(conditional.WhenFalse, model, method, depth);
                if (whenTrue is null || whenFalse is null)
                {
                    return null;
                }

                whenTrue.UnionWith(whenFalse);
                return whenTrue;
            }

            case IdentifierNameSyntax identifier when model.GetSymbolInfo(identifier).Symbol is ILocalSymbol local:
                return CreatedTypesOfLocal(local, model, method, depth + 1);

            default:
                return null;
        }
    }

    private static HashSet<INamedTypeSymbol>? CreatedTypesOfLocal(ILocalSymbol local, SemanticModel model, MethodDeclarationSyntax method, int depth)
    {
        var assigned = new List<ExpressionSyntax>();

        foreach (var node in method.DescendantNodes())
        {
            if (node is VariableDeclaratorSyntax declarator
                && declarator.Initializer is not null
                && Same(model.GetDeclaredSymbol(declarator), local))
            {
                assigned.Add(declarator.Initializer.Value);
            }
            else if (node is AssignmentExpressionSyntax assignment
                     && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                     && Same(model.GetSymbolInfo(assignment.Left).Symbol, local))
            {
                assigned.Add(assignment.Right);
            }
        }

        if (assigned.Count == 0)
        {
            return null;
        }

        var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var expression in assigned)
        {
            var created = CreatedTypes(expression, model, method, depth);
            if (created is null)
            {
                return null;
            }

            result.UnionWith(created);
        }

        return result;
    }

    private static bool IsSubtype(INamedTypeSymbol type, INamedTypeSymbol target)
    {
        if (target.TypeKind == TypeKind.Interface)
        {
            return type.AllInterfaces.Any(i => Same(i, target));
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (Same(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol child:
                    foreach (var inner in AllTypes(child))
                    {
                        yield return inner;
                    }
                    break;

                case INamedTypeSymbol named:
                    yield return named;
                    foreach (var nested in NestedTypes(named))
                    {
                        yield return nested;
                    }
                    break;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> NestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in NestedTypes(nested))
            {
                yield return deeper;
            }
        }
    }

    private static bool Same(ISymbol? left, ISymbol? right) => SymbolEqualityComparer.Default.Equals(left, right);
}
