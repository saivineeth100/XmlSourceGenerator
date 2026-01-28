using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace XmlSourceGenerator.Abstractions
{
    /// <summary>
    /// Optional interface for high-performance manual mapping.
    /// If T implements this, Reflection is skipped.
    /// </summary>
    public interface IXmlStreamable
    {
        string DefaultXmlRootElementName { get; }

        void ReadFromXml(XElement element, XmlSerializationOptions? options = null);
        XElement WriteToXml(XmlSerializationOptions? options = null);
    }

    public static class GenericXmlStreamer
    {
        // ---------------------------------------------------------
        // GENERIC READ: Stream -> IEnumerable<T>
        // ---------------------------------------------------------
        public static IEnumerable<T> ReadListDataFromStream<T>(Stream stream, XmlSerializationOptions? options = null, string? itemName = null) where T : new()
        {
            var settings = new XmlReaderSettings { Async = true };
            using (var reader = XmlReader.Create(stream, settings))
            {
                foreach (var item in ReadListDataFromReader<T>(reader, options, itemName))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ReadListDataFromTextReader<T>(TextReader textReader, XmlSerializationOptions? options = null, string? itemName = null) where T : new()
        {
            var settings = new XmlReaderSettings { Async = true };
            using (var reader = XmlReader.Create(textReader, settings))
            {
                foreach (var item in ReadListDataFromReader<T>(reader, options, itemName))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ReadNestedListDataFromTextReader<T>(TextReader textReader, string[] path, XmlSerializationOptions? options = null, string? itemName = null) where T : new()
        {
            var settings = new XmlReaderSettings { Async = true };
            using (var reader = XmlReader.Create(textReader, settings))
            {
                // Navigate to the target path
                bool found = true;
                foreach (var nodeName in path)
                {
                    found = false;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == nodeName)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) break; 
                }

                if (found)
                {
                    // If we found the container, read the list inside it
                    // We need to move inside the container
                    // Currently reader is at <Objects>. 
                    // ReadListDataFromReader expects to find <T> elements.
                    // It will call MoveToContent() which stays on <Objects> if it's content? No.
                    // ReadListDataFromReader loop calls reader.Read() if Name != targetName.
                    // So if we pass the reader positioned at <Objects>, it will read next and find <T>.
                    
                    // Use ReadSubtree to limit scope to the container
                    using (var subReader = reader.ReadSubtree())
                    {
                        // subReader is at <Objects> (Initial)
                        subReader.Read(); // Move to <Objects> element
                        
                        foreach (var item in ReadListDataFromReader<T>(subReader, options, itemName))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        private static IEnumerable<T> ReadListDataFromReader<T>(XmlReader reader, XmlSerializationOptions? options, string? itemName) where T : new()
        {
            // Default item name to class name if not provided
            // Default item name to class name if not provided
            string targetName = GetRootName<T>(itemName);

            // Skip to first content
            reader.MoveToContent();

            // Read through all elements
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == targetName)
                {
                    T item = default;
                    try
                    {
                        item = ParseItem<T>(reader, options);
                    }
                    catch
                    {
                       if (options?.IgnoreParsingErrors == true)
                       {
                           // Skip item
                           item = default;
                           // Ensure reader advances if ParseItem failed?
                           // If XElement.Load failed, we might be in trouble.
                           // But if conversion failed, reader is safely at EndElement.
                       }
                       else throw;
                    }

                    if (item != null) yield return item;

                    // ReadSubtree positions reader AT the element, so we need to skip it/move past.
                    reader.Read();
                }
                else
                {
                    reader.Read(); // Move to next node
                }
            }
        }

        // ---------------------------------------------------------
        // GENERIC READ: Stream -> T (Single Item)
        // ---------------------------------------------------------
        public static T? ReadDataFromStream<T>(Stream stream, XmlSerializationOptions? options = null, string? itemName = null) where T : new()
        {
            try
            {
                // Default item name to class name if not provided
                string targetName = GetRootName<T>(itemName);

                var settings = new XmlReaderSettings { Async = true };
                using (var reader = XmlReader.Create(stream, settings))
                {
                    reader.MoveToContent();

                    // If root matches target, read it directly
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == targetName)
                    {
                        return ParseItem<T>(reader, options);
                    }
                }
            }
            catch
            {
                // return default or throw?
            }
            return default;
        }

        // ---------------------------------------------------------
        // GENERIC WRITE: IEnumerable<T> -> Stream
        // ---------------------------------------------------------
        public static async Task WriteDataToStreamAsync<T>(Stream stream, IEnumerable<T> items, XmlSerializationOptions? options = null, string rootName = "ArrayOfItems", string? itemName = null)
        {
            string targetItemName = GetRootName<T>(itemName);

            var settings = new XmlWriterSettings 
            { 
                Async = true, 
                Indent = options?.WriteIndented ?? false,
                Encoding = options?.Encoding ?? System.Text.Encoding.UTF8,
                CloseOutput = false
            };

            using (var writer = XmlWriter.Create(stream, settings))
            {
                await writer.WriteStartDocumentAsync();
                
                // If rootName is null (e.g. single item write which handles its own root), handle it?
                // WriteDataToStreamAsync(IEnumerable) implies a root container.
                await writer.WriteStartElementAsync(null, rootName, null);

                foreach (var item in items)
                {
                    WriteItem(writer, item, targetItemName, options);
                }

                await writer.WriteEndElementAsync();
                await writer.WriteEndDocumentAsync();
                await writer.FlushAsync();
            }
        }

        public static async Task WriteDataToStreamAsync<T>(Stream stream, T item, XmlSerializationOptions? options = null, string? rootName = null, string? itemName = null)
        {
            string actualRootName = rootName ?? itemName;

            var settings = new XmlWriterSettings
            {
                Async = true,
                Indent = options?.WriteIndented ?? false,
                Encoding = options?.Encoding ?? System.Text.Encoding.UTF8,
                CloseOutput = false
            };

            using (var writer = XmlWriter.Create(stream, settings))
            {
                await writer.WriteStartDocumentAsync();
                
                WriteItem(writer, item, actualRootName, options);

                await writer.WriteEndDocumentAsync();
                await writer.FlushAsync();
            }
        }

        // ---------------------------------------------------------
        // REFLECTION HELPERS (The "Dynamic" Part)
        // ---------------------------------------------------------
        


        private static T ParseItem<T>(XmlReader reader, XmlSerializationOptions? options) where T : new()
        {
            using (XmlReader subtree = reader.ReadSubtree())
            {
                subtree.MoveToContent();
                XElement el = XElement.Load(subtree);

                T item = new();
                if (item is IXmlStreamable streamable)
                {
                    streamable.ReadFromXml(el, options);
                }
                else
                {
                    MapFromXElement(item, el);
                }
                return item;
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
                 el = MapToXElement(item, itemName ?? item.GetType().Name);
             }

             el.WriteTo(writer);
        }

        private static void MapFromXElement<T>(T item, XElement el)
        {
            var metadata = ReflectionHelper.GetCachedMetadata(typeof(T));

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
            var metadata = ReflectionHelper.GetCachedMetadata(typeof(T));

            foreach (var propMeta in metadata.Properties.Where(p => p.CanRead))
            {
                if (propMeta.IsIgnored) continue;

                var value = propMeta.Property.GetValue(item);
                if (value != null)
                {
                    if (propMeta.IsAttribute && ReflectionHelper.IsSimpleType(propMeta.PropertyType))
                    {
                        el.Add(new XAttribute(propMeta.XmlName, ReflectionHelper.FormatValue(value, propMeta.PropertyType)));
                    }
                    else
                    {
                        el.Add(new XElement(propMeta.XmlName, value));
                    }
                }
            }
            return el;
        }
        private static string GetRootName<T>(string? itemName)
        {
            if (!string.IsNullOrEmpty(itemName)) return itemName!;
            
            if (typeof(IXmlStreamable).IsAssignableFrom(typeof(T)) && !typeof(T).IsAbstract && !typeof(T).IsInterface)
            {
                try
                {
                    // Use Activator to create instance since we don't have new() constraint
                    var instance = (IXmlStreamable)Activator.CreateInstance(typeof(T));
                    if (instance != null) return instance.DefaultXmlRootElementName;
                }
                catch { }
            }

            return ReflectionHelper.GetCachedMetadata(typeof(T)).RootName;
        }
    }
}
