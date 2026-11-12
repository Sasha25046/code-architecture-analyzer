using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core.Detectors;

public sealed record BrainMethodThresholds(
    int CyclomaticComplexity = 10,
    int Loc = 50,
    int MaxNestingDepth = 4,
    int Noav = 7);


public sealed class BrainMethodDetector : IPatternDetector
{
    private const string Name = "Brain Method";

    private readonly BrainMethodThresholds _thresholds;
    private readonly MetricsCalculator _calculator;

    public BrainMethodDetector()
        : this(new BrainMethodThresholds(), new MetricsCalculator())
    {
    }

    public BrainMethodDetector(BrainMethodThresholds thresholds, MetricsCalculator calculator)
    {
        _thresholds = thresholds;
        _calculator = calculator;
    }

    public string PatternName => Name;

    public IEnumerable<DetectionResult> Detect(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.Body is null && method.ExpressionBody is null)
                {
                    continue; // абстрактні та інтерфейсні методи не мають тіла
                }

                var metrics = _calculator.CalculateMethodMetrics(method, model);
                var name = model.GetDeclaredSymbol(method)?.ToDisplayString() ?? metrics.MethodName;

                var conditions = new List<ConditionResult>
                {
                    Check("B1", $"Цикломатична складність CC ≥ {_thresholds.CyclomaticComplexity}",
                        metrics.CyclomaticComplexity, _thresholds.CyclomaticComplexity),
                    Check("B2", $"Кількість рядків LOC ≥ {_thresholds.Loc}",
                        metrics.Loc, _thresholds.Loc),
                    Check("B3", $"Глибина вкладеності MaxNestingDepth ≥ {_thresholds.MaxNestingDepth}",
                        metrics.MaxNestingDepth, _thresholds.MaxNestingDepth),
                    Check("B4", $"Кількість змінних NOAV ≥ {_thresholds.Noav}",
                        metrics.AccessedVariables, _thresholds.Noav),
                };

                yield return new DetectionResult(Name, name, metrics.FilePath, metrics.Line, conditions);
            }
        }
    }

    private static ConditionResult Check(string id, string description, int value, int threshold) =>
        new(id, description, value >= threshold, $"Значення {value}, поріг {threshold}.");
}
