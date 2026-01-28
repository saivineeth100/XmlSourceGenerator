using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using XmlSourceGenerator.Helpers;
using XmlSourceGenerator.Models;

namespace XmlSourceGenerator
{
    /// <summary>
    /// Analyzes property metadata for code generation.
    /// Single Responsibility: Property analysis logic.
    /// </summary>
    public class PropertyAnalyzer
    {
        public static GeneratorPropertyModel AnalyzeMember(ISymbol member)
        {
            ITypeSymbol type = null;
            if (member is IPropertySymbol prop) type = prop.Type;
            else if (member is IFieldSymbol field) type = field.Type;
            else throw new System.ArgumentException("Member must be Property or Field");

            // Handle Nullable<T> unwrapping for analysis
            var underlyingType = type;
            bool isNullable = false;
            if (type is INamedTypeSymbol nt && nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                underlyingType = nt.TypeArguments[0];
                isNullable = true;
            }
            else if (type.IsReferenceType)
            {
               // Reference types are conceptually nullable in C# unless NRT enabled, but we track XML nullable explicitly
            }

            var typeModel = CreateTypeModel(underlyingType);
            
            var info = new GeneratorPropertyModel
            {
                Name = member.Name,
                TypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeInfo = typeModel
            };
            
            var mappingDepths = new List<(PolymorphicMappingModel Map, int Depth)>();

            // Set ItemTypeInfo if collection
            if (typeModel.Kind == PropertyKind.Collection && underlyingType is INamedTypeSymbol namedUnderlying)
            {
                 // Typically List<T> or IList<T>
                 var itemType = namedUnderlying.TypeArguments[0];
                 info.ItemTypeInfo = CreateTypeModel(itemType);
            }

            // Defaults based on type
            if (isNullable)
            {
                info.IsNullable = true;
            }
            if (type.IsReferenceType && !info.IsNullable.HasValue)
            {
                 // Default for reference types is usually false for "IsNullable" attribute (xsi:nil), 
                 // but we can leave it null to mean "default behavior"
                 info.IsNullable = false;
            }
            if (!type.IsReferenceType && !isNullable && !info.IsNullable.HasValue)
            {
                 info.IsNullable = false;
            }


            if (info.TypeInfo.Kind == PropertyKind.Collection)
            {
                info.IsFlattened = true;
            }

            // Check attributes (with inheritance for overrides)
            foreach (var attr in GetXmlAttributes(member))
            {
                var attrName = attr.AttributeClass?.Name;
                
                switch (attrName)
                {
                    case "XmlIgnoreAttribute":
                        info.IsIgnored = true;
                        break;
                        
                    case "XmlElementAttribute":
                        string? elementName = null;
                        INamedTypeSymbol? targetType = null;

                        if (attr.ConstructorArguments.Length > 0)
                        {
                            if (attr.ConstructorArguments[0].Value is string s)
                            {
                                elementName = s;
                                if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is INamedTypeSymbol t)
                                {
                                    targetType = t;
                                }
                            }
                            else if (attr.ConstructorArguments[0].Value is INamedTypeSymbol t)
                            {
                                targetType = t;
                            }
                        }

                        if (elementName == null)
                        {
                            elementName = attr.NamedArguments.FirstOrDefault(a => a.Key == "ElementName").Value.Value as string;
                        }
                        
                        if (targetType == null)
                        {
                            var typeArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Type");
                            if (typeArg.Value.Value is INamedTypeSymbol t)
                            {
                                targetType = t;
                            }
                        }

                        if (targetType != null)
                        {
                            string polyXmlName = elementName ?? targetType.Name;
                            var mapping = new PolymorphicMappingModel 
                            { 
                                XmlName = polyXmlName, 
                                TargetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                TargetTypeInfo = CreateTypeModel(targetType),
                                ImplementsIXmlStreamable = ImplementsIXmlStreamable(targetType)
                            };
                            
                            mappingDepths.Add((mapping, GetInheritanceDepth(targetType)));

                            // Implicitly flattened if polymorphic XmlElements are used
                            if (info.TypeInfo.Kind == PropertyKind.Collection) info.IsFlattened = true;
                        }
                        else
                        {
                            info.XmlElementName = elementName;
                            // If XmlElement is present on a collection, it essentially means flattened (no wrapper)
                            // If elementName is null (e.g. [XmlElement]), it means use item type name or default
                            if (info.TypeInfo.Kind == PropertyKind.Collection) info.IsFlattened = true;
                        }

                        info.Namespace = attr.NamedArguments.FirstOrDefault(a => a.Key == "Namespace").Value.Value as string;
                        
                        var isNullableArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "IsNullable");
                        if (isNullableArg.Value.Value != null)
                        {
                            info.IsNullable = (bool)isNullableArg.Value.Value;
                        }

                        var orderArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Order");
                        if (orderArg.Value.Value != null)
                        {
                            info.Order = (int)orderArg.Value.Value;
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
                        
                        var arrayOrderArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Order");
                        if (arrayOrderArg.Value.Value != null)
                        {
                            info.Order = (int)arrayOrderArg.Value.Value;
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
                        var tType = (INamedTypeSymbol)attr.ConstructorArguments[1].Value;
                         info.PolymorphicMappings.Add(new PolymorphicMappingModel 
                            { 
                                XmlName = xmlName, 
                                TargetTypeName = tType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                TargetTypeInfo = CreateTypeModel(tType),
                                ImplementsIXmlStreamable = ImplementsIXmlStreamable(tType)
                            });
                        break;

                    case "XmlAnyElementAttribute":
                        info.IsAnyElement = true;
                        var anyOrderArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Order");
                        if (anyOrderArg.Value.Value != null)
                        {
                            info.Order = (int)anyOrderArg.Value.Value;
                        }
                        break;

                    case "XmlAnyAttributeAttribute":
                        info.IsAnyAttribute = true;
                        break;
                }
            }

            // Polymorphism analysis via XmlInclude on property type
            ITypeSymbol typeToCheck = typeModel.Kind == PropertyKind.Collection && underlyingType is INamedTypeSymbol nu
                ? nu.TypeArguments[0] 
                : underlyingType;
            
            AnalyzePolymorphism(info, typeToCheck, mappingDepths);
            
            // Sort PolymorphicMappings by inheritance depth (descending)
            if (mappingDepths.Count > 0)
            {
               foreach(var item in mappingDepths.OrderByDescending(x => x.Depth))
               {
                   info.PolymorphicMappings.Add(item.Map);
               }
            }
            
            return info;
        }

        private static int GetInheritanceDepth(ITypeSymbol type)
        {
            int depth = 0;
            var current = type;
            while (current.BaseType != null)
            {
                depth++;
                current = current.BaseType;
            }
            return depth;
        }

        private static GeneratorTypeModel CreateTypeModel(ITypeSymbol type)
        {
             var kind = DeterminePropertyKind(type);
             var model = new GeneratorTypeModel
             {
                 Name = type.Name,
                 Namespace = type.ContainingNamespace?.ToString() ?? "",
                 FullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                 IsEnum = type.TypeKind == TypeKind.Enum,
                 IsReferenceType = type.IsReferenceType,
                 IsString = type.SpecialType == SpecialType.System_String,
                 ImplementsIXmlStreamable = ImplementsIXmlStreamable(type),
                 Kind = kind
             };

             if (model.IsEnum)
             {
                 model.EnumMapping = EnumHelper.GetEnumMap(type);
             }

             return model;
        }

        private static bool ImplementsIXmlStreamable(ITypeSymbol type)
        {
            return type.AllInterfaces.Any(i => i.Name == "IXmlStreamable" && i.ContainingNamespace.ToString() == "XmlSourceGenerator.Abstractions");
        }

        /// <summary>
        /// Gets XML attributes for a member, with inheritance support for property overrides.
        /// </summary>
        private static IEnumerable<AttributeData> GetXmlAttributes(ISymbol member)
        {
            var attrs = member.GetAttributes();
            
            if (member is IPropertySymbol property)
            {
                // If property is an override and has no XML attributes, check base
                if (property.IsOverride && !HasXmlAttributes(attrs))
                {
                    var baseProperty = property.OverriddenProperty;
                    if (baseProperty != null)
                    {
                        return GetXmlAttributes(baseProperty); // Recursive for multi-level inheritance
                    }
                }
            }
            
            return attrs;
        }

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

        private static void AnalyzePolymorphism(GeneratorPropertyModel info, ITypeSymbol type, List<(PolymorphicMappingModel Map, int Depth)> mappingDepths)
        {
            // Check for XmlInclude on the type definition
            foreach (var attr in type.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "XmlIncludeAttribute")
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is INamedTypeSymbol targetType)
                    {
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
                        
                        var mapping = new PolymorphicMappingModel
                        {
                             XmlName = xmlName,
                             TargetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                             TargetTypeInfo = CreateTypeModel(targetType),
                             ImplementsIXmlStreamable = ImplementsIXmlStreamable(targetType)
                        };
                        
                        mappingDepths.Add((mapping, GetInheritanceDepth(targetType)));
                    }
                }
            }
        }

        private static PropertyKind DeterminePropertyKind(ITypeSymbol type)
        {
            // Primitives
            if (IsPrimitive(type.SpecialType))
            {
                return PropertyKind.Primitive;
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

            // XElement
            if (typeName == "System.Xml.Linq.XElement")
            {
                return PropertyKind.XElement;
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

        private static bool IsPrimitive(SpecialType specialType)
        {
            return specialType >= SpecialType.System_Boolean && 
                   specialType <= SpecialType.System_String;
        }
    }
}
