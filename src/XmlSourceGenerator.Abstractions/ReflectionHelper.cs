using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml;

namespace XmlSourceGenerator.Abstractions
{
    /// <summary>
    /// Provides reflection-based fallback for types that do not implement IXmlStreamable.
    /// </summary>
    public static class ReflectionHelper
    {
        private static readonly ConcurrentDictionary<Type, XmlTypeMetadata> _metadataCache = new();

        public static XElement? Serialize(object? item, XmlSerializationOptions? options, string? elementName = null)
        {
            if (item == null) return null;

            if (item is IXmlStreamable streamable)
            {
                var el = streamable.WriteToXml(options);
                if (elementName != null && el.Name != elementName)
                {
                    el.Name = elementName;
                }
                return el;
            }

            var type = item.GetType();
            var name = elementName ?? type.Name;
            var element = new XElement(name);
            var metadata = GetCachedMetadata(type);

            foreach (var prop in metadata.Properties.Where(p => p.CanRead))
            {
                var val = prop.Property.GetValue(item);
                if (val != null)
                {
                    // For primitives, write as Element by default in fallback mode
                    // TODO: Could use simple heuristics (int/string -> attribute?) but Element is safer for nesting.
                    if (IsSimpleType(prop.PropertyType))
                    {
                        element.Add(new XElement(prop.Name, FormatValue(val, prop.PropertyType)));
                    }
                    else
                    {
                        // Recursive serialization for complex types
                        element.Add(Serialize(val, options, prop.Name));
                    }
                }
            }

            return element;
        }

        public static T? Deserialize<T>(XElement? element, XmlSerializationOptions? options) where T : new()
        {
            if (element == null) return default;
            var item = new T();
            Populate(item, element, options);
            return item;
        }

        public static void Populate(object? item, XElement? element, XmlSerializationOptions? options)
        {
            if (item == null || element == null) return;

            if (item is IXmlStreamable streamable)
            {
                streamable.ReadFromXml(element, options);
                return;
            }

            var type = item.GetType();
            var metadata = GetCachedMetadata(type);

            foreach (var prop in metadata.Properties.Where(p => p.CanWrite))
            {
                // Try Element
                var childEl = element.Element(prop.Name);
                if (childEl != null)
                {
                    if (IsSimpleType(prop.PropertyType))
                    {
                        try 
                        {
                            object val = ConvertValue(childEl.Value, prop.UnderlyingType);
                            prop.Property.SetValue(item, val);
                        }
                        catch { /* Ignore conversion failure */ }
                    }
                    else
                    {
                        // Recursive deserialization
                        // For nested properties, we normally create new instances.
                        // We use Deserialize<T> via reflection because we need to know the type to create.
                        var method = typeof(ReflectionHelper).GetMethod(nameof(Deserialize), BindingFlags.Public | BindingFlags.Static)
                                        .MakeGenericMethod(prop.PropertyType);
                        var val = method.Invoke(null, new object[] { childEl, options });
                        prop.Property.SetValue(item, val);
                    }
                }
                else
                {
                    // Try Attribute
                    var attr = element.Attribute(prop.Name);
                    if (attr != null && IsSimpleType(prop.PropertyType))
                    {
                        try 
                        {
                            object val = ConvertValue(attr.Value, prop.UnderlyingType);
                            prop.Property.SetValue(item, val);
                        }
                        catch { /* Ignore conversion failure */ }
                    }
                }
            }
        }

        public static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal) || type == typeof(Guid) || 
                   type.Name == "DateOnly" || type.Name == "TimeOnly" || type.Name == "TimeSpan" ||
                   (Nullable.GetUnderlyingType(type) != null && IsSimpleType(Nullable.GetUnderlyingType(type)));
        }

        public static string FormatValue(object value, Type type)
        {
            if (value is DateTime dt) return dt.ToString("s"); // ISO 8601
            if (value is bool b) return b ? "true" : "false"; // XML lowercase
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static object ConvertValue(string value, Type type)
        {
             if (type == typeof(DateTime)) return DateTime.Parse(value);
             if (type == typeof(bool)) return XmlConvert.ToBoolean(value);
             if (type.IsEnum) return Enum.Parse(type, value);
             
             // Dynamic parse for DateOnly/TimeOnly
             if (type.Name == "DateOnly" || type.Name == "TimeOnly")
             {
                 var parseMethod = type.GetMethod("Parse", new[] { typeof(string) });
                 if (parseMethod != null) return parseMethod.Invoke(null, new object[] { value });
             }

             return Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static XmlTypeMetadata GetCachedMetadata(Type type)
        {
            return _metadataCache.GetOrAdd(type, t => new XmlTypeMetadata(t));
        }

        public class XmlTypeMetadata
        {
            public Type Type { get; }
            public string RootName { get; } // Add RootName if missing or ensure it exists
            public List<XmlPropertyMetadata> Properties { get; }

            public XmlTypeMetadata(Type type)
            {
                Type = type;
                RootName = type.Name; // Default
                // Check XmlRoot?
                var rootAttr = type.GetCustomAttributes(true).FirstOrDefault(a => a.GetType().Name == "XmlRootAttribute");
                if (rootAttr != null)
                {
                     var nameProp = rootAttr.GetType().GetProperty("ElementName");
                     var val = nameProp?.GetValue(rootAttr) as string;
                     if (!string.IsNullOrEmpty(val)) RootName = val!;
                }

                Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => new XmlPropertyMetadata(p))
                    .ToList();
            }
        }

        public class XmlPropertyMetadata
        {
            public PropertyInfo Property { get; }
            public string Name { get; }
            public string XmlName { get; }
            public Type PropertyType { get; }
            public Type UnderlyingType { get; }
            public bool CanRead { get; }
            public bool CanWrite { get; }
            public bool IsAttribute { get; }
            public bool IsIgnored { get; }

            public XmlPropertyMetadata(PropertyInfo property)
            {
                Property = property;
                Name = property.Name;
                XmlName = property.Name;
                PropertyType = property.PropertyType;
                UnderlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
                CanRead = property.CanRead;
                CanWrite = property.CanWrite;

                foreach (var attr in property.GetCustomAttributes(true))
                {
                    var typeName = attr.GetType().Name;
                    if (typeName == "XmlAttributeAttribute")
                    {
                        IsAttribute = true;
                        var nameProp = attr.GetType().GetProperty("AttributeName");
                        var val = nameProp?.GetValue(attr) as string;
                        if (!string.IsNullOrEmpty(val)) XmlName = val!;
                    }
                    else if (typeName == "XmlElementAttribute")
                    {
                        var nameProp = attr.GetType().GetProperty("ElementName");
                        var val = nameProp?.GetValue(attr) as string;
                        if (!string.IsNullOrEmpty(val)) XmlName = val!;
                    }
                    else if (typeName == "XmlIgnoreAttribute")
                    {
                        IsIgnored = true;
                    }
                }
            }
        }
    }
}
