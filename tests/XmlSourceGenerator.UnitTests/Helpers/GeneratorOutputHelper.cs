using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using XmlSourceGenerator.Abstractions;

namespace XmlSourceGenerator.UnitTests.Helpers;

public static class GeneratorOutputHelper
{
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = new List<MetadataReference>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Ensure we have the abstraction assembly
        references.Add(MetadataReference.CreateFromFile(typeof(IXmlStreamable).Assembly.Location));

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static string CaptureGeneratedCode(string source, string classNameFilter = null)
    {
        var compilation = CreateCompilation(source);
        var generator = new XmlGenerator().AsSourceGenerator();
        
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Generator produced errors: {errors}");
        }

        if (result.GeneratedTrees.Length == 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(classNameFilter))
        {
            return result.GeneratedTrees[0].ToString();
        }

        var filtered = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains(classNameFilter));
        return filtered?.ToString() ?? throw new InvalidOperationException($"No generated file found for {classNameFilter}");
    }

    public static void VerifyGeneratedCode(string source, string expectedCode, string classNameFilter = null)
    {
        var actualCode = CaptureGeneratedCode(source, classNameFilter);
        
        // Normalize line endings
        expectedCode = expectedCode.Replace("\r\n", "\n").Trim();
        actualCode = actualCode.Replace("\r\n", "\n").Trim();

        actualCode.Should().Be(expectedCode);
    }

    public static void PrintGeneratedCode(string source, string title = "Generated Code")
    {
        try 
        {
            var code = CaptureGeneratedCode(source);
            Console.WriteLine($"\n========== {title} ==========");
            Console.WriteLine(code);
            Console.WriteLine("========================================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n========== {title} (Error) ==========");
            Console.WriteLine(ex.Message);
            Console.WriteLine("========================================\n");
        }
    }
}
