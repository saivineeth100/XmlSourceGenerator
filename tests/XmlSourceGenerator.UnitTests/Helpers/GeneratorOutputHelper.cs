using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;
using XmlSourceGenerator.Abstractions;

namespace XmlSourceGenerator.UnitTests.Helpers;

/// <summary>
/// Helper to capture actual generator output for creating snapshot baselines.
/// </summary>
public static class GeneratorOutputHelper
{
    public static string CaptureGeneratedCode(string source, string classNameFilter = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(IXmlStreamable).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Xml.Linq.XElement).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Xml.Serialization.XmlAttributeAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new XmlGenerator().AsSourceGenerator();
        
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        if (string.IsNullOrEmpty(classNameFilter))
        {
            return result.GeneratedTrees[0].ToString();
        }

        var filtered = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains(classNameFilter));
        return filtered?.ToString() ?? throw new InvalidOperationException($"No generated file found for {classNameFilter}");
    }

    public static void PrintGeneratedCode(string source, string title = "Generated Code")
    {
        var code = CaptureGeneratedCode(source);
        Console.WriteLine($"\n========== {title} ==========");
        Console.WriteLine(code);
        Console.WriteLine("========================================\n");
    }
}
