using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace XmlSourceGenerator
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

            // Check attributes (with inheritance for overrides)
            foreach (var attr in GetXmlAttributes(property))
            {
                var attrName = attr.AttributeClass?.Name;
                
                switch (attrName)
                {
                    case "XmlIgnoreAttribute":
                        info.IsIgnored = true;
                        break;
                        
                    case "XmlElementAttribute":
                        string? elementName = null;
                        INamedTypeSymbol? type = null;

                        if (attr.ConstructorArguments.Length > 0)
                        {
                            if (attr.ConstructorArguments[0].Value is string s)
                            {
                                elementName = s;
                                if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is INamedTypeSymbol t)
                                {
                                    type = t;
                                }
                            }
                            else if (attr.ConstructorArguments[0].Value is INamedTypeSymbol t)
                            {
                                type = t;
                            }
                        }

                        if (elementName == null)
                        {
                            elementName = attr.NamedArguments.FirstOrDefault(a => a.Key == "ElementName").Value.Value as string;
                        }
                        
                        if (type == null)
                        {
                            var typeArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Type");
                            if (typeArg.Value.Value is INamedTypeSymbol t)
                            {
                                type = t;
                            }
                        }

                        if (type != null)
                        {
                            string polyXmlName = elementName ?? type.Name;
                            info.PolymorphicMappings.Add((polyXmlName, type));
                        }
                        else
                        {
                            info.XmlElementName = elementName;
                        }

                        info.Namespace = attr.NamedArguments.FirstOrDefault(a => a.Key == "Namespace").Value.Value as string;
                        
                        var isNullableArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "IsNullable");
                        if (isNullableArg.Value.Value != null)
                        {
                            info.IsNullable = (bool)isNullableArg.Value.Value;
                        }
                        break;
                        
                    case "XmlAttributeAttribute":
                        info.SerializeAsAttribute = true;
                        if (attr.ConstructorArguments.Length > 0)
                        {
                            info.AttributeName = (string)attr.ConstructorArguments[0].Value;
                        }
                        else
                        {
                            info.AttributeName = attr.NamedArguments.FirstOrDefault(a => a.Key == "AttributeName").Value.Value as string;
                        }
                        info.Namespace = attr.NamedArguments.FirstOrDefault(a => a.Key == "Namespace").Value.Value as string;
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
                        if (attr.ConstructorArguments.Length > 0)
                        {
                            info.ArrayElementName = (string)attr.ConstructorArguments[0].Value;
                        }
                        else
                        {
                            info.ArrayElementName = attr.NamedArguments.FirstOrDefault(a => a.Key == "ElementName").Value.Value as string;
                        }
                        break;
                        
                    case "XmlArrayItemAttribute":
                        if (attr.ConstructorArguments.Length > 0)
                        {
                            info.ArrayItemElementName = (string)attr.ConstructorArguments[0].Value;
                        }
                        else
                        {
                            info.ArrayItemElementName = attr.NamedArguments.FirstOrDefault(a => a.Key == "ElementName").Value.Value as string;
                        }
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

            // Default IsNullable to true for nullable value types if not explicitly set
            // Note: We need to check if it was set by attribute already. 
            // But since we parse attributes first, we might have missed the default.
            // Actually, let's just set the default if it wasn't set. 
            // However, boolean default is false. We need to know if it was assigned.
            // Simpler: If PropertyKind is Nullable, default IsNullable to true unless attribute says false.
            // But we already parsed attributes. 
            // Let's re-evaluate IsNullable logic.
            
            // If PropertyKind is Nullable (e.g. int?), XmlSerializer defaults IsNullable=true.
            if (info.PropertyKind == PropertyKind.Nullable)
            {
                 if (!info.IsNullable.HasValue)
                 {
                     info.IsNullable = true;
                 }
            }
            else
            {
                // For other types, default is false if not set
                if (!info.IsNullable.HasValue)
                {
                    info.IsNullable = false;
                }
            }

            // Check for polymorphism (XmlInclude)
            ITypeSymbol typeToCheck = info.PropertyKind == PropertyKind.Collection 
                ? ((INamedTypeSymbol)property.Type).TypeArguments[0] 
                : property.Type;

            AnalyzePolymorphism(info, typeToCheck);
            
            return info;
        }

        /// <summary>
        /// Gets XML attributes for a property, with inheritance support for overrides.
        /// If a property is an override without XML attributes, inherits from base property.
        /// </summary>
        private static IEnumerable<AttributeData> GetXmlAttributes(IPropertySymbol property)
        {
            var attrs = property.GetAttributes();
            
            // If property is an override and has no XML attributes, check base
            if (property.IsOverride && !HasXmlAttributes(attrs))
            {
                var baseProperty = property.OverriddenProperty;
                if (baseProperty != null)
                {
                    return GetXmlAttributes(baseProperty); // Recursive for multi-level inheritance
                }
            }
            
            return attrs;
        }

        /// <summary>
        /// Checks if any attributes in the collection are XML-related.
        /// </summary>
        private static bool HasXmlAttributes(ImmutableArray<AttributeData> attributes)
        {
            return attributes.Any(a => 
            {
                var name = a.AttributeClass?.Name;
                return name != null && (
                    name == "XmlElementAttribute" ||
                    name == "XmlAttributeAttribute" ||
                    name == "XmlIgnoreAttribute" ||
                    name == "XmlTextAttribute" ||
                    name == "XmlArrayAttribute" ||
                    name == "XmlArrayItemAttribute" ||
                    name == "XmlAnyElementAttribute" ||
                    name == "XmlAnyAttributeAttribute");
            });
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
                        if (typeAttr != null)
                        {
                            if (typeAttr.ConstructorArguments.Length > 0)
                            {
                                xmlName = (string)typeAttr.ConstructorArguments[0].Value;
                            }
                            else
                            {
                                var typeName = typeAttr.NamedArguments.FirstOrDefault(a => a.Key == "TypeName").Value.Value as string;
                                if (!string.IsNullOrEmpty(typeName))
                                {
                                    xmlName = typeName;
                                }
                            }
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
        public bool? IsNullable { get; set; }
        
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
