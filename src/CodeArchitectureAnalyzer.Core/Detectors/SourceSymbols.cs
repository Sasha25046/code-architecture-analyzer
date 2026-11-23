using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CodeArchitectureAnalyzer.Core.Detectors;

/// <summary>Спільні допоміжні методи для детекторів, що працюють із символами типів аналізованого коду.</summary>
internal static class SourceSymbols
{
    public static IEnumerable<INamedTypeSymbol> AllTypes(Compilation compilation) =>
        AllTypes(compilation.Assembly.GlobalNamespace);

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

    public static bool Same(ISymbol? left, ISymbol? right) => SymbolEqualityComparer.Default.Equals(left, right);

    /// <summary>Тип реалізує інтерфейс або успадковує клас target.</summary>
    public static bool IsSubtype(INamedTypeSymbol type, INamedTypeSymbol target)
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

    public static (string Path, int Line) LocationOf(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
        {
            return (string.Empty, 0);
        }

        var span = location.GetLineSpan();
        return (span.Path, span.StartLinePosition.Line + 1);
    }
}
