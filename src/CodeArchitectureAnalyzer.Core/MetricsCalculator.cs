using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core;

public sealed record MethodMetrics(
    string MethodName,
    string FilePath,
    int Line,
    int Loc,
    int CyclomaticComplexity,
    int MaxNestingDepth,
    int AccessedVariables);

public sealed record CodeMetrics(IReadOnlyList<MethodMetrics> Methods);


public sealed class MetricsCalculator
{
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
}
