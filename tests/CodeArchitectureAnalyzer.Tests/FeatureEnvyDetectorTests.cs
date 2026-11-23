using CodeArchitectureAnalyzer.Core;
using CodeArchitectureAnalyzer.Core.Detectors;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class FeatureEnvyDetectorTests
{
    private static IReadOnlyList<DetectionResult> Detect(string code, FeatureEnvyThresholds? thresholds = null) =>
        TestHelper.DetectWith(
            thresholds is null ? new FeatureEnvyDetector() : new FeatureEnvyDetector(thresholds, new MetricsCalculator()),
            code);

    [Fact]
    public void Method_that_uses_only_foreign_data_is_detected()
    {
        var result = TestHelper.Find(Detect(ClassSamples.Envious), "Report.Compute(Account)");

        Assert.True(result.IsMatch, TestHelper.Describe(result));
        Assert.Contains("Account", result.Conditions[0].Explanation);
    }

    [Fact]
    public void Too_few_foreign_accesses_are_rejected_by_count_only()
    {
        var result = TestHelper.Find(Detect(ClassSamples.BelowMinimumCount), "Order.Total(Account)");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "E2" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Own_members_dominating_the_method_are_rejected_by_share_only()
    {
        var result = TestHelper.Find(Detect(ClassSamples.OwnMembersDominate), "Mixed.Mix(Account)");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "E1" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Method_without_foreign_accesses_fails_both_conditions()
    {
        var result = TestHelper.Find(Detect(ClassSamples.NoForeignAccess), "Simple.Plain(int)");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "E1", "E2" }, TestHelper.FailedIds(result));
        Assert.Contains("Звернень до сторонніх", result.Conditions[0].Explanation);
    }

    [Fact]
    public void Thresholds_can_be_changed()
    {
        var relaxed = new FeatureEnvyThresholds(Share: 0.65, MinAccess: 4);

        var result = TestHelper.Find(Detect(ClassSamples.BelowMinimumCount, relaxed), "Order.Total(Account)");

        Assert.True(result.IsMatch, TestHelper.Describe(result));
    }
}
