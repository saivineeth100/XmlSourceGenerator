using Microsoft.CodeAnalysis;

namespace XmlSourceGenerator.Helpers
{
    internal static class EnumHelper
    {
        public static Dictionary<string, string> GetEnumMap(ITypeSymbol enumType)
        {
            var map = new Dictionary<string, string>();

            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!member.IsConst) continue; // Skip non-enum members (like value__)

                string xmlName = member.Name;
                var attr = member.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "XmlEnumAttribute");
                if (attr != null)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is { } nonNullValue)
                    {
                        xmlName = (string)nonNullValue;
                    }
                    else
                    {
                        var nameArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string;
                        if (nameArg is not null and not "")
                        {
                            xmlName = nameArg;
                        }
                    }
                }

                map[member.Name] = xmlName;
            }

            return map;
        }

        public static Dictionary<string, string> GetReverseEnumMap(ITypeSymbol enumType)
        {
            var map = new Dictionary<string, string>();

            foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!member.IsConst) continue;

                string xmlName = member.Name;
                var attr = member.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == nameof(Abstractions.XmlEnumAttribute));
                if (attr != null)
                {
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is { } nonNullValue)
                    {
                        xmlName = (string)nonNullValue;
                    }
                    else
                    {
                        var nameArg = attr.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string;
                        if (nameArg is not null and not "")
                        {
                            xmlName = nameArg;
                        }
                    }
                }

                // Map XML value -> Enum Member Name
                if (!map.ContainsKey(xmlName))
                {
                    map[xmlName] = member.Name;
                }
            }

            return map;
        }
    }
}
