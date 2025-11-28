using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using SourceGeneratorUtils;

namespace SourceGeneratorUtils.Tests.Unit
{
    public class GenericXmlStreamerTests
    {
        // Test class for IXmlStreamable
        public class StreamableItem : IXmlStreamable
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public void ReadFromXml(XElement element, XmlSerializationOptions options = null)
            {
                Id = (int)element.Element("Id");
                Name = (string)element.Element("Name");
            }

            public XElement WriteToXml(XmlSerializationOptions options = null)
            {
                return new XElement("StreamableItem",
                    new XElement("Id", Id),
                    new XElement("Name", Name));
            }
        }

        // Test class for non-IXmlStreamable (uses reflection)
        public class SimpleItem
        {
            public int Value { get; set; }
            public string Text { get; set; }
        }

        #region ReadDataFromStream Tests

        [Fact]
        public void ReadDataFromStream_EmptyStream_ReturnsEmpty()
        {
            var xml = "<?xml version=\"1.0\"?><Root></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var items = GenericXmlStreamer.ReadDataFromStream<StreamableItem>(stream, itemName: "Item").ToList();

            Assert.Empty(items);
        }

        [Fact]
        public void ReadDataFromStream_SingleItem_ReturnsOne()
        {
            var xml = "<?xml version=\"1.0\"?><Root><Item><Id>1</Id><Name>Test</Name></Item></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var items = GenericXmlStreamer.ReadDataFromStream<StreamableItem>(stream, itemName: "Item").ToList();

            Assert.Single(items);
            Assert.Equal(1, items[0].Id);
            Assert.Equal("Test", items[0].Name);
        }

        [Fact]
        public void ReadDataFromStream_LargeCollection_ReadsAll()
        {
            var sb = new StringBuilder("<?xml version=\"1.0\"?><Root>");
            for (int i = 1; i <= 100; i++)
            {
                sb.Append($"<Item><Id>{i}</Id><Name>Item{i}</Name></Item>");
            }
            sb.Append("</Root>");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
            var items = GenericXmlStreamer.ReadDataFromStream<StreamableItem>(stream, itemName: "Item").ToList();

            Assert.Equal(100, items.Count);
            Assert.Equal(1, items[0].Id);
            Assert.Equal(100, items[99].Id);
        }

        [Fact]
        public void ReadDataFromStream_CustomItemName_UsesCustomName()
        {
            var xml = "<?xml version=\"1.0\"?><Root><CustomItem><Id>1</Id><Name>Test</Name></CustomItem></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var items = GenericXmlStreamer.ReadDataFromStream<StreamableItem>(stream, itemName: "CustomItem").ToList();

            Assert.Single(items);
            Assert.Equal(1, items[0].Id);
        }

        [Fact]
        public void ReadDataFromStream_WithOptions_PassesOptions()
        {
            var xml = "<?xml version=\"1.0\"?><Root><Item><Id>1</Id><Name>Test</Name></Item></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var options = new XmlSerializationOptions();

            var items = GenericXmlStreamer.ReadDataFromStream<StreamableItem>(stream, options, "Item").ToList();

            Assert.Single(items);
        }

        [Fact]
        public void ReadDataFromStream_NonIXmlStreamable_UsesReflection()
        {
            var xml = "<?xml version=\"1.0\"?><Root><SimpleItem><Value>42</Value><Text>Hello</Text></SimpleItem></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var items = GenericXmlStreamer.ReadDataFromStream<SimpleItem>(stream, itemName: "SimpleItem").ToList();

            Assert.Single(items);
            Assert.Equal(42, items[0].Value);
            Assert.Equal("Hello", items[0].Text);
        }

        #endregion

        #region WriteDataToStreamAsync Tests

        [Fact]
        public async void WriteDataToStreamAsync_EmptyCollection_WritesEmptyRoot()
        {
            var items = new List<StreamableItem>();
            using var stream = new MemoryStream();

            await GenericXmlStreamer.WriteDataToStreamAsync(stream, items, rootName: "TestRoot");
            stream.Position = 0;

            var xml = XDocument.Load(stream);
            Assert.Equal("TestRoot", xml.Root.Name.LocalName);
            Assert.Empty(xml.Root.Elements());
        }

        [Fact]
        public async void WriteDataToStreamAsync_CustomRootName_UsesCustomName()
        {
            var items = new[] { new StreamableItem { Id = 1, Name = "Test" } };
            using var stream = new MemoryStream();

            await GenericXmlStreamer.WriteDataToStreamAsync(stream, items, rootName: "CustomRoot");
            stream.Position = 0;

            var xml = XDocument.Load(stream);
            Assert.Equal("CustomRoot", xml.Root.Name.LocalName);
        }

        [Fact]
        public async void WriteDataToStreamAsync_NonIXmlStreamable_UsesReflection()
        {
            var items = new[] { new SimpleItem { Value = 10, Text = "World" } };
            using var stream = new MemoryStream();

            await GenericXmlStreamer.WriteDataToStreamAsync(stream, items, itemName: "SimpleItem");
            stream.Position = 0;

            var xml = XDocument.Load(stream);
            var item = xml.Root.Element("SimpleItem");
            Assert.NotNull(item);
            Assert.Equal("10", item.Element("Value")?.Value);
            Assert.Equal("World", item.Element("Text")?.Value);
        }

        #endregion

        #region Reflection Mapping Tests

        [Fact]
        public void Reflection_MapFromXElement_TypeConversion()
        {
            var xml = "<?xml version=\"1.0\"?><Root><SimpleItem><Value>123</Value></SimpleItem></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var items = GenericXmlStreamer.ReadDataFromStream<SimpleItem>(stream, itemName: "SimpleItem").ToList();

            Assert.Single(items);
            Assert.Equal(123, items[0].Value);
            Assert.IsType<int>(items[0].Value);
        }

        [Fact]
        public void Reflection_MapFromXElement_NullValues_IgnoresMissing()
        {
            var xml = "<?xml version=\"1.0\"?><Root><SimpleItem><Value>5</Value></SimpleItem></Root>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var items = GenericXmlStreamer.ReadDataFromStream<SimpleItem>(stream, itemName: "SimpleItem").ToList();

            Assert.Single(items);
            Assert.Equal(5, items[0].Value);
            Assert.Null(items[0].Text); // Missing element
        }

        [Fact]
        public async void Reflection_MapToXElement_NullProperties_OmitsNulls()
        {
            var items = new[] { new SimpleItem { Value = 7, Text = null } };
            using var stream = new MemoryStream();

            await GenericXmlStreamer.WriteDataToStreamAsync(stream, items, itemName: "SimpleItem");
            stream.Position = 0;

            var xml = XDocument.Load(stream);
            var item = xml.Root.Element("SimpleItem");
            Assert.NotNull(item.Element("Value"));
            Assert.Null(item.Element("Text")); // Null property omitted
        }

        #endregion
    }
}
