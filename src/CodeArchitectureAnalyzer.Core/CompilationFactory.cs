using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeArchitectureAnalyzer.Core;

public static class CompilationFactory
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> BaseReferences = new(() =>
    {
        var platformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        return platformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    });

    public static CSharpCompilation Create(IEnumerable<SyntaxTree> trees) =>
        CSharpCompilation.Create(
            "AnalyzedProject",
            trees,
            BaseReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static IReadOnlyList<SyntaxTree> ParseFiles(string path)
    {
        IEnumerable<string> files;

        if (File.Exists(path))
        {
            files = new[] { path };
        }
        else if (Directory.Exists(path))
        {
            files = Directory
                .EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file));
        }
        else
        {
            throw new FileNotFoundException($"Шлях не знайдено: {path}");
        }

        return files
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToList();
    }

    private static bool IsBuildOutput(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin") || parts.Contains("obj");
    }
}
