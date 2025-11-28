using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using SourceGeneratorUtils;

namespace SourceGeneratorUtils.Tests.Integration
{
    /// <summary>
    /// Manual implementation of IXmlStreamable without using the generator.
    /// This validates the interface contract itself.
    /// </summary>
    public class ManualUser : IXmlStreamable
    {
        public int UserId { get; set; }
        public string Username { get; set; }

        public void ReadFromXml(XElement element, XmlSerializationOptions options = null)
        {
            var userIdName = options?.GetXmlName(typeof(ManualUser), "UserId") ?? "UserId";
            var usernameName = options?.GetXmlName(typeof(ManualUser), "Username") ?? "Username";

            UserId = (int)element.Element(userIdName);
            Username = (string)element.Element(usernameName);
        }

        public XElement WriteToXml(XmlSerializationOptions options = null)
        {
            var userIdName = options?.GetXmlName(typeof(ManualUser), "UserId") ?? "UserId";
            var usernameName = options?.GetXmlName(typeof(ManualUser), "Username") ?? "Username";

            return new XElement("ManualUser",
                new XElement(userIdName, UserId),
                new XElement(usernameName, Username));
        }
    }

    public class ManualImplementationTests
    {
        [Fact]
        public void TestManualImplementation_Basic()
        {
            var user = new ManualUser { UserId = 42, Username = "admin" };
            var xml = user.WriteToXml();

            var restored = new ManualUser();
            restored.ReadFromXml(xml);

            Assert.Equal(42, restored.UserId);
            Assert.Equal("admin", restored.Username);
        }

        [Fact]
        public void TestManualImplementation_WithOptions()
        {
            var options = new XmlSerializationOptions();
            options.PropertyOverrides[(typeof(ManualUser), "Username")] = "UserName";

            var xml = new XElement("ManualUser",
                new XElement("UserId", 100),
                new XElement("UserName", "testuser"));

            var user = new ManualUser();
            user.ReadFromXml(xml, options);

            Assert.Equal(100, user.UserId);
            Assert.Equal("testuser", user.Username);
        }

        [Fact]
        public void TestManualImplementation_WithStreamer()
        {
            var users = new[] 
            { 
                new ManualUser { UserId = 1, Username = "user1" },
                new ManualUser { UserId = 2, Username = "user2" }
            };
           
            using var stream = new MemoryStream();
            GenericXmlStreamer.WriteDataToStreamAsync(stream, users).Wait();
            // Test Writtten XML
            string xml = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains("﻿<?xml version=\"1.0\" encoding=\"utf-8\"?><ArrayOfItems><ManualUser><UserId>1</UserId><Username>user1</Username></ManualUser><ManualUser><UserId>2</UserId><Username>user2</Username></ManualUser></ArrayOfItems>", xml);
            stream.Position = 0;
            var restored = GenericXmlStreamer.ReadDataFromStream<ManualUser>(stream).ToList();

            Assert.Equal(2, restored.Count);
            Assert.Equal(1, restored[0].UserId);
            Assert.Equal("user1", restored[0].Username);
            Assert.Equal(2, restored[1].UserId);
            Assert.Equal("user2", restored[1].Username);
        }
    }
}
