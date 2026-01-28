using System.Xml;
using System.Xml.Linq;

namespace XmlSourceGenerator.Abstractions
{
    public class User
    {
        public string Name { get; set; } = string.Empty;
        public int? Age { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class XmlStreamProcessor
    {
        // ---------------------------------------------------------
        // READING: Stream -> IEnumerable<User>
        // Use this with: await client.GetStreamAsync(...)
        // ---------------------------------------------------------
        public static IEnumerable<User> ReadUsersFromStream(Stream stream)
        {
            // We do NOT use 'using' on the stream here because we don't own the stream's lifetime
            // (The caller, e.g., HttpClient, owns it).
            
            var settings = new XmlReaderSettings { Async = true }; // Async is good practice even if we sync read here
            using (var reader = XmlReader.Create(stream, settings))
            {
                reader.MoveToContent(); // Skip BOM / whitespace

                while (reader.Read())
                {
                    // Fast scan until we hit a <User> start tag
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "User")
                    {
                        // 1. Materialize ONLY this node into XElement (DOM)
                        // This loads just ~1KB into RAM, not the whole 1GB file
                        XElement el = (XElement)XNode.ReadFrom(reader);

                        // 2. Map XElement -> Object using nice LINQ
                        yield return new User
                        {
                            Name = (string)el.Element("Name"),
                            Age = (int?)el.Element("Age"),
                            Role = (string)el.Attribute("Role")
                        };
                        
                        // 'el' goes out of scope here and is eligible for GC
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // WRITING: IEnumerable<User> -> Stream
        // Use this with: new PushStreamContent(...) or similar streaming content
        // ---------------------------------------------------------
        public static async Task WriteUsersToStreamAsync(Stream stream, IEnumerable<User> users)
        {
            var settings = new XmlWriterSettings 
            { 
                Async = true, 
                Indent = false // False for performance/bandwidth
            };

            using (var writer = XmlWriter.Create(stream, settings))
            {
                await writer.WriteStartDocumentAsync();
                await writer.WriteStartElementAsync(null, "Users", null);

                foreach (var user in users)
                {
                    // Hybrid Approach: Create XElement for one item, then write it
                    // This is cleaner than manual writer.WriteStartElement calls
                    var el = new XElement("User",
                        new XAttribute("Role", user.Role ?? ""),
                        new XElement("Name", user.Name),
                        user.Age.HasValue ? new XElement("Age", user.Age) : null
                    );

                    // Efficiently write the XElement to the stream
                    el.WriteTo(writer);
                }

                await writer.WriteEndElementAsync(); // </Users>
                await writer.WriteEndDocumentAsync();
            }
        }
    }
}
