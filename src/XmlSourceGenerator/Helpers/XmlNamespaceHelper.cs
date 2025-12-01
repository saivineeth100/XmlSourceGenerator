using Microsoft.CodeAnalysis;

namespace XmlSourceGenerator.Helpers
{
    internal static class XmlNamespaceHelper
    {
        public static string? GetNamespace(ISymbol symbol)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass == null) continue;
                
                string name = attr.AttributeClass.Name;
                if (name == "XmlElementAttribute" || name == "XmlRootAttribute" || name == "XmlAttributeAttribute" || name == "XmlTypeAttribute" || name == "XmlArrayAttribute")
                {
                    var nsArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Namespace");
                    if (!nsArg.Equals(default(KeyValuePair<string, TypedConstant>)))
                    {
                        return nsArg.Value.Value as string;
                    }
                }
            }
            return null;
        }
    }
}
