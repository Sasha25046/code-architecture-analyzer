using CodeArchitectureAnalyzer.Cli;
using CodeArchitectureAnalyzer.Core;
using CodeArchitectureAnalyzer.Core.Reporting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string? input = null;
string? markdownPath = null;
var showAll = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--md" when i + 1 < args.Length:
            markdownPath = args[++i];
            break;
        case "--all":
            showAll = true;
            break;
        default:
            input = args[i];
            break;
    }
}

IReadOnlyList<SyntaxTree> trees;
if (input is null)
{
    Console.WriteLine("Шлях не вказано: використовується вбудований приклад.");
    trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(SampleCode.Text, path: "Sample.cs") };
    showAll = true;
}
else
{
    try
    {
        trees = CompilationFactory.ParseFiles(input);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Помилка читання вихідного коду: {ex.Message}");
        return 1;
    }
}

static void PrintSummary(IReadOnlyList<DetectionResult> results)
{
    foreach (var group in results.GroupBy(r => r.PatternName))
    {
        Console.WriteLine($"{group.Key}: перевірено {group.Count()}, виявлено {group.Count(r => r.IsMatch)}.");
    }
}

var compilation = CompilationFactory.Create(trees);
var results = CodeAnalyzer.CreateDefault().Analyze(compilation);

if (markdownPath is not null)
{
    // Передано --md: звіт зберігається у файл.
    File.WriteAllText(markdownPath, MarkdownReportWriter.Write(results, showAll));
    PrintSummary(results);
    Console.WriteLine($"Звіт збережено: {markdownPath}");
}
else
{
    // Без --md: докладний вивід у консоль.
    Console.WriteLine("=== АНАЛІЗАТОР ШАБЛОНІВ ПРОЄКТУВАННЯ ===");
    Console.WriteLine($"Файлів: {trees.Count}\n");

    foreach (var result in results.Where(r => showAll || r.IsMatch))
    {
        var status = result.IsMatch ? "[ШАБЛОН ЗНАЙДЕНО]" : "[НІ]";
        Console.WriteLine($"{status} {result.PatternName}: {result.TypeName}");
        Console.WriteLine($"      {result.FilePath}:{result.Line}");
        foreach (var condition in result.Conditions)
        {
            var mark = condition.Passed ? "+" : "-";
            Console.WriteLine($"      [{mark}] {condition.Id}. {condition.Description}: {condition.Explanation}");
        }
        Console.WriteLine(new string('-', 60));
    }

    PrintSummary(results);
}

return 0;
