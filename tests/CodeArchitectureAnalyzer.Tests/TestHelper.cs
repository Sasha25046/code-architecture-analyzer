using CodeArchitectureAnalyzer.Core;
using CodeArchitectureAnalyzer.Core.Detectors;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeArchitectureAnalyzer.Tests;

internal static class TestHelper
{
    public static IReadOnlyList<DetectionResult> Detect(params string[] sources)
    {
        var trees = sources
            .Select((code, index) => CSharpSyntaxTree.ParseText(code, path: $"Test{index}.cs"))
            .ToList();

        var compilation = CompilationFactory.Create(trees);
        return new SingletonDetector().Detect(compilation).ToList();
    }

    public static DetectionResult Find(IReadOnlyList<DetectionResult> results, string typeName) =>
        results.Single(r => r.TypeName == typeName);

    public static string[] FailedIds(DetectionResult result) =>
        result.Conditions.Where(c => !c.Passed).Select(c => c.Id).ToArray();

    public static string Describe(DetectionResult result) =>
        string.Join("; ", result.Conditions.Select(c => $"{c.Id}={(c.Passed ? "ok" : "fail")} ({c.Explanation})"));
}
