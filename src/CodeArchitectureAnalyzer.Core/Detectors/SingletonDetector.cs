using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core.Detectors;

public sealed class SingletonDetector : IPatternDetector
{
    private const string Name = "Singleton";

    public string PatternName => Name;

    public IEnumerable<DetectionResult> Detect(Compilation compilation)
    {
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

        // C0: статичний клас не може мати екземпляра, тому не є Одинаком за GoF.
        conditions.Add(Check(
            "C0", "Клас не є статичним", !type.IsStatic,
            "Модифікатор static відсутній.",
            "Клас оголошено як static."));

        // C1, правило (2.1): є явні конструктори, і всі вони закриті.
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

        // C2, правило (2.2): статичне сховище екземпляра (поле або властивість типу класу, або Lazy<T>).
        var storage = type.GetMembers()
            .Where(m => m.IsStatic && !m.IsImplicitlyDeclared)
            .Where(m => HoldsInstance(MemberType(m), type))
            .ToList();
        conditions.Add(Check(
            "C2", "Є статичне поле або властивість типу самого класу", storage.Count > 0,
            "Знайдено: " + string.Join(", ", storage.Select(s => s.Name)) + ".",
            "Статичного поля чи властивості типу самого класу немає."));

        // C3, правило (2.3): відкритий статичний член, що повертає екземпляр класу.
        var accessors = type.GetMembers()
            .Where(m => m.IsStatic && !m.IsImplicitlyDeclared && m.DeclaredAccessibility == Accessibility.Public)
            .Where(m => IsInstanceAccessor(m, type))
            .ToList();
        conditions.Add(Check(
            "C3", "Є відкритий статичний член, що повертає екземпляр класу", accessors.Count > 0,
            "Знайдено: " + string.Join(", ", accessors.Select(a => a.Name)) + ".",
            "Відкритого статичного члена, що повертає екземпляр класу, немає."));

        // C4 (доповнення): відкритий член повертає саме збережений екземпляр.
        var returnsStored = accessors.Any(a =>
            storage.Contains(a, SymbolEqualityComparer.Default) || ReferencesStorage(compilation, a, storage));
        conditions.Add(Check(
            "C4", "Відкритий член повертає збережений екземпляр", returnsStored,
            "Відкритий член є сховищем або звертається до нього.",
            "Жоден відкритий член не звертається до статичного сховища екземпляра."));

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

        return candidate is INamedTypeSymbol { IsGenericType: true } generic
               && generic.TypeArguments.Length == 1
               && generic.OriginalDefinition.ToDisplayString() == "System.Lazy<T>"
               && Same(generic.TypeArguments[0], type);
    }

    private static bool ReferencesStorage(Compilation compilation, ISymbol accessor, IReadOnlyCollection<ISymbol> storage)
    {
        foreach (var reference in accessor.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax();
            var model = compilation.GetSemanticModel(node.SyntaxTree);

            foreach (var identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(identifier).Symbol;
                if (symbol is not null && storage.Contains(symbol.OriginalDefinition, SymbolEqualityComparer.Default))
                {
                    return true;
                }
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
