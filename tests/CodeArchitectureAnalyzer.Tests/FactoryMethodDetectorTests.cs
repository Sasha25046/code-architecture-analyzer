using CodeArchitectureAnalyzer.Core.Detectors;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class FactoryMethodDetectorTests
{
    private static IReadOnlyList<CodeArchitectureAnalyzer.Core.DetectionResult> Detect(string code) =>
        TestHelper.DetectWith(new FactoryMethodDetector(), code);

    [Theory]
    [InlineData(FactoryMethodSamples.FormAAbstract, "Creator.Create()")]
    [InlineData(FactoryMethodSamples.FormBInterface, "IFactory.Create()")]
    [InlineData(FactoryMethodSamples.VirtualWithLocalVariable, "Creator.Create()")]
    [InlineData(FactoryMethodSamples.TargetTypedNew, "Creator.Create()")]
    public void Supported_forms_are_detected(string code, string methodName)
    {
        var result = TestHelper.Find(Detect(code), methodName);

        Assert.True(result.IsMatch, TestHelper.Describe(result));
    }

    [Fact]
    public void Report_lists_creator_classes_and_created_types()
    {
        var result = TestHelper.Find(Detect(FactoryMethodSamples.FormAAbstract), "Creator.Create()");

        var f3 = result.Conditions.Single(c => c.Id == "F3");
        var f4 = result.Conditions.Single(c => c.Id == "F4");

        Assert.Contains("класів-створювачів: 2", f3.Explanation);
        Assert.Contains("ConcreteA", f4.Explanation);
        Assert.Contains("ConcreteB", f4.Explanation);
    }

    [Theory]
    [InlineData(FactoryMethodSamples.StoredObject)]
    [InlineData(FactoryMethodSamples.IndirectCreation)]
    public void Method_that_does_not_create_object_is_rejected_by_f4_only(string code)
    {
        var result = TestHelper.Find(Detect(code), "Creator.Create()");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "F4" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Method_without_implementations_is_rejected()
    {
        var result = TestHelper.Find(Detect(FactoryMethodSamples.NoImplementations), "Creator.Create()");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "F3", "F4" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Non_polymorphic_method_returning_abstract_type_is_rejected()
    {
        var result = TestHelper.Find(Detect(FactoryMethodSamples.NonPolymorphic), "SimpleCreator.Create()");

        Assert.False(result.IsMatch);
        Assert.Contains("F1", TestHelper.FailedIds(result));
    }

    [Fact]
    public void Static_factory_method_is_not_a_candidate()
    {
        var results = Detect(FactoryMethodSamples.StaticFactory);

        Assert.DoesNotContain(results, r => r.TypeName == "Factory.Create()");
    }
}
