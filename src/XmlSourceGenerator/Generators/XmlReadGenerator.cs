using Microsoft.CodeAnalysis;
using XmlSourceGenerator.Helpers;

namespace XmlSourceGenerator.Generators
{
    /// <summary>
    /// Generates ReadFromXml method implementation.
    /// </summary>
    internal partial class XmlReadGenerator
    {
        private readonly IndentedStringBuilder _sb;
        private readonly Compilation _compilation;

        public XmlReadGenerator(IndentedStringBuilder sb, Compilation compilation)
        {
            _sb = sb;
            _compilation = compilation;
        }

        public void GenerateReadMethod(INamedTypeSymbol classSymbol)
        {
            _sb.AppendLine("public void ReadFromXml(XElement element, XmlSerializationOptions options = null)");
            _sb.AppendLine("{");
            
            using (_sb.Indent())
            {

                var properties = PropertyHelpers.GetAllProperties(classSymbol);
                var knownElements = new System.Collections.Generic.HashSet<string>();
                var knownAttributes = new System.Collections.Generic.HashSet<string>();

                PropertyInfo? anyElementProp = null;
                PropertyInfo? anyAttributeProp = null;

                // Pass 1: Analyze and collect known names
                foreach (var prop in properties)
                {
                    if (prop.IsReadOnly || prop.IsStatic) continue;
                    var info = PropertyAnalyzer.AnalyzeProperty(prop, _compilation);
                    if (info.IsIgnored) continue;

                    if (info.IsAnyElement)
                    {
                        anyElementProp = info;
                        continue;
                    }
                    if (info.IsAnyAttribute)
                    {
                        anyAttributeProp = info;
                        continue;
                    }

                    if (info.SerializeAsAttribute)
                    {
                        knownAttributes.Add(info.AttributeName ?? info.Name);
                    }
                    else if (!info.SerializeAsInnerText)
                    {
                        // Element or Collection
                        if (info.XmlElementName != null)
                        {
                            knownElements.Add(info.XmlElementName);
                        }
                        else if (info.PropertyKind == PropertyKind.Collection && info.ArrayElementName != null)
                        {
                            knownElements.Add(info.ArrayElementName);
                        }
                        else
                        {
                            // Dynamic name or implicit
                            // If dynamic, we can't easily add to knownElements set at compile time without more complex logic.
                            // But usually it's static.
                            // If it depends on options, we might miss it.
                            // For now, assume static or default.
                            knownElements.Add(info.Name); 
                        }
                    }
                }

                // Pass 2: Generate standard read
                foreach (var prop in properties)
                {
                    if (prop.IsReadOnly || prop.IsStatic) continue;
                    var info = PropertyAnalyzer.AnalyzeProperty(prop, _compilation);
                    if (info.IsIgnored || info.IsAnyElement || info.IsAnyAttribute) continue;

                    if (info.IsPolymorphic)
                    {
                        GeneratePolymorphicListRead(info);
                    }
                    else if (info.PropertyKind == PropertyKind.Collection)
                    {
                        new XmlCollectionGenerator(_sb, _compilation).GenerateCollectionRead(info, classSymbol);
                    }
                    else
                    {
                        new XmlPropertyGenerator(_sb, _compilation).GenerateStandardPropertyRead(info, classSymbol);
                    }
                }

                // Pass 3: Catch-all for AnyAttribute
                if (anyAttributeProp != null)
                {
                    _sb.AppendLine($"if ({anyAttributeProp.Name} == null) {anyAttributeProp.Name} = new();");
                    _sb.AppendLine("foreach (var attr in element.Attributes())");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        // Check if known
                        if (knownAttributes.Count > 0)
                        {
                            var checks = string.Join(" && ", knownAttributes.Select(n => $"attr.Name.LocalName != \"{n}\""));
                            _sb.AppendLine($"if ({checks})");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                _sb.AppendLine($"{anyAttributeProp.Name}.Add(attr);");
                            }
                            _sb.AppendLine("}");
                        }
                        else
                        {
                            _sb.AppendLine($"{anyAttributeProp.Name}.Add(attr);");
                        }
                    }
                    _sb.AppendLine("}");
                }

                // Pass 4: Catch-all for AnyElement
                if (anyElementProp != null)
                {
                    _sb.AppendLine($"if ({anyElementProp.Name} == null) {anyElementProp.Name} = new();");
                    _sb.AppendLine("foreach (var child in element.Elements())");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        // Check if known
                        if (knownElements.Count > 0)
                        {
                            var checks = string.Join(" && ", knownElements.Select(n => $"child.Name.LocalName != \"{n}\""));
                            _sb.AppendLine($"if ({checks})");
                            _sb.AppendLine("{");
                            using (_sb.Indent())
                            {
                                _sb.AppendLine($"{anyElementProp.Name}.Add(child);");
                            }
                            _sb.AppendLine("}");
                        }
                        else
                        {
                            _sb.AppendLine($"{anyElementProp.Name}.Add(child);");
                        }
                    }
                    _sb.AppendLine("}");
                }
            }
            _sb.AppendLine("}");
        }

        private void GeneratePolymorphicListRead(PropertyInfo info)
        {
            _sb.AppendLine($"if ({info.Name} == null) {info.Name} = new();");
            _sb.AppendLine("foreach (var child in element.Elements())");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                _sb.AppendLine("switch (child.Name.LocalName)");
                _sb.AppendLine("{");
                using (_sb.Indent())
                {
                    int index = 0;
                    foreach (var mapping in info.PolymorphicMappings)
                    {
                        _sb.AppendLine($"case \"{mapping.XmlName}\":");
                        using (_sb.Indent())
                        {
                            string varName = $"item{index}";
                            string streamableVarName = $"streamable{index}";
                            _sb.AppendLine($"var {varName} = new {mapping.TargetType.ToDisplayString()}();");
                            _sb.AppendLine($"if ({varName} is IXmlStreamable {streamableVarName}) {streamableVarName}.ReadFromXml(child, options);");
                            _sb.AppendLine($"{info.Name}.Add({varName});");
                            _sb.AppendLine("break;");
                        }
                        index++;
                    }
                }
                _sb.AppendLine("}");
            }
            _sb.AppendLine("}");
        }
    }
}
