using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Optional interface for high-performance manual mapping.
    /// If T implements this, Reflection is skipped.
    /// </summary>
    public interface IXmlStreamable
    {
        void ReadFromXml(XElement element, XmlSerializationOptions options = null);
        XElement WriteToXml(XmlSerializationOptions options = null);
    }

    public static class GenericXmlStreamer
    {
        // ---------------------------------------------------------
        // GENERIC READ: Stream -> IEnumerable<T>
        // ---------------------------------------------------------
        public static IEnumerable<T> ReadDataFromStream<T>(Stream stream, XmlSerializationOptions options = null, string itemName = null) where T : new()
        {
            // Default item name to class name if not provided
            string targetName = itemName ?? typeof(T).Name;
            
            var settings = new XmlReaderSettings { Async = true };
            using (var reader = XmlReader.Create(stream, settings))
            {
                // Skip to first content
                reader.MoveToContent();
                
                // Read through all elements
                while (!reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == targetName)
                    {
                        // Use ReadSubtree for cleaner element extraction
                        using (XmlReader subtree = reader.ReadSubtree())
                        {
                            subtree.MoveToContent();
                            XElement el = XElement.Load(subtree);

                            // Map to T
                            T item = new T();

                            if (item is IXmlStreamable streamable)
                            {
                                streamable.ReadFromXml(el, options);
                            }
                            else
                            {
                                MapFromXElement(item, el);
                            }

                            yield return item;
                        }
                        
                        // ReadSubtree positions reader AT the element, so we need to skip it
                        reader.Read(); // Move past the element we just processed
                    }
                    else
                    {
                        reader.Read(); // Move to next node
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // GENERIC WRITE: IEnumerable<T> -> Stream
        // ---------------------------------------------------------
        public static async Task WriteDataToStreamAsync<T>(Stream stream, IEnumerable<T> items, XmlSerializationOptions options = null, string rootName = "ArrayOfItems", string itemName = null)
        {
            string targetItemName = itemName ?? typeof(T).Name;

            var settings = new XmlWriterSettings 
            { 
                Async = true, 
                Indent = options?.WriteIndented ?? false
            };

            using (var writer = XmlWriter.Create(stream, settings))
            {
                await writer.WriteStartDocumentAsync();
                await writer.WriteStartElementAsync(null, rootName, null);

                foreach (var item in items)
                {
                    XElement el;
                    if (item is IXmlStreamable streamable)
                    {
                        el = streamable.WriteToXml(options);
                        // Ensure the name matches what we expect (optional consistency check)
                        if (el.Name != targetItemName) el.Name = targetItemName; 
                    }
                    else
                    {
                        el = MapToXElement(item, targetItemName);
                    }

                    el.WriteTo(writer);
                }

                await writer.WriteEndElementAsync();
                await writer.WriteEndDocumentAsync();
            }
        }

        // ---------------------------------------------------------
        // REFLECTION HELPERS (The "Dynamic" Part)
        // ---------------------------------------------------------
        
        // Metadata classes for caching type information
        private class XmlPropertyMetadata
        {
            public PropertyInfo Property { get; }
            public string Name { get; }
            public Type PropertyType { get; }
            public Type UnderlyingType { get; } // For nullable types
            public bool CanRead { get; }
            public bool CanWrite { get; }

            public XmlPropertyMetadata(PropertyInfo property)
            {
                Property = property;
                Name = property.Name;
                PropertyType = property.PropertyType;
                UnderlyingType = Nullable.GetUnderlyingType(PropertyType) ?? PropertyType;
                CanRead = property.CanRead;
                CanWrite = property.CanWrite;
            }
        }

        private class XmlTypeMetadata
        {
            public Type Type { get; }
            public XmlPropertyMetadata[] Properties { get; }

            public XmlTypeMetadata(Type type)
            {
                Type = type;
                Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => new XmlPropertyMetadata(p))
                    .ToArray();
            }
        }

        // Cache for type metadata to avoid repeated reflection
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, XmlTypeMetadata> _metadataCache 
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, XmlTypeMetadata>();

        private static XmlTypeMetadata GetCachedMetadata(Type type)
        {
            return _metadataCache.GetOrAdd(type, t => new XmlTypeMetadata(t));
        }

        private static void MapFromXElement<T>(T item, XElement el)
        {
            var metadata = GetCachedMetadata(typeof(T));

            foreach (var propMeta in metadata.Properties.Where(p => p.CanWrite))
            {
                // Try Element first, then Attribute
                var xmlValue = (string)el.Element(propMeta.Name) ?? (string)el.Attribute(propMeta.Name);

                if (xmlValue != null)
                {
                    try
                    {
                        // Use pre-calculated UnderlyingType
                        object value = Convert.ChangeType(xmlValue, propMeta.UnderlyingType);
                        propMeta.Property.SetValue(item, value);
                    }
                    catch
                    {
                        // Ignore conversion errors or log them
                    }
                }
            }
        }

        private static XElement MapToXElement<T>(T item, string elementName)
        {
            var el = new XElement(elementName);
            var metadata = GetCachedMetadata(typeof(T));

            foreach (var propMeta in metadata.Properties.Where(p => p.CanRead))
            {
                var value = propMeta.Property.GetValue(item);
                if (value != null)
                {
                    // Simple heuristic: Simple types -> Attributes, Complex -> Elements?
                    // For now, let's default everything to Elements for safety
                    el.Add(new XElement(propMeta.Name, value));
                }
            }
            return el;
        }
    }
}
