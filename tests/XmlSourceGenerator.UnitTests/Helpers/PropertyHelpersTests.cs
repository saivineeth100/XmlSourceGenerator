using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XmlSourceGenerator.Abstractions;
using XmlSourceGenerator.Helpers;

namespace XmlSourceGenerator.UnitTests.Helpers;

public class PropertyHelpersTests
{
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var coreAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var abstractionsPath = typeof(IXmlStreamable).Assembly.Location;
        coreAssemblies.Add(MetadataReference.CreateFromFile(abstractionsPath));

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            coreAssemblies,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void GetAllProperties_ReturnsAllPublicInstanceProperties()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class MyClass
                {
                    public string PublicProp { get; set; }
                    private string PrivateProp { get; set; }
                    public static string StaticProp { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");

        // Act
        var properties = PropertyHelpers.GetAllProperties(typeSymbol!).ToArray();

        // Assert
        properties.Should().HaveCount(1);
        properties[0].Name.Should().Be("PublicProp");
    }

    [Fact]
    public void GetAllProperties_IncludesInheritedProperties()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class BaseClass
                {
                    public string BaseProp { get; set; }
                }
                
                public class DerivedClass : BaseClass
                {
                    public string DerivedProp { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.DerivedClass");

        // Act
        var properties = PropertyHelpers.GetAllProperties(typeSymbol!);

        // Assert
        properties.Should().HaveCount(2);
        properties.Select(p => p.Name).Should().Contain(new[] { "BaseProp", "DerivedProp" });
    }

    [Fact]
    public void IsPrimitive_ReturnsTrueForPrimitiveTypes()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class MyClass
                {
                    public int IntProp { get; set; }
                    public string StringProp { get; set; }
                    public bool BoolProp { get; set; }
                    public double DoubleProp { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var properties = PropertyHelpers.GetAllProperties(typeSymbol!);

        // Act & Assert
        foreach (var prop in properties)
        {
            PropertyHelpers.IsPrimitive(prop.Type).Should().BeTrue($"{prop.Type} should be primitive");
        }
    }

    [Fact]
    public void IsPrimitive_ReturnsFalseForComplexTypes()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class ComplexType { }
                
                public class MyClass
                {
                    public ComplexType Complex { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var properties = PropertyHelpers.GetAllProperties(typeSymbol!).ToArray();

        // Act
        var isPrimitive = PropertyHelpers.IsPrimitive(properties[0].Type);

        // Assert
        isPrimitive.Should().BeFalse();
    }

    [Fact]
    public void IsPrimitive_ReturnsTrueForNullableTypes()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class MyClass
                {
                    public int? NullableInt { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var properties = PropertyHelpers.GetAllProperties(typeSymbol!).ToArray();

        // Act
        var isPrimitive = PropertyHelpers.IsPrimitive(properties[0].Type);

        // Assert
        isPrimitive.Should().BeTrue();
    }
}
