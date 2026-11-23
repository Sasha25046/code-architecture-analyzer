using CodeArchitectureAnalyzer.Core;
using CodeArchitectureAnalyzer.Core.Detectors;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class StrategyDetectorTests
{
    private static IReadOnlyList<DetectionResult> Detect(string code) =>
        TestHelper.DetectWith(new StrategyDetector(), code);

    [Theory]
    [InlineData(StrategySamples.ConstructorInjection, "Order → IDiscount")]
    [InlineData(StrategySamples.NullChecksAndConditionalAccess, "SafeOrder → IDiscount")]
    [InlineData(StrategySamples.AbstractClassWithSetter, "Sorter → SortStrategy")]
    public void Supported_forms_are_detected(string code, string pairName)
    {
        var result = TestHelper.Find(Detect(code), pairName);

        Assert.True(result.IsMatch, TestHelper.Describe(result));
    }

    [Fact]
    public void Report_shows_mutator_and_implementations()
    {
        var result = TestHelper.Find(Detect(StrategySamples.AbstractClassWithSetter), "Sorter → SortStrategy");

        var s2 = result.Conditions.Single(c => c.Id == "S2");
        var s4 = result.Conditions.Single(c => c.Id == "S4");

        Assert.Contains("Реалізацій: 2", s2.Explanation);
        Assert.Contains("SetStrategy", s4.Explanation);
    }

    [Fact]
    public void Single_implementation_is_ordinary_dependency_injection()
    {
        var result = TestHelper.Find(Detect(StrategySamples.SingleImplementation), "Service → IRepository");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "S2" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Decorator_that_implements_the_contract_is_rejected()
    {
        var result = TestHelper.Find(Detect(StrategySamples.Decorator), "Decorator → IText");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "S2" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Context_that_never_calls_the_contract_is_rejected_by_s5_only()
    {
        var result = TestHelper.Find(Detect(StrategySamples.NoDelegation), "IdleOrder → IDiscount");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "S5" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Context_that_creates_its_own_strategy_is_rejected_by_s4_only()
    {
        var result = TestHelper.Find(Detect(StrategySamples.NoInjection), "FixedOrder → IDiscount");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "S4" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Public_field_does_not_satisfy_storage_condition()
    {
        var result = TestHelper.Find(Detect(StrategySamples.PublicField), "OpenOrder → IDiscount");

        Assert.False(result.IsMatch);
        Assert.Contains("S3", TestHelper.FailedIds(result));
    }

    [Fact]
    public void Contracts_from_libraries_are_not_candidates()
    {
        Assert.Empty(Detect(StrategySamples.LibraryContractOnly));
    }
}
