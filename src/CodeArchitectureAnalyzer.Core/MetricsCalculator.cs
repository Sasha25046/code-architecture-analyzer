using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core;

/// <summary>Метрики одного методу (підрозділ 2.3.3 звіту).</summary>
public sealed record MethodMetrics(
    string MethodName,
    string FilePath,
    int Line,
    int Loc,
    int CyclomaticComplexity,
    int MaxNestingDepth,
    int AccessedVariables);

/// <summary>Метрики всіх методів одного файлу.</summary>
public sealed record CodeMetrics(IReadOnlyList<MethodMetrics> Methods);

/// <summary>Метрики одного класу (підрозділ 2.3.1 звіту). Lcom дорівнює null, якщо метрику не визначено.</summary>
public sealed record ClassMetrics(
    string TypeName,
    string FilePath,
    int Line,
    int Loc,
    int Nom,
    int Nof,
    int Cbo,
    double? Lcom);

/// <summary>
/// Обчислює метрики методів (LOC, CC, MaxNestingDepth, NOAV), метрики класів (LOC, NOM, NOF, CBO, LCOM)
/// та кількість звернень методу до членів різних класів (для правила Заздрість до чужих властивостей).
/// </summary>
public sealed class MetricsCalculator
{
    // ------------------------------------------------------------------ методи

    public CodeMetrics CalculateMetrics(Compilation compilation, SyntaxTree tree)
    {
        var model = compilation.GetSemanticModel(tree);

        var methods = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Body is not null || m.ExpressionBody is not null)
            .Select(m => CalculateMethodMetrics(m, model))
            .ToList();

        return new CodeMetrics(methods);
    }

    public MethodMetrics CalculateMethodMetrics(MethodDeclarationSyntax method, SemanticModel model)
    {
        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;

        var span = method.SyntaxTree.GetLineSpan(method.Span);
        var loc = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

        return new MethodMetrics(
            method.Identifier.Text,
            span.Path,
            span.StartLinePosition.Line + 1,
            loc,
            body is null ? 1 : CyclomaticComplexity(body),
            body is null ? 0 : MaxNestingDepth(body, 0),
            body is null ? 0 : AccessedVariables(body, model));
    }

    /// <summary>
    /// CC за Маккейбом: кількість предикатних вузлів плюс одиниця. Враховуються if, case, while, for, foreach,
    /// catch, умовна операція, логічні &amp;&amp; та ||.
    /// </summary>
    private static int CyclomaticComplexity(SyntaxNode body)
    {
        var predicates = 0;

        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case IfStatementSyntax:
                case WhileStatementSyntax:
                case ForStatementSyntax:
                case CommonForEachStatementSyntax:
                case CatchClauseSyntax:
                case ConditionalExpressionSyntax:
                case CaseSwitchLabelSyntax:
                case CasePatternSwitchLabelSyntax:
                    predicates++;
                    break;
                case BinaryExpressionSyntax binary
                    when binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression):
                    predicates++;
                    break;
            }
        }

        return predicates + 1;
    }

    /// <summary>Максимальна глибина вкладених керуючих конструкцій; else-if не збільшує глибину.</summary>
    private static int MaxNestingDepth(SyntaxNode node, int depth)
    {
        var max = depth;

        foreach (var child in node.ChildNodes())
        {
            var childDepth = IsNestingConstruct(child) && !IsElseIf(child) ? depth + 1 : depth;
            max = Math.Max(max, MaxNestingDepth(child, childDepth));
        }

        return max;
    }

    private static bool IsNestingConstruct(SyntaxNode node) =>
        node is IfStatementSyntax
            or ForStatementSyntax
            or CommonForEachStatementSyntax
            or WhileStatementSyntax
            or DoStatementSyntax
            or SwitchStatementSyntax
            or TryStatementSyntax;

    private static bool IsElseIf(SyntaxNode node) => node is IfStatementSyntax && node.Parent is ElseClauseSyntax;

    /// <summary>NOAV: кількість різних параметрів, локальних змінних і полів (не констант), до яких є звернення.</summary>
    private static int AccessedVariables(SyntaxNode body, SemanticModel model)
    {
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(identifier).Symbol;
            if (symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol { IsConst: false })
            {
                symbols.Add(symbol.OriginalDefinition);
            }
        }

        return symbols.Count;
    }

    // ------------------------------------------------------------------ класи

    /// <summary>
    /// Метрики класу. LOC — сума рядків усіх оголошень (partial); NOM — звичайні методи; NOF — екземплярні поля
    /// (не константи й не службові поля автовластивостей); LCOM за формулою (2.19); CBO — кількість різних
    /// сторонніх типів, на які посилається клас (базовий тип, інтерфейси та імена типів і членів у коді класу).
    /// </summary>
    public ClassMetrics CalculateClassMetrics(Compilation compilation, INamedTypeSymbol type)
    {
        var declarations = type.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).ToList();

        var loc = 0;
        foreach (var declaration in declarations)
        {
            var span = declaration.SyntaxTree.GetLineSpan(declaration.Span);
            loc += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        }

        var path = string.Empty;
        var line = 0;
        if (declarations.Count > 0)
        {
            var first = declarations[0].SyntaxTree.GetLineSpan(declarations[0].Span);
            path = first.Path;
            line = first.StartLinePosition.Line + 1;
        }

        var methods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared)
            .ToList();
        var fields = type.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared)
            .ToList();

        // μ(f): кількість методів, які звертаються до поля f.
        var usage = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        foreach (var field in fields)
        {
            usage[field.OriginalDefinition] = 0;
        }

        foreach (var method in methods)
        {
            var used = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                var node = reference.GetSyntax();
                var model = compilation.GetSemanticModel(node.SyntaxTree);

                foreach (var identifier in node.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (model.GetSymbolInfo(identifier).Symbol is IFieldSymbol referenced
                        && usage.ContainsKey(referenced.OriginalDefinition))
                    {
                        used.Add(referenced.OriginalDefinition);
                    }
                }
            }

            foreach (var field in used)
            {
                usage[field]++;
            }
        }

        double? lcom = null;
        if (methods.Count > 1 && fields.Count > 0)
        {
            double sum = usage.Values.Sum();
            lcom = (methods.Count - sum / fields.Count) / (methods.Count - 1);
        }

        return new ClassMetrics(
            type.ToDisplayString(), path, line, loc, methods.Count, fields.Count,
            CouplingBetweenObjects(compilation, type), lcom);
    }

    private static int CouplingBetweenObjects(Compilation compilation, INamedTypeSymbol type)
    {
        var self = type.OriginalDefinition;
        var coupled = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        void Add(ITypeSymbol? candidate)
        {
            switch (candidate)
            {
                case IArrayTypeSymbol array:
                    Add(array.ElementType);
                    break;

                case INamedTypeSymbol named:
                {
                    var definition = named.OriginalDefinition;
                    if (definition.SpecialType == SpecialType.None
                        && !definition.IsAnonymousType
                        && !IsSelfOrNested(definition, self))
                    {
                        coupled.Add(definition);
                    }

                    break;
                }
            }
        }

        Add(type.BaseType);
        foreach (var implemented in type.Interfaces)
        {
            Add(implemented);
        }

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);

            foreach (var name in declaration.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(name).Symbol;

                if (symbol is ITypeSymbol typeSymbol)
                {
                    Add(typeSymbol);
                }
                else if (symbol is IMethodSymbol or IFieldSymbol or IPropertySymbol or IEventSymbol)
                {
                    Add(symbol.ContainingType);
                }
            }
        }

        return coupled.Count;
    }

    private static bool IsSelfOrNested(INamedTypeSymbol candidate, INamedTypeSymbol self)
    {
        for (INamedTypeSymbol? current = candidate; current is not null; current = current.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, self))
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------ звернення до членів класів

    /// <summary>
    /// AccessCount(m, T): кількість звернень методу до полів (не констант), властивостей і методів класів,
    /// оголошених в аналізованому коді. Члени, успадковані від базових класів owner, зараховуються до owner.
    /// </summary>
    public IReadOnlyDictionary<INamedTypeSymbol, int> CalculateAccessCounts(
        MethodDeclarationSyntax method, SemanticModel model, INamedTypeSymbol owner)
    {
        var counts = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        var ownerDefinition = owner.OriginalDefinition;

        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return counts;
        }

        foreach (var name in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name).Symbol;

            if (symbol is not (IFieldSymbol or IPropertySymbol or IMethodSymbol))
            {
                continue;
            }

            if (symbol is IFieldSymbol { IsConst: true } || symbol is IMethodSymbol { MethodKind: MethodKind.Constructor })
            {
                continue;
            }

            var container = symbol.ContainingType?.OriginalDefinition;
            if (container is null || container.DeclaringSyntaxReferences.Length == 0)
            {
                continue; // бібліотечні типи не враховуються
            }

            var key = IsSelfOrBase(ownerDefinition, container) ? ownerDefinition : container;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static bool IsSelfOrBase(INamedTypeSymbol owner, INamedTypeSymbol container)
    {
        if (SymbolEqualityComparer.Default.Equals(owner, container))
        {
            return true;
        }

        for (var current = owner.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, container))
            {
                return true;
            }
        }

        return false;
    }
}
