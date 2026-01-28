using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XmlSourceGenerator.Abstractions;
using XmlSourceGenerator.Helpers;
using XmlSourceGenerator.Models;

namespace XmlSourceGenerator.UnitTests.Helpers;

public class PropertyAnalyzerTests
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
        
        // Add System.Xml.Serialization
        var xmlPath = typeof(System.Xml.Serialization.XmlAttributeAttribute).Assembly.Location;
        coreAssemblies.Add(MetadataReference.CreateFromFile(xmlPath));

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree },
            coreAssemblies,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlAttributeAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlAttribute]
                    public string Id { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.SerializeAsAttribute.Should().BeTrue();
        info.AttributeName.Should().BeNull();
    }

    [Fact]
    public void AnalyzeProperty_DetectsCustomAttributeName()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlAttribute(""custom"")]
                    public string Id { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.SerializeAsAttribute.Should().BeTrue();
        info.AttributeName.Should().Be("custom");
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlTextAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlText]
                    public string Content { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.SerializeAsInnerText.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlIgnoreAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlIgnore]
                    public string Hidden { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.IsIgnored.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlElementAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlElement(""CustomName"")]
                    public string Value { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.XmlElementName.Should().Be("CustomName");
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlArrayAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            using System.Collections.Generic;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlArray(""Items"")]
                    [XmlArrayItem(""Item"")]
                    public List<string> Collection { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.ArrayElementName.Should().Be("Items");
        info.ArrayItemElementName.Should().Be("Item");
        info.TypeInfo.Kind.Should().Be(PropertyKind.Collection);
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlAnyElementAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            using System.Xml.Linq;
            using System.Collections.Generic;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlSourceGenerator.Abstractions.XmlAnyElement]
                    public List<XElement> Any { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.IsAnyElement.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeProperty_DetectsXmlAnyAttributeAttribute()
    {
        // Arrange
        var source = @"
            using System.Xml.Serialization;
            using System.Xml.Linq;
            using System.Collections.Generic;
            
            namespace Test
            {
                public class MyClass
                {
                    [XmlSourceGenerator.Abstractions.XmlAnyAttribute]
                    public List<XAttribute> Attrs { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.IsAnyAttribute.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeProperty_DetectsPrimitivePropertyKind()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class MyClass
                {
                    public int Number { get; set; }
                    public string Text { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var properties = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray();

        // Act
        var intInfo = PropertyAnalyzer.AnalyzeMember(properties[0]);
        var stringInfo = PropertyAnalyzer.AnalyzeMember(properties[1]);

        // Assert
        intInfo.TypeInfo.Kind.Should().Be(PropertyKind.Primitive);
        stringInfo.TypeInfo.Kind.Should().Be(PropertyKind.Primitive);
    }

    [Fact]
    public void AnalyzeProperty_DetectsEnumPropertyKind()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public enum Status { Active, Inactive }
                
                public class MyClass
                {
                    public Status State { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.TypeInfo.Kind.Should().Be(PropertyKind.Enum);
    }

    [Fact]
    public void AnalyzeProperty_DetectsDateTimePropertyKind()
    {
        // Arrange
        var source = @"
            using System;
            
            namespace Test
            {
                public class MyClass
                {
                    public DateTime Created { get; set; }
                    public DateTimeOffset Modified { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var properties = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray();

        // Act
        var datetimeInfo = PropertyAnalyzer.AnalyzeMember(properties[0]);
        var offsetInfo = PropertyAnalyzer.AnalyzeMember(properties[1]);

        // Assert
        datetimeInfo.TypeInfo.Kind.Should().Be(PropertyKind.DateTime);
        offsetInfo.TypeInfo.Kind.Should().Be(PropertyKind.DateTime);
    }

    [Fact]
    public void AnalyzeProperty_DetectsComplexObjectPropertyKind()
    {
        // Arrange
        var source = @"
            namespace Test
            {
                public class Address { }
                
                public class MyClass
                {
                    public Address Location { get; set; }
                }
            }";

        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName("Test.MyClass");
        var property = PropertyHelpers.GetAllMembers(typeSymbol!).ToArray()[0];

        // Act
        var info = PropertyAnalyzer.AnalyzeMember(property);

        // Assert
        info.TypeInfo.Kind.Should().Be(PropertyKind.ComplexObject);
    }
}
