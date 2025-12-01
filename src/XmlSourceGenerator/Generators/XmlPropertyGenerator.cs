using Microsoft.CodeAnalysis;
using XmlSourceGenerator.Helpers;

namespace XmlSourceGenerator.Generators
{
    /// <summary>
    /// Generates property read/write code for individual properties.
    /// </summary>
    internal class XmlPropertyGenerator
    {
        private readonly IndentedStringBuilder _sb;
        private readonly Compilation _compilation;

        public XmlPropertyGenerator(IndentedStringBuilder sb, Compilation compilation)
        {
            _sb = sb;
            _compilation = compilation;
        }

        public void GenerateStandardPropertyRead(PropertyInfo info, INamedTypeSymbol classSymbol)
        {
            string propName = info.Name;
            string typeName = info.TypeName;

            string? ns = XmlNamespaceHelper.GetNamespace(info.Symbol);

            // Determine source (Attribute, InnerText, or Element)
            if (info.SerializeAsInnerText)
            {
                GenerateValueRead(info, "element");
            }
            else if (info.SerializeAsAttribute)
            {
                string attrName = info.AttributeName ?? propName;
                if (ns != null)
                {
                    _sb.AppendLine($"var attr_{propName} = element.Attribute(XNamespace.Get(\"{ns}\") + \"{attrName}\");");
                }
                else
                {
                    _sb.AppendLine($"var attr_{propName} = element.Attribute(\"{attrName}\");");
                }

                _sb.AppendLine($"if (attr_{propName} != null)");
                _sb.AppendLine("{");
                using (_sb.Indent())
                {
                    GenerateValueRead(info, $"attr_{propName}");
                }
                _sb.AppendLine("}");
            }
            else
            {
                // Element
                string xmlNameVar = $"xmlName_{propName}";
                if (info.XmlElementName != null)
                {
                    _sb.AppendLine($"string {xmlNameVar} = \"{info.XmlElementName}\";");
                    // Check for override if configured
                    _sb.AppendLine($"if (options != null && options.PreferOptionsOverAttributes)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        _sb.AppendLine($"var override_{propName} = options.GetOverride(typeof({classSymbol.Name}), \"{propName}\");");
                        _sb.AppendLine($"if (!string.IsNullOrEmpty(override_{propName})) {xmlNameVar} = override_{propName};");
                    }
                    _sb.AppendLine("}");
                }
                else
                {
                    _sb.AppendLine($"var {xmlNameVar} = options?.GetXmlName(XmlTypeInfo.Type, XmlTypeInfo.PropName_{propName}) ?? XmlTypeInfo.DefaultXmlName_{propName};");
                }

                if (ns != null)
                {
                    _sb.AppendLine($"var elem_{propName} = element.Element(XNamespace.Get(\"{ns}\") + {xmlNameVar});");
                }
                else
                {
                    _sb.AppendLine($"var elem_{propName} = element.Element({xmlNameVar});");
                }

                if (info.PropertyKind == PropertyKind.ComplexObject)
                {
                    // Check for polymorphic overrides for reading
                    _sb.AppendLine($"var polyMappings_{propName} = options?.GetPolymorphicMappings(typeof({classSymbol.Name}), \"{propName}\");");
                    _sb.AppendLine($"if (options != null && options.PreferOptionsOverAttributes && polyMappings_{propName} != null)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        _sb.AppendLine($"foreach (var mapping in polyMappings_{propName})");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            if (ns != null)
                            {
                                _sb.AppendLine($"var polyElem_{propName} = element.Element(XNamespace.Get(\"{ns}\") + mapping.Name);");
                            }
                            else
                            {
                                _sb.AppendLine($"var polyElem_{propName} = element.Element(mapping.Name);");
                            }

                            _sb.AppendLine($"if (polyElem_{propName} != null)");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                // Instantiate type dynamically
                                _sb.AppendLine($"{propName} = ({typeName})Activator.CreateInstance(mapping.Type);");
                                _sb.AppendLine($"if ({propName} is IXmlStreamable streamable_{propName}) streamable_{propName}.ReadFromXml(polyElem_{propName}, options);");
                                _sb.AppendLine("break;");
                            }
                            _sb.AppendLine("}");
                        }
                        _sb.AppendLine("}");
                    }
                    _sb.AppendLine("}");
                    _sb.AppendLine("else");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        _sb.AppendLine($"if (elem_{propName} != null)");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine($"{propName} = new {typeName}();");
                            _sb.AppendLine($"if ({propName} is IXmlStreamable streamable_{propName}) streamable_{propName}.ReadFromXml(elem_{propName}, options);");
                        }
                        _sb.AppendLine("}");
                    }
                    _sb.AppendLine("}");
                }
                else
                {
                    _sb.AppendLine($"if (elem_{propName} != null)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        GenerateValueRead(info, $"elem_{propName}");
                    }
                    _sb.AppendLine("}");
                }
            }
        }

        public void GenerateStandardPropertyWrite(PropertyInfo info, INamedTypeSymbol classSymbol)
        {
            string propName = info.Name;

            // Value formatting
            string valueExpression = propName;
            if (info.PropertyKind == PropertyKind.DateTime && info.Formats != null && info.Formats.Length > 0)
            {
                // Use first format for writing
                valueExpression = $"{propName}.ToString(\"{info.Formats[0]}\", CultureInfo.InvariantCulture)";
            }

            bool isReferenceOrNullable = info.Type.IsReferenceType || info.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

            string? ns = XmlNamespaceHelper.GetNamespace(info.Symbol);

            if (info.SerializeAsInnerText)
            {
                if (isReferenceOrNullable)
                    _sb.AppendLine($"if ({propName} != null) element.Value = {valueExpression}.ToString();");
                else
                    _sb.AppendLine($"element.Value = {valueExpression}.ToString();");
            }
            else if (info.SerializeAsAttribute)
            {
                string attrName = info.AttributeName ?? propName;
                string attrCreation = ns != null
                    ? $"new XAttribute(XNamespace.Get(\"{ns}\") + \"{attrName}\", {valueExpression})"
                    : $"new XAttribute(\"{attrName}\", {valueExpression})";

                if (isReferenceOrNullable)
                    _sb.AppendLine($"if ({propName} != null) element.Add({attrCreation});");
                else
                    _sb.AppendLine($"element.Add({attrCreation});");
            }
            else
            {
                // Element
                string xmlNameVar = $"xmlName_{propName}";
                if (info.XmlElementName != null)
                {
                    _sb.AppendLine($"string {xmlNameVar} = \"{info.XmlElementName}\";");
                    // Check for override if configured
                    _sb.AppendLine($"if (options != null && options.PreferOptionsOverAttributes)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        _sb.AppendLine($"var override_{propName} = options.GetOverride(typeof({classSymbol.Name}), \"{propName}\");");
                        _sb.AppendLine($"if (!string.IsNullOrEmpty(override_{propName})) {xmlNameVar} = override_{propName};");
                    }
                    _sb.AppendLine("}");
                }
                else
                {
                    _sb.AppendLine($"var {xmlNameVar} = options?.GetXmlName(XmlTypeInfo.Type, XmlTypeInfo.PropName_{propName}) ?? XmlTypeInfo.DefaultXmlName_{propName};");
                }

                string elementCreation;
                if (ns != null)
                {
                    _sb.AppendLine($"XNamespace ns_{propName} = \"{ns}\";");
                    elementCreation = $"new XElement(ns_{propName} + {xmlNameVar}, {valueExpression})";
                }
                else
                {
                    elementCreation = $"new XElement({xmlNameVar}, {valueExpression})";
                }

                if (info.PropertyKind == PropertyKind.Primitive || info.PropertyKind == PropertyKind.DateTime || info.PropertyKind == PropertyKind.Nullable)
                {
                    if (isReferenceOrNullable)
                    {
                        _sb.AppendLine($"if ({propName} != null)");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine($"element.Add({elementCreation});");
                        }
                        _sb.AppendLine("}");
                        if (info.IsNullable == true)
                        {
                            _sb.AppendLine("else");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                // Add nil element
                                if (ns != null)
                                {
                                    _sb.AppendLine($"var nilElement = new XElement(ns_{propName} + {xmlNameVar});");
                                }
                                else
                                {
                                    _sb.AppendLine($"var nilElement = new XElement({xmlNameVar});");
                                }
                                _sb.AppendLine("nilElement.Add(new XAttribute(XNamespace.Get(\"http://www.w3.org/2001/XMLSchema-instance\") + \"nil\", \"true\"));");
                                _sb.AppendLine("element.Add(nilElement);");
                            }
                            _sb.AppendLine("}");
                        }
                    }
                    else
                        _sb.AppendLine($"element.Add({elementCreation});");
                }
                else if (info.PropertyKind == PropertyKind.Enum)
                {
                    // Generate switch for enum write
                    _sb.AppendLine($"string enumValue_{propName} = {propName}.ToString();");

                    var enumMap = EnumHelper.GetEnumMap(info.Type);
                    if (enumMap.Any(kvp => kvp.Key != kvp.Value))
                    {
                        _sb.AppendLine($"switch ({propName})");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            foreach (var kvp in enumMap)
                            {
                                _sb.AppendLine($"case {info.TypeName}.{kvp.Key}: enumValue_{propName} = \"{kvp.Value}\"; break;");
                            }
                        }
                        _sb.AppendLine("}");
                    }

                    string enumElementCreation = ns != null
                        ? $"new XElement(ns_{propName} + {xmlNameVar}, enumValue_{propName})"
                        : $"new XElement({xmlNameVar}, enumValue_{propName})";

                    _sb.AppendLine($"element.Add({enumElementCreation});");
                }
                else
                {
                    // Complex object
                    _sb.AppendLine($"if ({propName} != null)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        // Check for polymorphic overrides
                        _sb.AppendLine($"var polyMappings_{propName} = options?.GetPolymorphicMappings(typeof({classSymbol.Name}), \"{propName}\");");
                        _sb.AppendLine($"if (options != null && options.PreferOptionsOverAttributes && polyMappings_{propName} != null)");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine($"bool matched_{propName} = false;");
                            _sb.AppendLine($"foreach (var mapping in polyMappings_{propName})");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                _sb.AppendLine($"if ({propName}.GetType() == mapping.Type)");
                                _sb.AppendLine("{");
                                using (_sb.Indent())
                                {
                                    _sb.AppendLine($"var child_{propName} = (({propName} as IXmlStreamable)?.WriteToXml(options));");
                                    _sb.AppendLine($"if (child_{propName} != null)");
                                    _sb.AppendLine("{");
                                    using (_sb.Indent())
                                    {
                                        if (ns != null)
                                        {
                                            _sb.AppendLine($"child_{propName}.Name = ns_{propName} + mapping.Name;");
                                        }
                                        else
                                        {
                                            _sb.AppendLine($"child_{propName}.Name = mapping.Name;");
                                        }
                                        _sb.AppendLine($"element.Add(child_{propName});");
                                    }
                                    _sb.AppendLine("}");
                                    _sb.AppendLine($"matched_{propName} = true;");
                                    _sb.AppendLine("break;");
                                }
                                _sb.AppendLine("}");
                            }
                            _sb.AppendLine("}");
                            // If no mapping matched, fall back? Or do nothing? 
                            // For now, if no mapping matches, we might want to fall back to standard behavior or just not serialize?
                            // Let's fall back to standard behavior if not matched
                            _sb.AppendLine($"if (!matched_{propName})");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                GenerateStandardComplexWrite(propName, xmlNameVar, ns);
                            }
                            _sb.AppendLine("}");
                        }
                        _sb.AppendLine("}");
                        _sb.AppendLine("else");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            GenerateStandardComplexWrite(propName, xmlNameVar, ns);
                        }
                        _sb.AppendLine("}");
                    }
                    _sb.AppendLine("}");
                }
            }
        }


        private void GenerateStandardComplexWrite(string propName, string xmlNameVar, string? ns)
        {
            _sb.AppendLine($"if ({propName} is IXmlStreamable streamable_{propName})");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                _sb.AppendLine($"var child_{propName} = streamable_{propName}.WriteToXml(options);");
                if (ns != null)
                {
                    _sb.AppendLine($"child_{propName}.Name = ns_{propName} + {xmlNameVar};");
                }
                else
                {
                    _sb.AppendLine($"child_{propName}.Name = {xmlNameVar};");
                }
                _sb.AppendLine($"element.Add(child_{propName});");
            }
            _sb.AppendLine("}");
        }

        private void GenerateValueRead(PropertyInfo info, string sourceVariable)
        {
            string propName = info.Name;
            string typeName = info.TypeName;

            if (info.PropertyKind == PropertyKind.Primitive || info.PropertyKind == PropertyKind.Nullable)
            {
                if (info.Type.SpecialType == SpecialType.System_String)
                {
                    _sb.AppendLine($"{propName} = {sourceVariable}.Value;");
                }
                else
                {
                    _sb.AppendLine($"{propName} = ({typeName}){sourceVariable};");
                }
            }
            else if (info.PropertyKind == PropertyKind.Enum)
            {
                // Generate switch for enum read
                var reverseMap = EnumHelper.GetReverseEnumMap(info.Type);
                if (reverseMap.Any(kvp => kvp.Key != kvp.Value))
                {
                    _sb.AppendLine($"switch ({sourceVariable}.Value)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        foreach (var kvp in reverseMap)
                        {
                            _sb.AppendLine($"case \"{kvp.Key}\": {propName} = {info.TypeName}.{kvp.Value}; break;");
                        }
                        _sb.AppendLine($"default: {propName} = ({typeName})Enum.Parse(typeof({typeName}), {sourceVariable}.Value); break;");
                    }
                    _sb.AppendLine("}");
                }
                else
                {
                    _sb.AppendLine($"{propName} = ({typeName})Enum.Parse(typeof({typeName}), {sourceVariable}.Value);");
                }
            }
            else if (info.PropertyKind == PropertyKind.DateTime)
            {
                string valueSource = $"{sourceVariable}.Value";

                if (info.Formats != null && info.Formats.Length > 0)
                {
                    _sb.AppendLine($"if (!string.IsNullOrEmpty({valueSource}))");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        _sb.AppendLine($"{typeName} result_{propName};");
                        var formats = string.Join(", ", info.Formats.Select(f => $"\"{f}\""));
                        _sb.AppendLine($"string[] formats_{propName} = {{ {formats} }};");

                        string parseMethod = "TryParseExact";

                        _sb.AppendLine($"if ({typeName}.{parseMethod}({valueSource}, formats_{propName}, CultureInfo.InvariantCulture, DateTimeStyles.None, out result_{propName}))");
                        _sb.AppendLine("{");
                        _sb.AppendLine($"    {propName} = result_{propName};");
                        _sb.AppendLine("}");
                    }
                    _sb.AppendLine("}");
                }
                else
                {
                    _sb.AppendLine($"{propName} = {typeName}.Parse({valueSource}, CultureInfo.InvariantCulture);");
                }
            }
        }
    }
}
