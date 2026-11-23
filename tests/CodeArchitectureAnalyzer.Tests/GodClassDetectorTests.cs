using CodeArchitectureAnalyzer.Core;
using CodeArchitectureAnalyzer.Core.Detectors;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class GodClassDetectorTests
{
    // Знижені пороги, щоб перевіряти правило на невеликих прикладах.
    private static readonly GodClassThresholds Small = new(Loc: 10, Nom: 5, Nof: 3, Cbo: 2, Lcom: 0.7);

    private static IReadOnlyList<DetectionResult> Detect(string code, GodClassThresholds? thresholds = null) =>
        TestHelper.DetectWith(
            thresholds is null ? new GodClassDetector() : new GodClassDetector(thresholds, new MetricsCalculator()),
            code);

    [Fact]
    public void Large_coupled_and_incohesive_class_is_detected()
    {
        var result = TestHelper.Find(Detect(ClassSamples.Big, Small), "Big");

        Assert.True(result.IsMatch, TestHelper.Describe(result));
        Assert.Equal(3, result.Conditions.Count);
    }

    [Fact]
    public void Default_thresholds_do_not_flag_a_small_class()
    {
        var result = TestHelper.Find(Detect(ClassSamples.Big), "Big");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "G1", "G2" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Cohesive_class_is_rejected_by_lcom_only()
    {
        var thresholds = new GodClassThresholds(Loc: 5, Nom: 3, Nof: 2, Cbo: 0, Lcom: 0.7);

        var result = TestHelper.Find(Detect(ClassSamples.Cohesive, thresholds), "Cohesive");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "G3" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Undefined_lcom_is_reported_and_class_is_not_a_candidate()
    {
        var thresholds = new GodClassThresholds(Loc: 1, Nom: 1, Nof: 0, Cbo: 0, Lcom: 0.7);

        var result = TestHelper.Find(Detect(ClassSamples.Stateless, thresholds), "Stateless");

        var g3 = result.Conditions.Single(c => c.Id == "G3");
        Assert.False(g3.Passed);
        Assert.Contains("не визначено", g3.Explanation);
        Assert.False(result.IsMatch);
    }
}
