using Microsoft.CodeAnalysis;
using XmlSourceGenerator.Helpers;
using XmlSourceGenerator.Models;

namespace XmlSourceGenerator.Generators
{
    /// <summary>
    /// Generates collection read/write code.
    /// </summary>
    public class XmlCollectionGenerator
    {
        private readonly IndentedStringBuilder _sb;
        public XmlCollectionGenerator(IndentedStringBuilder sb)
        {
            _sb = sb;
            //_compilation = compilation;
        }

        public void GenerateCollectionRead(GeneratorPropertyModel info)
        {
            var itemTypeModel = info.ItemTypeInfo!;
            string itemTypeName = itemTypeModel.FullName;
            
            string containerName = info.ArrayElementName ?? info.Name;
            bool isWrapped = info.ArrayElementName != null;
            string itemXmlName = info.ArrayItemElementName ?? info.XmlElementName ?? itemTypeModel.Name;

            string? ns = info.Namespace;



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
                    GenerateCollectionLoop(info, itemTypeModel, itemXmlName, $"container_{info.Name}", ns);
                }
                _sb.AppendLine("}");
            }
            else
            {
                if (info.XmlElementName != null || info.IsFlattened)
                {
                    // Explicit or Implicit Flattening: Read directly from element
                    // If IsFlattened and not named, we might need to filter by Type Name or Polymorphic names
                    // If Polymorphic, GenerateCollectionLoop handles names internally.
                    // If not polymorphic and not named, default to ItemType Name.
                    string? effectiveItemName = info.XmlElementName ?? (info.IsPolymorphic ? null : itemTypeModel.Name);
                    GenerateCollectionLoop(info, itemTypeModel, effectiveItemName, "element", ns);
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
                        GenerateCollectionLoop(info, itemTypeModel, null, $"container_{info.Name}", ns);  
                    }
                    _sb.AppendLine("}");
                }
            }
        }

        private void GenerateCollectionLoop(GeneratorPropertyModel info, GeneratorTypeModel itemTypeModel, string? itemXmlName, string parentElementVar, string? ns)
        {
            _sb.AppendLine($"foreach (var child in {parentElementVar}.Elements())");
            _sb.AppendLine("{");
            using (_sb.Indent())
            {
                if (info.IsPolymorphic)
                {
                    bool first = true;
                    foreach (var mapping in info.PolymorphicMappings)
                    {
                        string elsePrefix = first ? "" : "else ";
                        _sb.AppendLine($"{elsePrefix}if (child.Name.LocalName == \"{mapping.XmlName}\")");
                        _sb.AppendLine("{");
                        using (_sb.Indent())
                        {
                            string targetTypeName = mapping.TargetTypeName;
                            _sb.AppendLine($"var item = new {targetTypeName}();");
                            _sb.AppendLine("if (item is IXmlStreamable streamable) streamable.ReadFromXml(child, options);");
                            _sb.AppendLine($"if ({info.Name} == null) {info.Name} = new();");
                            _sb.AppendLine($"{info.Name}.Add(item);");
                        }
                        _sb.AppendLine("}");
                        first = false;
                    }
                }
                else
                {
                    if (itemXmlName != null)
                    {
                        _sb.AppendLine($"if (child.Name.LocalName != \"{itemXmlName}\") continue;");
                    }

                    string itemTypeName = itemTypeModel.FullName;
                    bool isItemPrimitive = itemTypeModel.Kind == PropertyKind.Primitive;
                    
                    _sb.AppendLine($"if ({info.Name} == null) {info.Name} = new();");
                    if (isItemPrimitive)
                    {
                        if (itemTypeModel.IsString)
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
            }
            _sb.AppendLine("}");
        }

        public void GenerateCollectionWrite(GeneratorPropertyModel info)
        {
            var itemTypeModel = info.ItemTypeInfo;
            
            bool isWrapped = info.ArrayElementName != null;
            string containerName = info.ArrayElementName ?? info.Name;
            string? itemXmlName = info.ArrayItemElementName ?? info.XmlElementName;
            string fallbackItemName = itemXmlName ?? itemTypeModel.Name;

            string? ns = info.Namespace;

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
                else if (info.XmlElementName == null && !info.IsFlattened) // Implicit container
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
                    if (itemTypeModel.Kind == PropertyKind.Primitive)
                    {
                         if (ns != null && isWrapped) 
                             _sb.AppendLine($"{parentVar}.Add(new XElement(ns_{info.Name} + \"{fallbackItemName}\", item));");
                         else
                             _sb.AppendLine($"{parentVar}.Add(new XElement(\"{fallbackItemName}\", item));");
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
