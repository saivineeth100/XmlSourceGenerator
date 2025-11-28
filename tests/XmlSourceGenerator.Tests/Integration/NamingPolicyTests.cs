using System;
using Xunit;
using SourceGeneratorUtils;

namespace SourceGeneratorUtils.Tests.Integration
{
    public class NamingPolicyTests
    {
        [Theory]
        [InlineData("PascalCase", "pascalCase")]
        [InlineData("Name", "name")]
        [InlineData("HTTPServer", "httpServer")]
        [InlineData("XMLParser", "xmlParser")]
        [InlineData("IOStream", "ioStream")]
        public void TestCamelCase(string input, string expected)
        {
            var policy = XmlNamingPolicy.CamelCase;
            var result = policy.ConvertName(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("PascalCase", "pascal_case")]
        [InlineData("Name", "name")]
        [InlineData("HTTPServer", "h_t_t_p_server")]
        [InlineData("XMLParser", "x_m_l_parser")]
        public void TestSnakeCase(string input, string expected)
        {
            var policy = XmlNamingPolicy.SnakeCase;
            var result = policy.ConvertName(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestNullInput()
        {
            Assert.Null(XmlNamingPolicy.CamelCase.ConvertName(null));
            Assert.Null(XmlNamingPolicy.SnakeCase.ConvertName(null));
        }

        [Fact]
        public void TestEmptyInput()
        {
            Assert.Equal("", XmlNamingPolicy.CamelCase.ConvertName(""));
            Assert.Equal("", XmlNamingPolicy.SnakeCase.ConvertName(""));
        }
    }
}
