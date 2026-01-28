using Microsoft.CodeAnalysis;
using XmlSourceGenerator.Helpers;
using XmlSourceGenerator.Models;

namespace XmlSourceGenerator.Generators
{
    /// <summary>
    /// Generates ReadFromXml method implementation.
    /// </summary>
    public partial class XmlReadGenerator
    {
        private readonly IndentedStringBuilder _sb;
        public XmlReadGenerator(IndentedStringBuilder sb)
        {
            _sb = sb;
        }

        private bool ImplementsIXmlStreamable(ITypeSymbol type)
        {
            return type.AllInterfaces.Any(i => i.Name == "IXmlStreamable" && i.ContainingNamespace.ToString() == "XmlSourceGenerator.Abstractions");
        }

        public void GenerateReadMethod(GeneratorTypeModel model)
        {
            GenerateReadMethodInternal(model.Properties, model.Name, false);
        }

        public void GenerateReadMethod(INamedTypeSymbol classSymbol)
        {
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

            bool isNew = classSymbol.BaseType != null && ImplementsIXmlStreamable(classSymbol.BaseType);
            GenerateReadMethodInternal(analyzedProperties, classSymbol.Name, isNew);
        }

        private void GenerateReadMethodInternal(IEnumerable<GeneratorPropertyModel> properties, string className, bool isNew)
        {
            string newModifier = isNew ? "new " : "";
            _sb.AppendLine($"public {newModifier} void ReadFromXml(XElement element, XmlSerializationOptions? options = null)");
            _sb.AppendLine("{");
            
            using (_sb.Indent())
            {
                var knownElements = new System.Collections.Generic.HashSet<string>();
                var knownAttributes = new System.Collections.Generic.HashSet<string>();

                GeneratorPropertyModel? anyElementProp = null;
                GeneratorPropertyModel? anyAttributeProp = null;

                // Pass 1: Analyze and collect known names
                foreach (var info in properties)
                {
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
                        else if (info.TypeInfo.Kind == PropertyKind.Collection && info.ArrayElementName != null)
                        {
                            knownElements.Add(info.ArrayElementName);
                        }
                        else
                        {
                            knownElements.Add(info.Name); 
                        }
                    }
                }

                // Pass 2: Generate standard read
                foreach (var info in properties)
                {
                    if (info.IsAnyElement || info.IsAnyAttribute) continue;

                    if (info.IsPolymorphic)
                    {
                        GeneratePolymorphicListRead(info);
                    }
                    else if (info.TypeInfo.Kind == PropertyKind.Collection)
                    {
                        new XmlCollectionGenerator(_sb).GenerateCollectionRead(info);
                    }
                    else
                    {
                        new XmlPropertyGenerator(_sb).GenerateStandardPropertyRead(info, className);
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

        private void GeneratePolymorphicListRead(GeneratorPropertyModel info)
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
                            _sb.AppendLine($"var {varName} = new {mapping.TargetTypeName}();");
                            if (mapping.ImplementsIXmlStreamable)
                            {
                                 _sb.AppendLine($"if ({varName} is IXmlStreamable {streamableVarName}) {streamableVarName}.ReadFromXml(child, options);");
                            }
                            else
                            {
                                 _sb.AppendLine($"{varName} = ReflectionHelper.Deserialize<{mapping.TargetTypeName}>(child, options);");
                            }
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
