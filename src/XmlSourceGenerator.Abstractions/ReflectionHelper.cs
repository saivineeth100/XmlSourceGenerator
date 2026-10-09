using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

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

        [return: NotNullIfNotNull(nameof(element))]
        public static T? Deserialize<T>(XElement? element, XmlSerializationOptions? options) where T : new()
        {
            if (element == null) return default;
            var item = new T();
            Populate(item, element, options);
            return item;
        }

        /// <summary>Deserialize via boxed-<see cref="Type"/> instead of generic parameter</summary>
        /// <returns>
        ///   <see langword="null"/> when <paramref name="element"/> is null -OR- <paramref name="type"/> lacks a public, parameterless constructor<br/>
        /// </returns>
        internal static object? Deserialize(
            XElement? element,
            XmlSerializationOptions? options,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            Type type
        )
        {
            if (element == null) return default;
#pragma warning disable IL2070 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
            var item = type.GetConstructor([])?.Invoke(null);
#pragma warning restore IL2070 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
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
                            object? val = ConvertValue(childEl.Value, prop.UnderlyingType);
                            prop.Property.SetValue(item, val);
                        }
                        catch { /* Ignore conversion failure */ }
                    }
                    else
                    {
                        // deserializing to `dynamic` has runtime errors
                        // var val2 = ReflectionHelper.Deserialize<dynamic>(childEl, options);
                        // prop.Property.SetValue(item, val2);

                        // Recursive deserialization<br/>
                        // For nested properties, we normally create new instances.
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        var val = Deserialize(childEl, options, prop.PropertyType);
#pragma warning restore IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
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
                            object? val = ConvertValue(attr.Value, prop.UnderlyingType);
                            prop.Property.SetValue(item, val);
                        }
                        catch { /* Ignore conversion failure */ }
                    }
                }
            }
        }

        public static bool IsSimpleType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal) || type == typeof(Guid) ||
                type.Name == "DateOnly" || type.Name == "TimeOnly" || type.Name == "TimeSpan")
                return true;
            return Nullable.GetUnderlyingType(type) is { } underlyingType && IsSimpleType(underlyingType);
        }

        public static string FormatValue(object value, Type type)
        {
            if (value is DateTime dt) return dt.ToString("s"); // ISO 8601
            if (value is bool b) return b ? "true" : "false"; // XML lowercase
            return ConvertToString(value);
        }

        /// <summary> <see cref="Convert.ToString(object?, IFormatProvider?)"/>
        /// notes the following: "The string representation of value, or
        /// string.Empty if value is an object whose value is null. If value is
        /// null, the method returns null."<br/>
        /// In other words, it should utilize `[return:
        /// NotNullIfNotNull(nameof(value))]`, but doesn't.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull(nameof(value))]
        private static string? ConvertToString(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);

        private static object? ConvertValue(string value, Type type)
        {
            if (type == typeof(DateTime)) return DateTime.Parse(value);
            if (type == typeof(bool)) return XmlConvert.ToBoolean(value);
            if (type.IsEnum) return Enum.Parse(type, value);

            // Dynamic parse for DateOnly/TimeOnly
            if (type.Name == "DateOnly" || type.Name == "TimeOnly")
            {
#pragma warning disable IL2070 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
                var parseMethod = type.GetMethod("Parse", new[] { typeof(string) });
#pragma warning restore IL2070 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
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
                var rootAttr = type.GetCustomAttributes(true).FirstOrDefault(a => a.GetType().Name == nameof(XmlRootAttribute));
                if (rootAttr != null)
                {
#pragma warning disable IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                    var nameProp = rootAttr.GetType().GetProperty("ElementName");
#pragma warning restore IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                    var val = nameProp?.GetValue(rootAttr) as string;
                    if (!string.IsNullOrEmpty(val)) RootName = val!;
                }

#pragma warning disable IL2070 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
                Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => new XmlPropertyMetadata(p))
                    .ToList();
#pragma warning restore IL2070 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.
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
                    if (typeName == nameof(XmlAttributeAttribute))
                    {
                        IsAttribute = true;
#pragma warning disable IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        var nameProp = attr.GetType().GetProperty(nameof(XmlAttributeAttribute.AttributeName));
#pragma warning restore IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        var val = nameProp?.GetValue(attr) as string;
                        if (!string.IsNullOrEmpty(val)) XmlName = val!;
                    }
                    else if (typeName == nameof(XmlElementAttribute))
                    {
#pragma warning disable IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        var nameProp = attr.GetType().GetProperty(nameof(XmlElementAttribute.ElementName));
#pragma warning restore IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        var val = nameProp?.GetValue(attr) as string;
                        if (!string.IsNullOrEmpty(val)) XmlName = val!;
                    }
                    else if (typeName == nameof(XmlIgnoreAttribute))
                    {
                        IsIgnored = true;
                    }
                }
            }
        }
    }
}
