using System;
using Xunit;
using SourceGeneratorUtils;

namespace SourceGeneratorUtils.Tests.Unit
{
    public class XmlSerializationOptionsTests
    {
        [Fact]
        public void GetXmlName_WithOverride_ReturnsOverride()
        {
            var options = new XmlSerializationOptions();
            options.PropertyOverrides[(typeof(TestClass), "PropertyName")] = "CustomName";

            var result = options.GetXmlName(typeof(TestClass), "PropertyName");

            Assert.Equal("CustomName", result);
        }

        [Fact]
        public void GetXmlName_WithPolicy_AppliesPolicy()
        {
            var options = new XmlSerializationOptions
            {
                PropertyNamingPolicy = XmlNamingPolicy.CamelCase
            };

            var result = options.GetXmlName(typeof(TestClass), "PropertyName");

            Assert.Equal("propertyName", result);
        }

        [Fact]
        public void GetXmlName_DefaultBehavior_ReturnsPropertyName()
        {
            var options = new XmlSerializationOptions();

            var result = options.GetXmlName(typeof(TestClass), "PropertyName");

            Assert.Equal("PropertyName", result);
        }

        [Fact]
        public void GetXmlName_OverrideTakesPrecedence_OverPolicy()
        {
            var options = new XmlSerializationOptions
            {
                PropertyNamingPolicy = XmlNamingPolicy.CamelCase
            };
            options.PropertyOverrides[(typeof(TestClass), "PropertyName")] = "SpecificOverride";

            var result = options.GetXmlName(typeof(TestClass), "PropertyName");

            // Override should win over policy
            Assert.Equal("SpecificOverride", result);
        }

        [Fact]
        public void WriteIndented_DefaultValue_IsFalse()
        {
            var options = new XmlSerializationOptions();

            Assert.False(options.WriteIndented);
        }

        [Fact]
        public void PropertyOverrides_IsInitialized_NotNull()
        {
            var options = new XmlSerializationOptions();

            Assert.NotNull(options.PropertyOverrides);
        }

        private class TestClass
        {
            public string PropertyName { get; set; }
        }
    }
}
