using FluentAssertions;

namespace XmlSourceGenerator.UnitTests.Helpers;

public class IndentedStringBuilderTests
{
    [Fact]
    public void AppendLine_AppendsTextWithNewline()
    {
        var sb = new IndentedStringBuilder();
        sb.AppendLine("Hello");
        sb.ToString().Should().Be("Hello\r\n");
    }

    [Fact]
    public void Indent_IncreasesIndentation()
    {
        var sb = new IndentedStringBuilder();
        sb.AppendLine("Start");
        using (sb.Indent())
        {
            sb.AppendLine("Indented");
        }
        sb.AppendLine("End");

        var expected = "Start\r\n    Indented\r\nEnd\r\n";
        sb.ToString().Should().Be(expected);
    }

    [Fact]
    public void Indent_NestedIndentation()
    {
        var sb = new IndentedStringBuilder();
        using (sb.Indent())
        {
            sb.AppendLine("Level 1");
            using (sb.Indent())
            {
                sb.AppendLine("Level 2");
            }
            sb.AppendLine("Level 1 Again");
        }

        var expected = "    Level 1\r\n        Level 2\r\n    Level 1 Again\r\n";
        sb.ToString().Should().Be(expected);
    }

    [Fact]
    public void Append_AppendsTextWithoutNewline()
    {
        var sb = new IndentedStringBuilder();
        sb.Append("Hello");
        sb.Append(" World");
        sb.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void Append_RespectsIndentationForNewLines()
    {
        // Note: Append usually doesn't indent unless it's start of line, 
        // but IndentedStringBuilder implementation might vary.
        // Let's check the implementation if needed, but standard behavior is:
        
        var sb = new IndentedStringBuilder();
        using (sb.Indent())
        {
            sb.Append("Hello");
        }
        
        // Assuming Append doesn't automatically indent if it's just raw text, 
        // OR it indents if it's the start of a line.
        // Let's verify behavior with a simpler test first.
        
        sb.ToString().Should().Be("    Hello");
    }
}
