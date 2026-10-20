using System.Collections.Generic;
using System.Linq;

namespace CodeArchitectureAnalyzer.Core;

public sealed record ConditionResult(string Id, string Description, bool Passed, string Explanation);

public sealed record DetectionResult(
    string PatternName,
    string TypeName,
    string FilePath,
    int Line,
    IReadOnlyList<ConditionResult> Conditions)
{
    public bool IsMatch => Conditions.All(c => c.Passed);
}
