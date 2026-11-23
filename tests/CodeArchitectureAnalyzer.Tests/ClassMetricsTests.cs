using CodeArchitectureAnalyzer.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class ClassMetricsTests
{
    private static ClassMetrics Calculate(string code, string typeName)
    {
        var compilation = TestHelper.Compile(code);
        var type = compilation.GetTypeByMetadataName(typeName)!;
        return new MetricsCalculator().CalculateClassMetrics(compilation, type);
    }

    private static IReadOnlyDictionary<INamedTypeSymbol, int> AccessCounts(string code, string methodName, out CSharpCompilation compilation)
    {
        var tree = CSharpSyntaxTree.ParseText(code, path: "Access.cs");
        compilation = CompilationFactory.Create(new[] { tree });
        var model = compilation.GetSemanticModel(tree);

        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == methodName);
        var owner = model.GetDeclaredSymbol(method)!.ContainingType;

        return new MetricsCalculator().CalculateAccessCounts(method, model, owner);
    }

    [Fact]
    public void Class_metrics_are_calculated_for_a_large_class()
    {
        var metrics = Calculate(ClassSamples.Big, "Big");

        Assert.Equal(12, metrics.Loc);
        Assert.Equal(5, metrics.Nom);
        Assert.Equal(3, metrics.Nof);
        Assert.Equal(2, metrics.Cbo);
        Assert.NotNull(metrics.Lcom);
        // (5 − 3/3) / (5 − 1) = 1
        Assert.Equal(1.0, metrics.Lcom!.Value, 3);
    }

    [Fact]
    public void Lcom_is_zero_when_all_methods_use_all_fields()
    {
        var metrics = Calculate(ClassSamples.Cohesive, "Cohesive");

        Assert.Equal(3, metrics.Nom);
        Assert.Equal(2, metrics.Nof);
        Assert.Equal(0.0, metrics.Lcom!.Value, 3);
    }

    [Fact]
    public void Lcom_is_undefined_without_fields()
    {
        var metrics = Calculate(ClassSamples.Stateless, "Stateless");

        Assert.Null(metrics.Lcom);
    }

    [Fact]
    public void Access_counts_group_accesses_by_class()
    {
        var counts = AccessCounts(ClassSamples.Envious, "Compute", out var compilation);

        var account = compilation.GetTypeByMetadataName("Account")!;
        Assert.Equal(5, counts[account]);
        Assert.Single(counts);
    }

    [Fact]
    public void Members_inherited_from_base_classes_count_as_own()
    {
        var counts = AccessCounts(ClassSamples.InheritedMembers, "M", out var compilation);

        Assert.Equal(5, counts[compilation.GetTypeByMetadataName("Derived")!]);
        Assert.Equal(1, counts[compilation.GetTypeByMetadataName("Account")!]);
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void Library_types_are_not_counted()
    {
        var counts = AccessCounts(ClassSamples.LibraryAccess, "M", out var compilation);

        Assert.Equal(1, counts[compilation.GetTypeByMetadataName("Account")!]);
        Assert.Single(counts);
    }
}
