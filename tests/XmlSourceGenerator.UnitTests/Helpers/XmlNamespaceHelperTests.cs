using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XmlSourceGenerator.Abstractions;
using XmlSourceGenerator.Helpers;

namespace XmlSourceGenerator.UnitTests.Helpers;

public class XmlNamespaceHelperTests
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
    public void GetNamespace_ReturnsNullForTypeWithoutAttribute()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class SimpleClass { }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.SimpleClass");

        // Act
        var ns = XmlNamespaceHelper.GetNamespace(typeSymbol!);

        // Assert
        ns.Should().BeNull();
    }

    [Fact]
    public void GetNamespace_ReturnsNamespaceFromXmlRootAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                [XmlRoot(Namespace = ""http://example.com"")]
                public class RootClass { }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.RootClass");

        // Act
        var ns = XmlNamespaceHelper.GetNamespace(typeSymbol!);

        // Assert
        ns.Should().Be("http://example.com");
    }

    [Fact]
    public void GetNamespace_ReturnsNamespaceFromXmlTypeAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                [XmlType(""CustomType"", Namespace = ""http://type.example.com"")]
                public class TypeClass { }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.TypeClass");
        var ns = XmlNamespaceHelper.GetNamespace(typeSymbol!);

        // Assert
        ns.Should().Be("http://type.example.com");
    }

    [Fact]
    public void GetNamespace_ReturnsNamespaceFromXmlElementAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class Container
                {
                    [XmlElement(Namespace = ""http://element.example.com"")]
                    public string Value { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.Container");
        var property = typeSymbol!.GetMembers("Value").First() as IPropertySymbol;

        // Act
        var ns = XmlNamespaceHelper.GetNamespace(property!);

        // Assert
        ns.Should().Be("http://element.example.com");
    }

    [Fact]
    public void GetNamespace_ReturnsNamespaceFromXmlAttributeAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class Container
                {
                    [XmlAttribute(Namespace = ""http://attr.example.com"")]
                    public string Id { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.Container");
        var property = typeSymbol!.GetMembers("Id").First() as IPropertySymbol;

        // Act
        var ns = XmlNamespaceHelper.GetNamespace(property!);

        // Assert
        ns.Should().Be("http://attr.example.com");
    }

    [Fact]
    public void GetNamespace_ReturnsNamespaceFromXmlArrayAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class Container
                {
                    [XmlArray(Namespace = ""http://array.example.com"")]
                    public string[] Items { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.Container");
        var property = typeSymbol!.GetMembers("Items").First() as IPropertySymbol;

        // Act
        var ns = XmlNamespaceHelper.GetNamespace(property!);

        // Assert
        ns.Should().Be("http://array.example.com");
    }
}
