using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CodeArchitectureAnalyzer.Core.Detectors;

/// <summary>Порогові значення правила Клас-бог (формула 2.18). Значення за замовчуванням є робочими.</summary>
public sealed record GodClassThresholds(
    int Loc = 400,
    int Nom = 20,
    int Nof = 15,
    int Cbo = 15,
    double Lcom = 0.7,
    int SizeCriteriaRequired = 2);

/// <summary>
/// Детектор антипатерну Клас-бог: розмір (щонайменше два з трьох критеріїв LOC, NOM, NOF),
/// зв’язність CBO та низька згуртованість LCOM одночасно. Кожна група умов пояснюється окремо.
/// </summary>
public sealed class GodClassDetector : IPatternDetector
{
    private const string Name = "God Class";

    private readonly GodClassThresholds _thresholds;
    private readonly MetricsCalculator _calculator;

    public GodClassDetector()
        : this(new GodClassThresholds(), new MetricsCalculator())
    {
    }

    public GodClassDetector(GodClassThresholds thresholds, MetricsCalculator calculator)
    {
        _thresholds = thresholds;
        _calculator = calculator;
    }

    public string PatternName => Name;

    public IEnumerable<DetectionResult> Detect(Compilation compilation)
    {
        foreach (var type in SourceSymbols.AllTypes(compilation).Where(x => x.TypeKind == TypeKind.Class))
        {
            var metrics = _calculator.CalculateClassMetrics(compilation, type);
            var t = _thresholds;

            var satisfied = (metrics.Loc >= t.Loc ? 1 : 0) + (metrics.Nom >= t.Nom ? 1 : 0) + (metrics.Nof >= t.Nof ? 1 : 0);

            var conditions = new List<ConditionResult>
            {
                new("G1", $"Розмір: виконано щонайменше {t.SizeCriteriaRequired} з трьох критеріїв (LOC ≥ {t.Loc}, NOM ≥ {t.Nom}, NOF ≥ {t.Nof})",
                    satisfied >= t.SizeCriteriaRequired,
                    $"LOC = {metrics.Loc}, NOM = {metrics.Nom}, NOF = {metrics.Nof}; виконано {satisfied} з 3."),
                new("G2", $"Зв’язність CBO ≥ {t.Cbo}",
                    metrics.Cbo >= t.Cbo,
                    $"CBO = {metrics.Cbo}, поріг {t.Cbo}."),
                new("G3", $"Низька згуртованість LCOM ≥ {Format(t.Lcom)}",
                    metrics.Lcom is { } lcom && lcom >= t.Lcom,
                    metrics.Lcom is { } value
                        ? $"LCOM = {Format(value)}, поріг {Format(t.Lcom)}."
                        : "LCOM не визначено (NOM ≤ 1 або NOF = 0): клас не розглядається як кандидат."),
            };

            yield return new DetectionResult(Name, metrics.TypeName, metrics.FilePath, metrics.Line, conditions);
        }
    }

    private static string Format(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
