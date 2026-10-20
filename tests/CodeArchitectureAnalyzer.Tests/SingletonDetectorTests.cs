using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class SingletonDetectorTests
{
    [Theory]
    [InlineData(Samples.ClassicProperty)]
    [InlineData(Samples.StaticMethod)]
    [InlineData(Samples.LazyProperty)]
    [InlineData(Samples.Locked)]
    [InlineData(Samples.AutoProperty)]
    [InlineData(Samples.PublicField)]
    [InlineData(Samples.NullableAnnotated)]
    public void Singleton_variants_are_detected(string code)
    {
        var result = TestHelper.Find(TestHelper.Detect(code), "Foo");

        Assert.True(result.IsMatch, TestHelper.Describe(result));
    }

    [Fact]
    public void Partial_class_is_reported_once_and_detected()
    {
        var results = TestHelper.Detect(Samples.PartialFirst, Samples.PartialSecond);

        var result = Assert.Single(results);
        Assert.True(result.IsMatch, TestHelper.Describe(result));
    }

    [Theory]
    [InlineData(Samples.PublicConstructor, "C1")]
    [InlineData(Samples.ImplicitConstructor, "C1")]
    [InlineData(Samples.PrivateProtectedConstructor, "C1")]
    [InlineData(Samples.ProtectedConstructor, "C1")]
    public void Constructor_rule_rejects_non_private_constructors(string code, string expectedFailure)
    {
        var result = TestHelper.Find(TestHelper.Detect(code), "Foo");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { expectedFailure }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Static_class_is_rejected()
    {
        var result = TestHelper.Find(TestHelper.Detect(Samples.StaticClass), "Foo");

        Assert.False(result.IsMatch);
        Assert.Contains("C0", TestHelper.FailedIds(result));
    }

    [Fact]
    public void Missing_public_accessor_is_rejected()
    {
        var result = TestHelper.Find(TestHelper.Detect(Samples.NoPublicAccessor), "Foo");

        Assert.False(result.IsMatch);
        Assert.Contains("C3", TestHelper.FailedIds(result));
    }

    [Fact]
    public void Factory_that_ignores_storage_is_rejected_by_c4_only()
    {
        var result = TestHelper.Find(TestHelper.Detect(Samples.FactoryWithoutStorage), "Foo");

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "C4" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Missing_static_storage_is_rejected()
    {
        var result = TestHelper.Find(TestHelper.Detect(Samples.NoStaticStorage), "Foo");

        Assert.False(result.IsMatch);
        Assert.Contains("C2", TestHelper.FailedIds(result));
    }

    [Fact]
    public void Types_with_same_name_from_other_namespace_are_not_confused()
    {
        var results = TestHelper.Detect(Samples.SameNameInOtherNamespace);

        var one = TestHelper.Find(results, "One.Foo");

        Assert.False(one.IsMatch);
        Assert.Contains("C2", TestHelper.FailedIds(one));
    }

    [Fact]
    public void Result_contains_all_conditions_and_source_location()
    {
        var result = TestHelper.Find(TestHelper.Detect(Samples.ClassicProperty), "Foo");

        Assert.Equal(5, result.Conditions.Count);
        Assert.Equal("Test0.cs", result.FilePath);
        Assert.Equal(1, result.Line);
    }
}
