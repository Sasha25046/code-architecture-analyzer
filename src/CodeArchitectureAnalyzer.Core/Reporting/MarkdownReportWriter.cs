using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeArchitectureAnalyzer.Core.Reporting;

public static class MarkdownReportWriter
{
    public static string Write(IReadOnlyList<DetectionResult> results, bool includeNonMatches = false)
    {
        var matches = results.Where(r => r.IsMatch).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# Звіт аналізатора шаблонів проєктування");
        sb.AppendLine();
        foreach (var group in results.GroupBy(r => r.PatternName))
        {
            sb.AppendLine($"- {group.Key}: перевірено {group.Count()}, виявлено {group.Count(r => r.IsMatch)}.");
        }
        sb.AppendLine();

        if (matches.Count > 0)
        {
            sb.AppendLine("| Шаблон | Тип | Розташування |");
            sb.AppendLine("|---|---|---|");
            foreach (var match in matches)
            {
                sb.AppendLine($"| {match.PatternName} | {match.TypeName} | `{match.FilePath}:{match.Line}` |");
            }
            sb.AppendLine();
        }

        foreach (var result in includeNonMatches ? results : matches)
        {
            var verdict = result.IsMatch ? "виявлено" : "не виявлено";
            sb.AppendLine($"## {result.PatternName}: {result.TypeName} ({verdict})");
            sb.AppendLine();
            sb.AppendLine($"Розташування: `{result.FilePath}:{result.Line}`");
            sb.AppendLine();
            foreach (var condition in result.Conditions)
            {
                var mark = condition.Passed ? "x" : " ";
                sb.AppendLine($"- [{mark}] {condition.Id}. {condition.Description}. {condition.Explanation}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
