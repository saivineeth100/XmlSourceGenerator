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
                reader.MoveToContent();

                while (reader.Read())
                {
                    // Look for the start element matching our target type
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == targetName)
                    {
                        // 1. Materialize one node
                        XElement el = (XElement)XNode.ReadFrom(reader);

                        // 2. Map to T
                        T item = new T();

                        if (item is IXmlStreamable streamable)
                        {
                            // Fast path: Manual mapping
                            streamable.ReadFromXml(el, options);
                        }
                        else
                        {
                            // Slow path: Reflection mapping
                            MapFromXElement(item, el);
                        }

                        yield return item;
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
        private static void MapFromXElement<T>(T item, XElement el)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.CanWrite);

            foreach (var prop in properties)
            {
                // Try Element first, then Attribute
                var xmlValue = (string)el.Element(prop.Name) ?? (string)el.Attribute(prop.Name);

                if (xmlValue != null)
                {
                    try
                    {
                        // Basic type conversion
                        object value = Convert.ChangeType(xmlValue, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                        prop.SetValue(item, value);
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
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.CanRead);

            foreach (var prop in properties)
            {
                var value = prop.GetValue(item);
                if (value != null)
                {
                    // Simple heuristic: Simple types -> Attributes, Complex -> Elements?
                    // For now, let's default everything to Elements for safety
                    el.Add(new XElement(prop.Name, value));
                }
            }
            return el;
        }
    }
}
