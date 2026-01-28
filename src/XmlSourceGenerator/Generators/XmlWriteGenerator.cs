using Microsoft.CodeAnalysis;
using XmlSourceGenerator.Helpers;
using XmlSourceGenerator.Models;

namespace XmlSourceGenerator.Generators
{
    /// <summary>
    /// Generates WriteToXml method implementation.
    /// </summary>
    public class XmlWriteGenerator
    {
        private readonly IndentedStringBuilder _sb;


        public XmlWriteGenerator(IndentedStringBuilder sb)
        {
            _sb = sb;
        }

        private bool ImplementsIXmlStreamable(ITypeSymbol type)
        {
            return type.AllInterfaces.Any(i => i.Name == "IXmlStreamable" && i.ContainingNamespace.ToString() == "XmlSourceGenerator.Abstractions");
        }

        public void GenerateWriteMethod(GeneratorTypeModel model)
        {
            GenerateWriteMethodInternal(model.Properties, model.Name, null, model.Name, model.Namespace, false);
        }

        public void GenerateWriteMethod(INamedTypeSymbol classSymbol)
        {
            // Root element name calculation
            string rootName = classSymbol.Name;
            var rootAttr = classSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "XmlRootAttribute");
            if (rootAttr != null)
            {
                if (rootAttr.ConstructorArguments.Length > 0)
                {
                    string? val = rootAttr.ConstructorArguments[0].Value as string;
                    if (!string.IsNullOrEmpty(val)) rootName = val!;
                }
                else
                {
                    var elementName = rootAttr.NamedArguments.FirstOrDefault(a => a.Key == "ElementName").Value.Value as string;
                    if (!string.IsNullOrEmpty(elementName))
                    {
                        rootName = elementName!;
                    }
                }
            }

            // Public property with attributes to hide from UI/Debug
            bool isNew = classSymbol.BaseType != null && ImplementsIXmlStreamable(classSymbol.BaseType);
            string newModifier = isNew ? "new " : "";
            _sb.AppendLine($"[global::System.ComponentModel.Browsable(false)]");
            _sb.AppendLine($"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
            _sb.AppendLine($"[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]");
            _sb.AppendLine($"public {newModifier}string DefaultXmlRootElementName => \"{rootName}\";");

            string? rootNs = XmlNamespaceHelper.GetNamespace(classSymbol);
            var members = PropertyHelpers.GetAllMembers(classSymbol);
            var analyzedProperties = new List<GeneratorPropertyModel>();

            foreach (var member in members)
            {
                if (member.IsStatic) continue;
                if (member is IPropertySymbol p && p.IsReadOnly) continue;


                var info = PropertyAnalyzer.AnalyzeMember(member);
                if (info.IsIgnored) continue;
                analyzedProperties.Add(info);
            }

            GenerateWriteMethodInternal(analyzedProperties, classSymbol.Name, rootNs, rootName, rootNs, isNew);
        }

        private void GenerateWriteMethodInternal(IEnumerable<GeneratorPropertyModel> properties, string className, string? rootNs, string rootName, string? targetNamespace, bool isNew)
        {
            // Generate Method
            string newModifier = isNew ? "new " : "";
            _sb.AppendLine($"public {newModifier} XElement WriteToXml(XmlSerializationOptions? options = null)");
            _sb.AppendLine("{");
            
            using (_sb.Indent())
            {
                if (rootNs != null)
                {
                    _sb.AppendLine($"XNamespace rootNs = \"{rootNs}\";");
                    _sb.AppendLine($"var element = new XElement(rootNs + DefaultXmlRootElementName);");
                }
                else
                {
                    _sb.AppendLine($"var element = new XElement(DefaultXmlRootElementName);");
                }

                // Sort properties: 
                // 1. Order >= 0, sorted by Order
                // 2. Order < 0 (default), sorted by declaration order (original list order)
                var sortedProperties = properties
                    .OrderBy(p => p.Order >= 0 ? p.Order : int.MaxValue)
                    .ThenBy(p => properties.ToList().IndexOf(p))
                    .ToList();

                foreach (var info in sortedProperties)
                {

                    if (info.IsPolymorphic)
                    {
                        GeneratePolymorphicListWrite(info);
                    }
                    else if (info.IsAnyElement)
                    {
                        // Write all elements in the collection
                        _sb.AppendLine($"if ({info.Name} != null)");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine($"foreach (var item in {info.Name})");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                // Assuming item is XElement or XmlElement. 
                                // Since we use XElement internally, we expect XElement or conversion.
                                // If the property type is List<XmlElement>, we might need conversion.
                                // For now, assuming List<XElement> or compatible.
                                _sb.AppendLine("if (item != null) element.Add(item);");
                            }
                            _sb.AppendLine("}");
                        }
                        _sb.AppendLine("}");
                    }
                    else if (info.IsAnyAttribute)
                    {
                        // Write all attributes
                        _sb.AppendLine($"if ({info.Name} != null)");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine($"foreach (var item in {info.Name})");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                _sb.AppendLine("if (item != null) element.Add(item);");
                            }
                            _sb.AppendLine("}");
                        }
                        _sb.AppendLine("}");
                    }
                    else if (info.TypeInfo.Kind == PropertyKind.Collection)
                    {
                        new XmlCollectionGenerator(_sb).GenerateCollectionWrite(info);
                    }
                    else
                    {
                        new XmlPropertyGenerator(_sb).GenerateStandardPropertyWrite(info, className);
                    }
                }

                _sb.AppendLine("return element;");
            }
            _sb.AppendLine("}");
        }

        private void GeneratePolymorphicListWrite(GeneratorPropertyModel info)
        {
            _sb.AppendLine($"if ({info.Name} != null)");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                _sb.AppendLine($"foreach (var item in {info.Name})");
                _sb.AppendLine("{");
                using (_sb.Indent())
                {
                    _sb.AppendLine("switch (item)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        int index = 0;
                        foreach (var mapping in info.PolymorphicMappings)
                        {
                            string varName = $"typedItem{index}";
                            _sb.AppendLine($"case {mapping.TargetTypeName} {varName}:");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                // Validated IXmlStreamable or fallback
                                if (mapping.ImplementsIXmlStreamable)
                                {
                                    _sb.AppendLine($"var child = ((IXmlStreamable){varName}).WriteToXml(options);");
                                    _sb.AppendLine($"child.Name = \"{mapping.XmlName}\";");
                                    _sb.AppendLine("element.Add(child);");
                                }
                                else
                                {
                                    _sb.AppendLine($"var child = ReflectionHelper.Serialize({varName}, options, \"{mapping.XmlName}\");");
                                    _sb.AppendLine("if (child != null) element.Add(child);");
                                }
                                _sb.AppendLine("break;");
                            }
                            _sb.AppendLine("}");
                            index++;
                        }
                    }
                    _sb.AppendLine("}");
                }
                _sb.AppendLine("}");
            }
            _sb.AppendLine("}");
        }
    }
}
