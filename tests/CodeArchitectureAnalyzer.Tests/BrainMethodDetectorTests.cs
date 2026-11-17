using CodeArchitectureAnalyzer.Core;
using CodeArchitectureAnalyzer.Core.Detectors;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class BrainMethodDetectorTests
{
    private const string Brain = """
public class Processor
{
    public int Process(int a, int b, int c, int d, int e)
    {
        var total = 0;
        var flag = false;
        for (var i = 0; i < a; i++)
        {
            if (i % 2 == 0)
            {
                foreach (var j in new[] { b, c, d })
                {
                    while (j > e)
                    {
                        if (total > 100 && !flag)
                        {
                            total -= j;
                        }
                        else if (total < 0 || flag)
                        {
                            total += j;
                        }
                        else
                        {
                            flag = true;
                        }
                    }
                }
            }
            else
            {
                switch (i)
                {
                    case 1:
                        total += 1;
                        break;
                    case 3:
                        total += 3;
                        break;
                    case 5:
                        total += 5;
                        break;
                    default:
                        total += 7;
                        break;
                }
            }
        }
        if (flag)
        {
            total++;
        }
        return total > 10 ? total : -total;
    }
}
""";

    // Складний і глибоко вкладений, але короткий метод (менше 50 рядків).
    private const string ComplexButShort = """
public class Small
{
    public int M(int a, int b, int c, int d, int e, int f, int g, int h, int i)
    {
        if (a > 0 && b > 0 && c > 0 && d > 0 && e > 0 && f > 0 && g > 0 && h > 0 && i > 0)
        {
            while (a > 0)
            {
                foreach (var x in new[] { b })
                {
                    if (c > d)
                    {
                        return e + f + g + h + x;
                    }
                }
            }
        }
        return 0;
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

    private const string AbstractMethod = """
public abstract class A
{
    public abstract int M();
}
""";

    // Довгий (60+ рядків), але лінійний метод.
    private static string LongButSimple() =>
        "public class L\n{\n    public int M()\n    {\n        var x = 0;\n"
        + string.Concat(Enumerable.Repeat("        x++;\n", 60))
        + "        return x;\n    }\n}\n";

    private static IReadOnlyList<DetectionResult> Detect(string code, IPatternDetector? detector = null) =>
        TestHelper.DetectWith(detector ?? new BrainMethodDetector(), code);

    [Fact]
    public void Large_complex_deeply_nested_method_with_many_variables_is_detected()
    {
        var result = Assert.Single(Detect(Brain));

        Assert.True(result.IsMatch, TestHelper.Describe(result));
        Assert.StartsWith("Processor.Process", result.TypeName);
        Assert.Equal(4, result.Conditions.Count);
    }

    [Fact]
    public void Long_but_simple_method_is_not_a_brain_method()
    {
        var result = Assert.Single(Detect(LongButSimple()));

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "B1", "B3", "B4" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Short_method_is_rejected_by_loc_only()
    {
        var result = Assert.Single(Detect(ComplexButShort));

        Assert.False(result.IsMatch);
        Assert.Equal(new[] { "B2" }, TestHelper.FailedIds(result));
    }

    [Fact]
    public void Thresholds_can_be_changed()
    {
        var strict = new BrainMethodDetector();
        var relaxed = new BrainMethodDetector(new BrainMethodThresholds(5, 10, 1, 3), new MetricsCalculator());

        Assert.False(Assert.Single(Detect(Branches, strict)).IsMatch);
        Assert.True(Assert.Single(Detect(Branches, relaxed)).IsMatch);
    }

    [Fact]
    public void Methods_without_body_are_skipped()
    {
        Assert.Empty(Detect(AbstractMethod));
    }
}
