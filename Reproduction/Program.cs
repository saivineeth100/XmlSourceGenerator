using System.Xml.Linq;
using XmlSourceGenerator.Abstractions;

namespace Reproduction
{
    public class SimpleItem
    {
        public int Value { get; set; }
        public string? Text { get; set; }
        public byte Byte { get; set; }
        public sbyte SByte { get; set; }
    }

    public static class Program
    {
        public static async Task Main()
        {
            try
            {
                var items = new[] { new SimpleItem { Value = 7, Text = null, Byte = 255, SByte = -127 } };
                using var stream = new MemoryStream();

                Console.WriteLine("Writing to stream...");
                await GenericXmlStreamer.WriteEnumerableDataToStreamAsync(stream, items, itemName: "SimpleItem");

                Console.WriteLine("Stream Length: " + stream.Length);
                stream.Position = 0;
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, /*net48-compat*/detectEncodingFromByteOrderMarks: true, /*net48-compat*/bufferSize: -1, leaveOpen: true);
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
}
