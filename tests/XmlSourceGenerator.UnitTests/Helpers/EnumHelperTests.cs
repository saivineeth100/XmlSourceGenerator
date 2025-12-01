using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XmlSourceGenerator.Abstractions;
using XmlSourceGenerator.Helpers;

namespace XmlSourceGenerator.UnitTests.Helpers;

public class EnumHelperTests
{
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        // Get all assemblies including XmlSourceGenerator.Abstractions
        var coreAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        // Explicitly add the Abstractions assembly
        var abstractionsPath = typeof(IXmlStreamable).Assembly.Location;
        coreAssemblies.Add(MetadataReference.CreateFromFile(abstractionsPath));

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            coreAssemblies,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void GetEnumMap_ReturnsDefaultMappingForStandardEnum()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public enum Status
                {
                    Active,
                    Inactive
                }
            }";

        var compilation = CreateCompilation(source);
        var enumType = compilation.GetTypeByMetadataName("Test.Status");

        // Act
        var map = EnumHelper.GetEnumMap(enumType!);

        // Assert
        map.Should().ContainKey("Active").WhoseValue.Should().Be("Active");
        map.Should().ContainKey("Inactive").WhoseValue.Should().Be("Inactive");
    }

    [Fact]
    public void GetEnumMap_ReturnsCustomMappingForXmlEnumAttribute()
    {
        // Arrange
        var source = @"
            using XmlSourceGenerator.Abstractions;
            
            namespace Test
            {
                public enum Priority
                {
                    [XmlEnum(""low"")]
                    Low,
                    [XmlEnum(""high"")]
                    High,
                    Medium
                }
            }";

        var compilation = CreateCompilation(source);
        var enumType = compilation.GetTypeByMetadataName("Test.Priority");

        // Act
        var map = EnumHelper.GetEnumMap(enumType!);

        // Assert
        map.Should().ContainKey("Low").WhoseValue.Should().Be("low");
        map.Should().ContainKey("High").WhoseValue.Should().Be("high");
        map.Should().ContainKey("Medium").WhoseValue.Should().Be("Medium"); // No attribute
    }

    [Fact]
    public void GetReverseEnumMap_ReturnsReversedMapping()
    {
        // Arrange
        var source = @"
            using XmlSourceGenerator.Abstractions;
            
            namespace Test
            {
                public enum Priority
                {
                    [XmlEnum(""low"")]
                    Low,
                    [XmlEnum(""high"")]
                    High
                }
            }";

        var compilation = CreateCompilation(source);
        var enumType = compilation.GetTypeByMetadataName("Test.Priority");

        // Act
        var reverseMap = EnumHelper.GetReverseEnumMap(enumType!);

        // Assert
        reverseMap.Should().ContainKey("low").WhoseValue.Should().Be("Low");
        reverseMap.Should().ContainKey("high").WhoseValue.Should().Be("High");
    }
}
