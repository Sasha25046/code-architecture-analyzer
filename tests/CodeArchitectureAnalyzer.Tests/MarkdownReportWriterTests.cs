using CodeArchitectureAnalyzer.Core.Reporting;
using Xunit;

namespace CodeArchitectureAnalyzer.Tests;

public class MarkdownReportWriterTests
{
    [Fact]
    public void Report_lists_found_pattern_with_location()
    {
        var results = TestHelper.Detect(Samples.ClassicProperty);

        var markdown = MarkdownReportWriter.Write(results);

        Assert.Contains("Singleton", markdown);
        Assert.Contains("Foo", markdown);
        Assert.Contains("Test0.cs:1", markdown);
    }

    [Fact]
    public void Report_hides_non_matches_unless_requested()
    {
        var results = TestHelper.Detect(Samples.PublicConstructor);

        Assert.DoesNotContain("## Singleton", MarkdownReportWriter.Write(results));
        Assert.Contains("## Singleton: Foo (не виявлено)", MarkdownReportWriter.Write(results, includeNonMatches: true));
    }
}
