using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Reproduction
{
    public class SimpleItem
    {
        public int Value { get; set; }
        public string Text { get; set; }
    }

    public static class Program
    {
        public static async Task Main()
        {
            try
            {
                var items = new[] { new SimpleItem { Value = 7, Text = null } };
                using var stream = new MemoryStream();

                Console.WriteLine("Writing to stream...");
                await GenericXmlStreamer.WriteDataToStreamAsync(stream, items, itemName: "SimpleItem");
                
                Console.WriteLine("Stream Length: " + stream.Length);
                stream.Position = 0;
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                var text = reader.ReadToEnd();
                Console.WriteLine("XML Content:");
                Console.WriteLine(text);

                stream.Position = 0;
                var xml = XDocument.Load(stream);
                
                if (xml.Root == null) Console.WriteLine("Root is null");
                else Console.WriteLine("Root Name: " + xml.Root.Name);

                var item = xml.Root?.Element("SimpleItem");
                if (item == null)
                {
                    Console.WriteLine("SimpleItem element NOT FOUND in root.");
                }
                else
                {
                    Console.WriteLine("SimpleItem FOUND.");
                    Console.WriteLine("Value Element: " + (item.Element("Value") != null ? "Present" : "Missing"));
                    Console.WriteLine("Text Element: " + (item.Element("Text") != null ? "Present" : "Missing"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex);
            }
        }
    }

    // --- PASTE DEPENDENCIES ---
    
    public interface IXmlStreamable
    {
        string DefaultXmlRootElementName { get; }
        void ReadFromXml(XElement element, XmlSerializationOptions? options = null);
        XElement WriteToXml(XmlSerializationOptions? options = null);
    }

    public class XmlSerializationOptions
    {
        public bool WriteIndented { get; set; }
        public System.Text.Encoding Encoding { get; set; }
         public bool IgnoreParsingErrors { get; set; }
    }

    public static class GenericXmlStreamer
    {
        public static async Task WriteDataToStreamAsync<T>(Stream stream, IEnumerable<T> items, XmlSerializationOptions? options = null, string rootName = "ArrayOfItems", string? itemName = null)
        {
            string targetItemName = itemName ?? typeof(T).Name; // Simplified for repro

            var settings = new XmlWriterSettings 
            { 
                Async = true, 
                Indent = options?.WriteIndented ?? false,
                Encoding = options?.Encoding ?? System.Text.Encoding.UTF8
            };

            using (var writer = XmlWriter.Create(stream, settings))
            {
                await writer.WriteStartDocumentAsync();
                await writer.WriteStartElementAsync(null, rootName, null);

                foreach (var item in items)
                {
                    WriteItem(writer, item, targetItemName, options);
                }

                await writer.WriteEndElementAsync();
                await writer.WriteEndDocumentAsync();
            }
        }

        private static void WriteItem<T>(XmlWriter writer, T item, string? itemName, XmlSerializationOptions? options)
        {
             if (item == null) return;
             XElement el;
             if (item is IXmlStreamable streamable)
             {
                 el = streamable.WriteToXml(options);
                 if (!string.IsNullOrEmpty(itemName) && el.Name != itemName) el.Name = itemName; 
             }
             else
             {
                 el = ReflectionHelper.Serialize(item, options, itemName ?? item.GetType().Name);
             }

             el.WriteTo(writer);
        }
    }

    public static class ReflectionHelper
    {
        private static readonly ConcurrentDictionary<Type, XmlTypeMetadata> _metadataCache = new();

        public static XElement Serialize(object? item, XmlSerializationOptions? options, string? elementName = null)
        {
            if (item == null) return null; // Should return XElement

            // Repro fix: Return null? But MapToXElement returns XElement.
            // GenericXmlStreamer calls Serialize.
            
            // Wait, GenericXmlStreamer in project uses MapToXElement directly?
            // Let's check the code I read in Step 354.
            // Line 254: el = MapToXElement(item, ...);
            // MapToXElement is PRIVATE in GenericXmlStreamer. It is NOT calling ReflectionHelper.Serialize.
            // Ah! GenericXmlStreamer HAS its own MapToXElement implementation!
            // I should use THAT one.
            
            // I am correcting the repro script to match GenericXmlStreamer.cs logic.
            // Copied below.
            
            return MapToXElement(item, elementName ?? item.GetType().Name);
        }

        public static XElement MapToXElement<T>(T item, string elementName)
        {
            var el = new XElement(elementName);
            var metadata = GetCachedMetadata(item.GetType());

            foreach (var propMeta in metadata.Properties.Where(p => p.CanRead))
            {
                if (propMeta.IsIgnored) continue;

                var value = propMeta.Property.GetValue(item);
                if (value != null)
                {
                    if (propMeta.IsAttribute && IsSimpleType(propMeta.PropertyType))
                    {
                        el.Add(new XAttribute(propMeta.XmlName, FormatValue(value, propMeta.PropertyType)));
                    }
                    else
                    {
                        el.Add(new XElement(propMeta.XmlName, value));
                    }
                }
            }
            return el;
        }

        public static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(DateTime);
        }

        public static string FormatValue(object value, Type type)
        {
            return Convert.ToString(value);
        }

        public static XmlTypeMetadata GetCachedMetadata(Type type)
        {
            return _metadataCache.GetOrAdd(type, t => new XmlTypeMetadata(t));
        }

        public class XmlTypeMetadata
        {
            public List<XmlPropertyMetadata> Properties { get; }
            public string RootName { get; }

            public XmlTypeMetadata(Type type)
            {
                RootName = type.Name;
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
            public bool CanRead { get; }
            public bool IsAttribute { get; } 
            public bool IsIgnored { get; }

            public XmlPropertyMetadata(PropertyInfo property)
            {
                Property = property;
                Name = property.Name;
                XmlName = property.Name;
                PropertyType = property.PropertyType;
                CanRead = property.CanRead;
            }
        }
    }
}
