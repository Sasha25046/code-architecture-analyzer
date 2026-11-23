using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeArchitectureAnalyzer.Core.Detectors;

/// <summary>Порогові значення правила Заздрість до чужих властивостей (формула 2.20).</summary>
public sealed record FeatureEnvyThresholds(double Share = 0.65, int MinAccess = 5);

/// <summary>
/// Детектор антипатерну Заздрість до чужих властивостей: частка звернень методу до членів одного стороннього класу B
/// не менша за поріг, а їх кількість не тривіальна. Враховуються лише класи аналізованого коду.
/// </summary>
public sealed class FeatureEnvyDetector : IPatternDetector
{
    private const string Name = "Feature Envy";

    private readonly FeatureEnvyThresholds _thresholds;
    private readonly MetricsCalculator _calculator;

    public FeatureEnvyDetector()
        : this(new FeatureEnvyThresholds(), new MetricsCalculator())
    {
    }

    public FeatureEnvyDetector(FeatureEnvyThresholds thresholds, MetricsCalculator calculator)
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
                    continue;
                }

                var symbol = model.GetDeclaredSymbol(method);
                if (symbol is null)
                {
                    continue;
                }

                var owner = symbol.ContainingType;
                var counts = _calculator.CalculateAccessCounts(method, model, owner);

                var total = counts.Values.Sum();
                var foreign = counts
                    .Where(pair => !SourceSymbols.Same(pair.Key, owner.OriginalDefinition))
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key.Name)
                    .FirstOrDefault();

                var hasForeign = foreign.Key is not null;
                var envyCount = hasForeign ? foreign.Value : 0;
                var share = total > 0 ? (double)envyCount / total : 0.0;
                var target = foreign.Key?.Name ?? "—";

                var conditions = new List<ConditionResult>
                {
                    new("E1", $"Частка звернень до одного стороннього класу ≥ {Format(_thresholds.Share)}",
                        hasForeign && share >= _thresholds.Share,
                        hasForeign
                            ? $"Клас {target}: {envyCount} з {total} звернень, частка {Format(share)}."
                            : "Звернень до сторонніх класів аналізованого коду немає."),
                    new("E2", $"Кількість звернень до цього класу ≥ {_thresholds.MinAccess}",
                        hasForeign && envyCount >= _thresholds.MinAccess,
                        hasForeign
                            ? $"Клас {target}: {envyCount} звернень, поріг {_thresholds.MinAccess}."
                            : "Звернень до сторонніх класів аналізованого коду немає."),
                };

                var span = tree.GetLineSpan(method.Span);
                yield return new DetectionResult(
                    Name, symbol.ToDisplayString(), span.Path, span.StartLinePosition.Line + 1, conditions);
            }
        }
    }

    private static string Format(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
