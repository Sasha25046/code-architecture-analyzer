using CodeArchitectureAnalyzer.Core;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class MetricsCalculatorTests
{
    private const string SimpleMethod = """
public class Calc
{
    public int Simple(int a, int b)
    {
        return a + b;
    }
}
""";

    private const string Branches = """
public class Calc
{
    public int Branches(int x, bool flag)
    {
        var result = 0;
        if (x > 0 && flag)
        {
            result = 1;
        }
        else if (x < 0 || !flag)
        {
            result = 2;
        }
        for (var i = 0; i < x; i++)
        {
            result += i;
        }
        return x > 5 ? result : -result;
    }
}
""";

    private const string Nested = """
public class Calc
{
    public int Nested(int[] items, int mode)
    {
        var sum = 0;
        foreach (var item in items)
        {
            while (item > 0)
            {
                if (mode == 1)
                {
                    switch (item)
                    {
                        case 1:
                            sum += 1;
                            break;
                        case 2:
                            sum += 2;
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        return sum;
    }
}
""";

    private const string Fields = """
public class Counter
{
    private int _count;
    private int _step;
    private int _unused;

    public void Tick(int times)
    {
        for (var i = 0; i < times; i++)
        {
            _count += _step;
        }
    }
}
""";

    private static MethodMetrics Calculate(string code, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(code, path: "Metrics.cs");
        var compilation = CompilationFactory.Create(new[] { tree });
        var model = compilation.GetSemanticModel(tree);

        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == methodName);

        return new MetricsCalculator().CalculateMethodMetrics(method, model);
    }

    [Fact]
    public void Simple_method_has_minimal_metrics()
    {
        var metrics = Calculate(SimpleMethod, "Simple");

        Assert.Equal(4, metrics.Loc);
        Assert.Equal(1, metrics.CyclomaticComplexity);
        Assert.Equal(0, metrics.MaxNestingDepth);
        Assert.Equal(2, metrics.AccessedVariables);
        Assert.Equal(3, metrics.Line);
    }

    [Fact]
    public void Cyclomatic_complexity_counts_branches_loops_operators_and_conditionals()
    {
        var metrics = Calculate(Branches, "Branches");

        // if, &&, else-if, ||, for, ?: = 6 предикатів + 1
        Assert.Equal(7, metrics.CyclomaticComplexity);
        Assert.Equal(17, metrics.Loc);
        Assert.Equal(4, metrics.AccessedVariables);
    }

    [Fact]
    public void Else_if_does_not_increase_nesting_depth()
    {
        var metrics = Calculate(Branches, "Branches");

        Assert.Equal(1, metrics.MaxNestingDepth);
    }

    [Fact]
    public void Nesting_depth_and_case_labels_are_counted()
    {
        var metrics = Calculate(Nested, "Nested");

        // foreach > while > if > switch
        Assert.Equal(4, metrics.MaxNestingDepth);
        // foreach, while, if, case 1, case 2 = 5 предикатів + 1 (default не рахується)
        Assert.Equal(6, metrics.CyclomaticComplexity);
        Assert.Equal(4, metrics.AccessedVariables);
    }

    [Fact]
    public void Accessed_variables_include_fields_but_not_unused_members()
    {
        var metrics = Calculate(Fields, "Tick");

        // times, i, _count, _step
        Assert.Equal(4, metrics.AccessedVariables);
    }

    [Fact]
    public void File_metrics_contain_every_method_with_body()
    {
        var tree = CSharpSyntaxTree.ParseText(SimpleMethod + "\n" + Branches.Replace("class Calc", "class Other"), path: "Metrics.cs");
        var compilation = CompilationFactory.Create(new[] { tree });

        var metrics = new MetricsCalculator().CalculateMetrics(compilation, tree);

        Assert.Equal(new[] { "Simple", "Branches" }, metrics.Methods.Select(m => m.MethodName).ToArray());
    }
}
