using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core.Detectors;

/// <summary>
/// Детектор структури, сумісної з шаблоном Стратегія (умови S1–S5, формули 2.12–2.17).
/// Кандидатами є пари (контекст, контракт), де контекст має нестатичне поле чи властивість типу інтерфейсу
/// або абстрактного класу, оголошеного в аналізованому коді.
/// </summary>
public sealed class StrategyDetector : IPatternDetector
{
    private const string Name = "Strategy";

    public string PatternName => Name;

    public IEnumerable<DetectionResult> Detect(Compilation compilation)
    {
        var types = SourceSymbols.AllTypes(compilation).ToList();

        foreach (var context in types.Where(t => t.TypeKind == TypeKind.Class))
        {
            foreach (var contract in CandidateContracts(context))
            {
                yield return Evaluate(compilation, types, context, contract);
            }
        }
    }

    private static List<INamedTypeSymbol> CandidateContracts(INamedTypeSymbol context)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var member in context.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
            {
                continue;
            }

            if (MemberType(member) is INamedTypeSymbol type
                && IsContract(type)
                && type.DeclaringSyntaxReferences.Length > 0
                && !result.Any(r => SourceSymbols.Same(r, type)))
            {
                result.Add(type);
            }
        }

        return result;
    }

    private static bool IsContract(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Interface || (type.TypeKind == TypeKind.Class && type.IsAbstract);

    private static ITypeSymbol? MemberType(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => null
    };

    private static DetectionResult Evaluate(
        Compilation compilation, IReadOnlyList<INamedTypeSymbol> types, INamedTypeSymbol context, INamedTypeSymbol contract)
    {
        var conditions = new List<ConditionResult>();

        // S1 (2.12): контракт — інтерфейс чи абстрактний клас із поведінковим методом.
        var methodCount = ContractMethodCount(contract);
        var s1 = IsContract(contract) && methodCount >= 1;
        conditions.Add(new ConditionResult(
            "S1", "Контракт є інтерфейсом або абстрактним класом із принаймні одним методом", s1,
            s1 ? $"Методів контракту: {methodCount}." : "Контракт не має методів."));

        // S2 (2.13): щонайменше дві неабстрактні реалізації, серед яких немає контексту.
        var implementations = types
            .Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract && SourceSymbols.IsSubtype(t, contract))
            .ToList();
        var contextIsImplementation = implementations.Any(i => SourceSymbols.Same(i, context));
        var s2 = implementations.Count >= 2 && !contextIsImplementation;
        conditions.Add(new ConditionResult(
            "S2", "Контракт має щонайменше дві реалізації, і контекст не є однією з них", s2,
            $"Реалізацій: {implementations.Count} ({string.Join(", ", implementations.Take(5).Select(i => i.Name))}); " +
            $"контекст {(contextIsImplementation ? "сам є реалізацією" : "не є реалізацією")}."));

        // S3 (2.14): нестатичне закрите чи захищене поле або властивість типу контракту.
        var storage = context.GetMembers()
            .Where(m => !m.IsStatic
                        && !m.IsImplicitlyDeclared
                        && m.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected
                        && SourceSymbols.Same(MemberType(m), contract))
            .ToList();
        conditions.Add(new ConditionResult(
            "S3", "Є закрите чи захищене нестатичне поле або властивість типу контракту", storage.Count > 0,
            storage.Count > 0
                ? $"Знайдено: {string.Join(", ", storage.Select(s => s.Name))}."
                : "Закритого чи захищеного поля або властивості типу контракту немає."));

        // S4 (2.15): конструктор чи метод приймає параметр типу контракту й присвоює його полю.
        var injections = FindInjections(compilation, context, contract, storage);
        var owners = injections
            .Select(i => i.Owner.MethodKind == MethodKind.Constructor ? $"конструктор {context.Name}" : i.Owner.Name)
            .Distinct()
            .ToList();
        var mutators = injections
            .Where(i => i.Owner.MethodKind != MethodKind.Constructor)
            .Select(i => i.Owner.Name)
            .Distinct()
            .ToList();
        conditions.Add(new ConditionResult(
            "S4", "Стратегія передається в контекст через параметр і зберігається в полі", injections.Count > 0,
            injections.Count > 0
                ? $"Передавання: {string.Join(", ", owners)}; змінність: {(mutators.Count > 0 ? "так (" + string.Join(", ", mutators) + ")" : "ні")}."
                : "Жоден конструктор чи метод не присвоює параметр типу контракту полю."));

        // S5 (2.16): виклик методу контракту на цьому полі.
        var delegationFields = injections.Count > 0
            ? injections.Select(i => i.Member).ToList()
            : storage;
        var delegations = FindDelegations(compilation, context, contract, delegationFields);
        conditions.Add(new ConditionResult(
            "S5", "Метод контексту делегує виклик методу контракту через поле", delegations.Count > 0,
            delegations.Count > 0
                ? $"Делегування: {string.Join(", ", delegations.Distinct().Take(5))}."
                : "Викликів методів контракту на полі стратегії не знайдено."));

        var (path, line) = SourceSymbols.LocationOf(context);
        return new DetectionResult(Name, $"{context.ToDisplayString()} → {contract.ToDisplayString()}", path, line, conditions);
    }

    private static int ContractMethodCount(INamedTypeSymbol contract)
    {
        var count = contract.GetMembers().OfType<IMethodSymbol>().Count(m => m.MethodKind == MethodKind.Ordinary);

        if (contract.TypeKind == TypeKind.Interface)
        {
            count += contract.AllInterfaces
                .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                .Count(m => m.MethodKind == MethodKind.Ordinary);
        }

        return count;
    }

    private static List<(ISymbol Member, IMethodSymbol Owner)> FindInjections(
        Compilation compilation, INamedTypeSymbol context, INamedTypeSymbol contract, IReadOnlyCollection<ISymbol> storage)
    {
        var result = new List<(ISymbol Member, IMethodSymbol Owner)>();

        foreach (var owner in context.GetMembers().OfType<IMethodSymbol>())
        {
            if (owner.IsStatic
                || (owner.MethodKind != MethodKind.Constructor && owner.MethodKind != MethodKind.Ordinary))
            {
                continue;
            }

            var parameters = owner.Parameters.Where(p => SourceSymbols.Same(p.Type, contract)).ToList();
            if (parameters.Count == 0)
            {
                continue;
            }

            foreach (var reference in owner.DeclaringSyntaxReferences)
            {
                var node = reference.GetSyntax();
                var model = compilation.GetSemanticModel(node.SyntaxTree);

                foreach (var assignment in node.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    {
                        continue;
                    }

                    var target = model.GetSymbolInfo(assignment.Left).Symbol;
                    if (target is null || !storage.Contains(target.OriginalDefinition, SymbolEqualityComparer.Default))
                    {
                        continue;
                    }

                    if (ParameterOf(assignment.Right, model) is { } parameter
                        && parameters.Any(p => SourceSymbols.Same(p, parameter)))
                    {
                        result.Add((target.OriginalDefinition, owner));
                    }
                }
            }
        }

        return result;
    }

    // Параметр у правій частині присвоєння: p, (p), p!, p ?? throw ..., (T)p.
    private static IParameterSymbol? ParameterOf(ExpressionSyntax expression, SemanticModel model)
    {
        var current = expression;

        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    current = cast.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    current = postfix.Operand;
                    continue;
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression):
                    current = binary.Left;
                    continue;
            }

            break;
        }

        return model.GetSymbolInfo(current).Symbol as IParameterSymbol;
    }

    private static List<string> FindDelegations(
        Compilation compilation, INamedTypeSymbol context, INamedTypeSymbol contract, IReadOnlyCollection<ISymbol> fields)
    {
        var found = new List<string>();

        foreach (var method in context.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
        {
            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                var node = reference.GetSyntax();
                var model = compilation.GetSemanticModel(node.SyntaxTree);

                foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var receiver = ReceiverOf(invocation, model);
                    if (receiver is null || !fields.Contains(receiver.OriginalDefinition, SymbolEqualityComparer.Default))
                    {
                        continue;
                    }

                    if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol target && IsContractMember(target, contract))
                    {
                        found.Add($"{method.Name} → {contract.Name}.{target.Name}");
                    }
                }
            }
        }

        return found;
    }

    // Отримувач виклику: f.M(), this.f.M(), f?.M().
    private static ISymbol? ReceiverOf(InvocationExpressionSyntax invocation, SemanticModel model) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => model.GetSymbolInfo(access.Expression).Symbol,
            MemberBindingExpressionSyntax => invocation.Ancestors()
                .OfType<ConditionalAccessExpressionSyntax>()
                .Select(c => model.GetSymbolInfo(c.Expression).Symbol)
                .FirstOrDefault(),
            _ => null
        };

    private static bool IsContractMember(IMethodSymbol target, INamedTypeSymbol contract)
    {
        var container = target.ContainingType;
        if (SourceSymbols.Same(container, contract))
        {
            return true;
        }

        if (contract.TypeKind == TypeKind.Interface)
        {
            return contract.AllInterfaces.Any(i => SourceSymbols.Same(i, container));
        }

        for (var current = contract.BaseType; current is not null; current = current.BaseType)
        {
            if (SourceSymbols.Same(current, container))
            {
                return true;
            }
        }

        return false;
    }
}
