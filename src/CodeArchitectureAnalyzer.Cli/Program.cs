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

var compilation = CompilationFactory.Create(trees);
var results = CodeAnalyzer.CreateDefault().Analyze(compilation);

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

var found = results.Count(r => r.IsMatch);
Console.WriteLine($"Перевірено типів: {results.Count}. Виявлено шаблонів: {found}.");

if (markdownPath is not null)
{
    File.WriteAllText(markdownPath, MarkdownReportWriter.Write(results, showAll));
    Console.WriteLine($"Звіт збережено: {markdownPath}");
}

return 0;
