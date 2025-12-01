using System;
using Xunit;
using XmlSourceGenerator;

namespace XmlSourceGenerator.Tests.Unit
{
    public class IndentedStringBuilderTests
    {
        [Fact]
        public void IncrementIndent_IncreasesIndentLevel()
        {
            var sb = new IndentedStringBuilder();
            sb.IncrementIndent();
            sb.AppendLine("Test");

            var result = sb.ToString();

            Assert.StartsWith("    Test", result); // Default indent is 4 spaces
        }

        [Fact]
        public void DecrementIndent_DecreasesIndentLevel()
        {
            var sb = new IndentedStringBuilder();
            sb.IncrementIndent();
            sb.IncrementIndent();
            sb.DecrementIndent();
            sb.AppendLine("Test");

            var result = sb.ToString();

            Assert.StartsWith("    Test", result); // One level of indent
        }

        [Fact]
        public void DecrementIndent_AtZero_StaysAtZero()
        {
            var sb = new IndentedStringBuilder();
            sb.DecrementIndent(); // Should not go negative
            sb.AppendLine("Test");

            var result = sb.ToString();

            Assert.StartsWith("Test", result); // No indent
        }

        [Fact]
        public void Indent_WithUsingBlock_AutoDecrements()
        {
            var sb = new IndentedStringBuilder();
            
            using (sb.Indent())
            {
                sb.AppendLine("Indented");
            }
            sb.AppendLine("Not Indented");

            var result = sb.ToString();
            var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.StartsWith("    Indented", lines[0]);
            Assert.StartsWith("Not Indented", lines[1]);
        }

        [Fact]
        public void AppendLine_AppliesIndentation()
        {
            var sb = new IndentedStringBuilder();
            sb.IncrementIndent();
            sb.AppendLine("Line1");
            sb.AppendLine();
            sb.AppendLine("Line2");

            var result = sb.ToString();

            Assert.Contains("    Line1", result);
            Assert.Contains("    Line2", result);
        }

        [Fact]
        public void AppendFormat_AppliesIndentation()
        {
            var sb = new IndentedStringBuilder();
            sb.IncrementIndent();
            sb.AppendFormat("Value: {0}", 42);

            var result = sb.ToString();

            Assert.Equal("    Value: 42", result);
        }

        [Fact]
        public void MultipleIndentLevels_NestedCorrectly()
        {
            var sb = new IndentedStringBuilder();
            
            sb.AppendLine("Level 0");
            using (sb.Indent())
            {
                sb.AppendLine("Level 1");
                using (sb.Indent())
                {
                    sb.AppendLine("Level 2");
                }
                sb.AppendLine("Back to Level 1");
            }
            sb.AppendLine("Back to Level 0");

            var result = sb.ToString();
            var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Level 0", lines[0]);
            Assert.Equal("    Level 1", lines[1]);
            Assert.Equal("        Level 2", lines[2]);
            Assert.Equal("    Back to Level 1", lines[3]);
            Assert.Equal("Back to Level 0", lines[4]);
        }

        [Fact]
        public void Append_DoesNotAddNewLine()
        {
            var sb = new IndentedStringBuilder();
            sb.Append("Part1");
            sb.Append("Part2");

            var result = sb.ToString();

            Assert.Equal("Part1Part2", result);
        }
    }
}
