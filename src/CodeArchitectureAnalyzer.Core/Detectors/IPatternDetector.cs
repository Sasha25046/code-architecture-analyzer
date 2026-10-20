using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CodeArchitectureAnalyzer.Core.Detectors;

public interface IPatternDetector
{
    string PatternName { get; }

    IEnumerable<DetectionResult> Detect(Compilation compilation);
}
