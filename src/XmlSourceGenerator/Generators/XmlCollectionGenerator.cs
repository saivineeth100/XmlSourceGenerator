using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Generates collection read/write code.
    /// </summary>
    internal class XmlCollectionGenerator
    {
        private readonly IndentedStringBuilder _sb;
        private readonly Compilation _compilation;

        public XmlCollectionGenerator(IndentedStringBuilder sb, Compilation compilation)
        {
            _sb = sb;
            _compilation = compilation;
        }

        public void GenerateCollectionRead(PropertyInfo info, INamedTypeSymbol classSymbol)
        {
            var namedType = (INamedTypeSymbol)info.Type;
            var itemType = namedType.TypeArguments[0];
            string itemTypeName = itemType.ToDisplayString();
            
            string containerName = info.ArrayElementName ?? info.Name;
            bool isWrapped = info.ArrayElementName != null;
            string itemXmlName = info.ArrayItemElementName ?? info.XmlElementName ?? itemType.Name;

            string? ns = XmlNamespaceHelper.GetNamespace(info.Symbol);

            _sb.AppendLine($"if ({info.Name} == null) {info.Name} = new();");

            if (isWrapped)
            {
                if (ns != null)
                    _sb.AppendLine($"var container_{info.Name} = element.Element(XNamespace.Get(\"{ns}\") + \"{containerName}\");");
                else
                    _sb.AppendLine($"var container_{info.Name} = element.Element(\"{containerName}\");");
                
                _sb.AppendLine($"if (container_{info.Name} != null)");
                _sb.AppendLine("{");
                using (_sb.Indent())
                {
                    GenerateCollectionLoop(info, itemType, itemXmlName, $"container_{info.Name}", ns);
                }
                _sb.AppendLine("}");
            }
            else
            {
                if (info.XmlElementName != null)
                {
                    GenerateCollectionLoop(info, itemType, info.XmlElementName, "element", ns);
                }
                else
                {
                    // Implicit container
                    if (ns != null)
                        _sb.AppendLine($"var container_{info.Name} = element.Element(XNamespace.Get(\"{ns}\") + \"{info.Name}\");");
                    else
                        _sb.AppendLine($"var container_{info.Name} = element.Element(\"{info.Name}\");");

                    _sb.AppendLine($"if (container_{info.Name} != null)");
                    _sb.AppendLine("{");
                    using (_sb.Indent())
                    {
                        GenerateCollectionLoop(info, itemType, null, $"container_{info.Name}", ns); 
                    }
                    _sb.AppendLine("}");
                }
            }
        }

        private void GenerateCollectionLoop(PropertyInfo info, ITypeSymbol itemType, string? itemXmlName, string parentElementVar, string? ns)
        {
            _sb.AppendLine($"foreach (var child in {parentElementVar}.Elements())");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                if (itemXmlName != null)
                {
                    _sb.AppendLine($"if (child.Name.LocalName != \"{itemXmlName}\") continue;");
                    // Optionally check namespace here too if strict
                }

                string itemTypeName = itemType.ToDisplayString();
                bool isItemPrimitive = PropertyHelpers.IsPrimitive(itemType);
                
                if (isItemPrimitive)
                {
                    if (itemType.SpecialType == SpecialType.System_String)
                        _sb.AppendLine($"{info.Name}.Add(child.Value);");
                    else
                        _sb.AppendLine($"{info.Name}.Add(({itemTypeName})child);");
                }
                else
                {
                    _sb.AppendLine($"var item = new {itemTypeName}();");
                    _sb.AppendLine("if (item is IXmlStreamable streamable) streamable.ReadFromXml(child, options);");
                    _sb.AppendLine($"{info.Name}.Add(item);");
                }
            }
            _sb.AppendLine("}");
        }

        public void GenerateCollectionWrite(PropertyInfo info)
        {
            var namedType = (INamedTypeSymbol)info.Type;
            var itemType = namedType.TypeArguments[0];
            
            bool isWrapped = info.ArrayElementName != null;
            string containerName = info.ArrayElementName ?? info.Name;
            string itemXmlName = info.ArrayItemElementName ?? info.XmlElementName ?? itemType.Name;

            string? ns = XmlNamespaceHelper.GetNamespace(info.Symbol);

            _sb.AppendLine($"if ({info.Name} != null)");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                string parentVar = "element";
                if (isWrapped)
                {
                    if (ns != null)
                    {
                        _sb.AppendLine($"XNamespace ns_{info.Name} = \"{ns}\";");
                        _sb.AppendLine($"var container = new XElement(ns_{info.Name} + \"{containerName}\");");
                    }
                    else
                    {
                        _sb.AppendLine($"var container = new XElement(\"{containerName}\");");
                    }
                    _sb.AppendLine("element.Add(container);");
                    parentVar = "container";
                }
                else if (info.XmlElementName == null) // Implicit container
                {
                     if (ns != null)
                     {
                        _sb.AppendLine($"XNamespace ns_{info.Name} = \"{ns}\";");
                        _sb.AppendLine($"var container = new XElement(ns_{info.Name} + \"{info.Name}\");");
                     }
                     else
                     {
                        _sb.AppendLine($"var container = new XElement(\"{info.Name}\");");
                     }
                     _sb.AppendLine("element.Add(container);");
                     parentVar = "container";
                }

                _sb.AppendLine($"foreach (var item in {info.Name})");
                _sb.AppendLine("{");
                using (_sb.Indent())
                {
                    if (PropertyHelpers.IsPrimitive(itemType))
                    {
                         if (ns != null && isWrapped) 
                             _sb.AppendLine($"{parentVar}.Add(new XElement(ns_{info.Name} + \"{itemXmlName}\", item));");
                         else
                             _sb.AppendLine($"{parentVar}.Add(new XElement(\"{itemXmlName}\", item));");
                    }
                    else
                    {
                        _sb.AppendLine($"if (item is IXmlStreamable streamable)");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            _sb.AppendLine("var child = streamable.WriteToXml(options);");
                            if (itemXmlName != null)
                            {
                                if (ns != null && isWrapped)
                                    _sb.AppendLine($"child.Name = ns_{info.Name} + \"{itemXmlName}\";");
                                else
                                    _sb.AppendLine($"child.Name = \"{itemXmlName}\";");
                            }
                            _sb.AppendLine($"{parentVar}.Add(child);");
                        }
                        _sb.AppendLine("}");
                    }
                }
                _sb.AppendLine("}");
            }
            _sb.AppendLine("}");
        }
    }
}
