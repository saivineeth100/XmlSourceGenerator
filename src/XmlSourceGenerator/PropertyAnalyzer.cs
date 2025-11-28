using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Analyzes property metadata for code generation.
    /// Single Responsibility: Property analysis logic.
    /// </summary>
    internal class PropertyAnalyzer
    {
        public static PropertyInfo AnalyzeProperty(IPropertySymbol property, Compilation compilation)
        {
            var info = new PropertyInfo
            {
                Symbol = property,
                Name = property.Name,
                TypeName = property.Type.ToDisplayString(),
                Type = property.Type
            };

            // Check attributes
            foreach (var attr in property.GetAttributes())
            {
                var attrName = attr.AttributeClass?.Name;
                
                switch (attrName)
                {
                    case "XmlIgnoreAttribute":
                        info.IsIgnored = true;
                        break;
                        
                    case "XmlElementAttribute":
                        info.XmlElementName = (string)attr.ConstructorArguments[0].Value;
                        info.Namespace = attr.NamedArguments.FirstOrDefault(a => a.Key == "Namespace").Value.Value as string;
                        break;
                        
                    case "XmlAttributeAttribute":
                        info.SerializeAsAttribute = true;
                        info.AttributeName = attr.ConstructorArguments.Length > 0 
                            ? (string)attr.ConstructorArguments[0].Value 
                            : null;
                        break;
                        
                    case "XmlTextAttribute":
                        info.SerializeAsInnerText = true;
                        break;
                        
                    case "XmlFormatAttribute":
                        info.Formats = attr.ConstructorArguments[0].Values
                            .Select(v => (string)v.Value)
                            .ToArray();
                        break;
                        
                    case "XmlArrayAttribute":
                        info.ArrayElementName = (string)attr.ConstructorArguments[0].Value;
                        break;
                        
                    case "XmlArrayItemAttribute":
                        info.ArrayItemElementName = (string)attr.ConstructorArguments[0].Value;
                        break;
                        
                    case "XmlStreamListElementAttribute":
                        var xmlName = (string)attr.ConstructorArguments[0].Value;
                        var targetType = (INamedTypeSymbol)attr.ConstructorArguments[1].Value;
                        info.PolymorphicMappings.Add((xmlName, targetType));
                        break;

                    case "XmlAnyElementAttribute":
                        info.IsAnyElement = true;
                        break;

                    case "XmlAnyAttributeAttribute":
                        info.IsAnyAttribute = true;
                        break;
                }
            }

            // Analyze type
            info.PropertyKind = DeterminePropertyKind(property.Type, compilation);

            // Check for polymorphism (XmlInclude)
            ITypeSymbol typeToCheck = info.PropertyKind == PropertyKind.Collection 
                ? ((INamedTypeSymbol)property.Type).TypeArguments[0] 
                : property.Type;

            AnalyzePolymorphism(info, typeToCheck);
            
            return info;
        }

        private static void AnalyzePolymorphism(PropertyInfo info, ITypeSymbol type)
        {
            // Check for XmlInclude on the type definition
            foreach (var attr in type.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "XmlIncludeAttribute")
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is INamedTypeSymbol targetType)
                    {
                        // Use the target type name as the XML name by default, or check for XmlType/XmlRoot on the target type
                        string xmlName = targetType.Name;
                        
                        var typeAttr = targetType.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "XmlTypeAttribute");
                        if (typeAttr != null && typeAttr.ConstructorArguments.Length > 0)
                        {
                            xmlName = (string)typeAttr.ConstructorArguments[0].Value;
                        }
                        else
                        {
                            var rootAttr = targetType.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "XmlRootAttribute");
                            if (rootAttr != null && rootAttr.ConstructorArguments.Length > 0)
                            {
                                xmlName = (string)rootAttr.ConstructorArguments[0].Value;
                            }
                        }
                        
                        info.PolymorphicMappings.Add((xmlName, targetType));
                    }
                }
            }
        }

        private static PropertyKind DeterminePropertyKind(ITypeSymbol type, Compilation compilation)
        {
            // Primitives
            if (type.SpecialType == SpecialType.System_String ||
                type.SpecialType == SpecialType.System_Int32 ||
                type.SpecialType == SpecialType.System_Int64 ||
                type.SpecialType == SpecialType.System_Double ||
                type.SpecialType == SpecialType.System_Single ||
                type.SpecialType == SpecialType.System_Boolean ||
                type.SpecialType == SpecialType.System_Decimal)
            {
                return PropertyKind.Primitive;
            }

            // Nullable<T>
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return PropertyKind.Nullable;
            }

            // DateTime types
            var typeName = type.ToDisplayString();
            if (typeName == "System.DateTime" || typeName == "System.DateTimeOffset")
            {
                return PropertyKind.DateTime;
            }
            if (typeName == "System.DateOnly" || typeName == "System.TimeOnly" || typeName == "System.TimeSpan")
            {
                return PropertyKind.DateTime;
            }

            // Enum
            if (type.TypeKind == TypeKind.Enum)
            {
                return PropertyKind.Enum;
            }

            // Collection
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var genericDef = namedType.ConstructedFrom.ToDisplayString();
                if (genericDef.StartsWith("System.Collections.Generic.List<") ||
                    genericDef.StartsWith("System.Collections.Generic.IList<"))
                {
                    return PropertyKind.Collection;
                }
            }

            // Complex object
            return PropertyKind.ComplexObject;
        }
    }

    internal class PropertyInfo
    {
        public IPropertySymbol Symbol { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public ITypeSymbol Type { get; set; }
        public PropertyKind PropertyKind { get; set; }
        
        public bool IsIgnored { get; set; }
        public bool SerializeAsAttribute { get; set; }
        public bool SerializeAsInnerText { get; set; }
        public bool IsAnyElement { get; set; }
        public bool IsAnyAttribute { get; set; }
        
        public string? XmlElementName { get; set; }
        public string? AttributeName { get; set; }
        public string? ArrayElementName { get; set; }
        public string? ArrayItemElementName { get; set; }
        public string? Namespace { get; set; }
        
        public string[]? Formats { get; set; }
        public List<(string XmlName, INamedTypeSymbol TargetType)> PolymorphicMappings { get; set; } = new();
        
        public bool IsPolymorphic => PolymorphicMappings.Count > 0;
    }

    internal enum PropertyKind
    {
        Primitive,
        Nullable,
        DateTime,
        Enum,
        Collection,
        ComplexObject
    }
}
