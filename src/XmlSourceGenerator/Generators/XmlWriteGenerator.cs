using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Generates WriteToXml method implementation.
    /// </summary>
    internal class XmlWriteGenerator
    {
        private readonly IndentedStringBuilder _sb;
        private readonly Compilation _compilation;

        public XmlWriteGenerator(IndentedStringBuilder sb, Compilation compilation)
        {
            _sb = sb;
            _compilation = compilation;
        }

        public void GenerateWriteMethod(INamedTypeSymbol classSymbol)
        {
            _sb.AppendLine("public XElement WriteToXml(XmlSerializationOptions options = null)");
            _sb.AppendLine("{");
            
            using (_sb.Indent())
            {

                // Root element name
                string rootName = classSymbol.Name;
                string? rootNs = XmlNamespaceHelper.GetNamespace(classSymbol);
                
                var rootAttr = classSymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass.Name == "XmlRootAttribute");
                if (rootAttr != null)
                {
                    if (rootAttr.ConstructorArguments.Length > 0)
                    {
                        rootName = (string)rootAttr.ConstructorArguments[0].Value;
                    }
                }

                if (rootNs != null)
                {
                    _sb.AppendLine($"XNamespace rootNs = \"{rootNs}\";");
                    _sb.AppendLine($"var element = new XElement(rootNs + \"{rootName}\");");
                }
                else
                {
                    _sb.AppendLine($"var element = new XElement(\"{rootName}\");");
                }

                var properties = PropertyHelpers.GetAllProperties(classSymbol);

                foreach (var prop in properties)
                {
                    if (prop.IsReadOnly || prop.IsStatic) continue;

                    var info = PropertyAnalyzer.AnalyzeProperty(prop, _compilation);
                    if (info.IsIgnored) continue;

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
                    else if (info.PropertyKind == PropertyKind.Collection)
                    {
                        new XmlCollectionGenerator(_sb, _compilation).GenerateCollectionWrite(info);
                    }
                    else
                    {
                        new XmlPropertyGenerator(_sb, _compilation).GenerateStandardPropertyWrite(info, classSymbol);
                    }
                }

                _sb.AppendLine("return element;");
            }
            _sb.AppendLine("}");
        }

        private void GeneratePolymorphicListWrite(PropertyInfo info)
        {
            _sb.AppendLine($"if ({info.Name} != null)");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                _sb.AppendLine($"foreach (var item in {info.Name})");
                _sb.AppendLine("{");
                using (_sb.Indent())
                {
                    bool first = true;
                    int index = 0;
                    foreach (var mapping in info.PolymorphicMappings)
                    {
                        string elsePrefix = first ? "" : "else ";
                        string varName = $"typedItem{index}";
                        _sb.AppendLine($"{elsePrefix}if (item is {mapping.TargetType.ToDisplayString()} {varName})");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine($"var child = ((IXmlStreamable){varName}).WriteToXml(options);");
                            _sb.AppendLine($"child.Name = \"{mapping.XmlName}\";");
                            _sb.AppendLine("element.Add(child);");
                        }
                        _sb.AppendLine("}");
                        first = false;
                        index++;
                    }
                }
                _sb.AppendLine("}");
            }
            _sb.AppendLine("}");
        }
    }
}
