using System.Collections.Generic;
using System.Linq;
using CodeArchitectureAnalyzer.Core.Detectors;
using Microsoft.CodeAnalysis;

namespace CodeArchitectureAnalyzer.Core;

public sealed class CodeAnalyzer
{
    private readonly IReadOnlyList<IPatternDetector> _detectors;

    public CodeAnalyzer(IEnumerable<IPatternDetector> detectors) => _detectors = detectors.ToList();

    public static CodeAnalyzer CreateDefault() =>
        new(new IPatternDetector[] { new SingletonDetector() });

    public IReadOnlyList<DetectionResult> Analyze(Compilation compilation) =>
        _detectors.SelectMany(detector => detector.Detect(compilation)).ToList();
}
