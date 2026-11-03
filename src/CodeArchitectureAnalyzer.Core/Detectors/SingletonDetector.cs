using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core.Detectors;

/// <summary>
/// Детектор шаблону Одинак (Singleton). Працює із символами семантичної моделі,
/// а не з текстовими назвами типів. Усі умови обчислюються незалежно, щоб результат можна було пояснити.
/// </summary>
public sealed class SingletonDetector : IPatternDetector
{
    private const string Name = "Singleton";

    public string PatternName => Name;

    public IEnumerable<DetectionResult> Detect(Compilation compilation)
    {
        // Для partial-класів символ типу єдиний, тому результат формується один раз.
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var declarations = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var declaration in declarations)
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type)
                {
                    continue;
                }

                if (visited.Add(type))
                {
                    yield return Evaluate(compilation, type);
                }
            }
        }
    }

    private static DetectionResult Evaluate(Compilation compilation, INamedTypeSymbol type)
    {
        var conditions = new List<ConditionResult>();

        // C0, правило (2.1): статичний клас не може мати екземпляра, тому не є Одинаком за GoF.
        conditions.Add(Check(
            "C0", "Клас не є статичним", !type.IsStatic,
            "Модифікатор static відсутній.",
            "Клас оголошено як static."));

        // C1, правило (2.2): є явні конструктори, і всі вони закриті.
        var explicitConstructors = type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared).ToList();
        var openConstructors = explicitConstructors.Where(c => c.DeclaredAccessibility != Accessibility.Private).ToList();
        var constructorsClosed = explicitConstructors.Count > 0 && openConstructors.Count == 0;
        conditions.Add(Check(
            "C1", "Усі явно оголошені конструктори закриті (private)", constructorsClosed,
            $"Закритих конструкторів: {explicitConstructors.Count}.",
            explicitConstructors.Count == 0
                ? "Явних конструкторів немає: компілятор створює відкритий конструктор за замовчуванням."
                : "Є конструктори з іншим доступом: " +
                  string.Join(", ", openConstructors.Select(c => c.DeclaredAccessibility.ToString().ToLowerInvariant())) + "."));

        // C2, правило (2.3): статичне сховище екземпляра (поле або властивість типу класу, або Lazy<T>).
        var storage = type.GetMembers()
            .Where(m => m.IsStatic && !m.IsImplicitlyDeclared)
            .Where(m => HoldsInstance(MemberType(m), type))
            .ToList();
        conditions.Add(Check(
            "C2", "Є статичне поле або властивість типу самого класу", storage.Count > 0,
            "Знайдено: " + string.Join(", ", storage.Select(s => s.Name)) + ".",
            "Статичного поля чи властивості типу самого класу немає."));

        // C3, правило (2.4): відкритий статичний член, що повертає екземпляр класу.
        var accessors = type.GetMembers()
            .Where(m => m.IsStatic && !m.IsImplicitlyDeclared && m.DeclaredAccessibility == Accessibility.Public)
            .Where(m => IsInstanceAccessor(m, type))
            .ToList();
        conditions.Add(Check(
            "C3", "Є відкритий статичний член, що повертає екземпляр класу", accessors.Count > 0,
            "Знайдено: " + string.Join(", ", accessors.Select(a => a.Name)) + ".",
            "Відкритого статичного члена, що повертає екземпляр класу, немає."));

        // C4, правило (2.5): відкритий член повертає саме збережений екземпляр.
        var returnsStored = accessors.Any(a => ReturnsStoredInstance(compilation, type, a, storage));
        conditions.Add(Check(
            "C4", "Відкритий член повертає збережений екземпляр", returnsStored,
            "Відкритий член є сховищем, ініціалізованим створенням екземпляра, або його вираз повернення звертається до сховища.",
            "Жоден відкритий член не повертає збережений екземпляр: немає ініціалізації сховища створенням екземпляра і виразу повернення, що звертається до сховища."));

        return new DetectionResult(Name, type.ToDisplayString(), FilePathOf(type), LineOf(type), conditions);
    }

    private static ConditionResult Check(string id, string description, bool passed, string passMessage, string failMessage) =>
        new(id, description, passed, passed ? passMessage : failMessage);

    private static ITypeSymbol? MemberType(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => null
    };

    private static bool IsInstanceAccessor(ISymbol member, INamedTypeSymbol type) => member switch
    {
        IFieldSymbol field => Same(field.Type, type),
        IPropertySymbol property => property.GetMethod is not null && Same(property.Type, type),
        IMethodSymbol { MethodKind: MethodKind.Ordinary } method => Same(method.ReturnType, type),
        _ => false
    };

    private static bool HoldsInstance(ITypeSymbol? candidate, INamedTypeSymbol type)
    {
        if (candidate is null)
        {
            return false;
        }

        if (Same(candidate, type))
        {
            return true;
        }

        // Lazy<TClass>: поширена форма потокобезпечної відкладеної ініціалізації.
        return candidate is INamedTypeSymbol { IsGenericType: true } generic
               && generic.TypeArguments.Length == 1
               && generic.OriginalDefinition.ToDisplayString() == "System.Lazy<T>"
               && Same(generic.TypeArguments[0], type);
    }

    /// <summary>
    /// Умова C4 (2.5): а) вираз повернення члена звертається до сховища, або
    /// б) сам член є сховищем, ініціалізованим створенням екземпляра чи присвоєним у статичному конструкторі.
    /// </summary>
    private static bool ReturnsStoredInstance(
        Compilation compilation, INamedTypeSymbol type, ISymbol accessor, IReadOnlyCollection<ISymbol> storage)
    {
        if (storage.Contains(accessor, SymbolEqualityComparer.Default)
            && IsInitializedWithInstance(compilation, type, accessor))
        {
            return true;
        }

        return ReturnExpressionsReferenceStorage(compilation, accessor, storage);
    }

    private static bool ReturnExpressionsReferenceStorage(
        Compilation compilation, ISymbol accessor, IReadOnlyCollection<ISymbol> storage)
    {
        foreach (var reference in accessor.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax();
            var model = compilation.GetSemanticModel(node.SyntaxTree);

            foreach (var expression in ReturnedExpressions(node))
            {
                foreach (var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    var symbol = model.GetSymbolInfo(identifier).Symbol;
                    if (symbol is not null && storage.Contains(symbol.OriginalDefinition, SymbolEqualityComparer.Default))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Вирази повернення: оператор return, вираз-тіло методу чи властивості, вираз-тіло акцесора get.</summary>
    private static IEnumerable<ExpressionSyntax> ReturnedExpressions(SyntaxNode declaration)
    {
        foreach (var node in declaration.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case ReturnStatementSyntax { Expression: not null } returnStatement:
                    yield return returnStatement.Expression!;
                    break;
                case ArrowExpressionClauseSyntax arrow:
                    yield return arrow.Expression;
                    break;
            }
        }
    }

    private static bool IsInitializedWithInstance(Compilation compilation, INamedTypeSymbol type, ISymbol member)
    {
        // Ініціалізатор поля чи автовластивості.
        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax();
            ExpressionSyntax? initializer = node switch
            {
                PropertyDeclarationSyntax property => property.Initializer?.Value,
                VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                _ => null
            };

            if (initializer is not null
                && CreatesInstance(initializer, compilation.GetSemanticModel(node.SyntaxTree), type))
            {
                return true;
            }
        }

        // Присвоєння в статичному конструкторі.
        foreach (var constructor in type.StaticConstructors)
        {
            foreach (var reference in constructor.DeclaringSyntaxReferences)
            {
                var node = reference.GetSyntax();
                var model = compilation.GetSemanticModel(node.SyntaxTree);

                foreach (var assignment in node.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    var target = model.GetSymbolInfo(assignment.Left).Symbol;
                    if (target is not null
                        && Same(target.OriginalDefinition, member.OriginalDefinition)
                        && CreatesInstance(assignment.Right, model, type))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool CreatesInstance(ExpressionSyntax expression, SemanticModel model, INamedTypeSymbol type)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
                && Same(model.GetTypeInfo(node).Type, type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Same(ISymbol? left, ISymbol? right) => SymbolEqualityComparer.Default.Equals(left, right);

    private static string FilePathOf(INamedTypeSymbol type)
    {
        var location = type.Locations.FirstOrDefault(l => l.IsInSource);
        return location is null ? string.Empty : location.GetLineSpan().Path;
    }

    private static int LineOf(INamedTypeSymbol type)
    {
        var location = type.Locations.FirstOrDefault(l => l.IsInSource);
        return location is null ? 0 : location.GetLineSpan().StartLinePosition.Line + 1;
    }
}
