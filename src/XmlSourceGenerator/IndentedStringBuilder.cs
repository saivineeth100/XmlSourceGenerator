using System;
using System.Text;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// A wrapper around StringBuilder that handles indentation automatically.
    /// </summary>
    public class IndentedStringBuilder
    {
        private readonly StringBuilder _sb;
        private int _indentLevel;
        private readonly string _indentString;
        private bool _indentPending = true;

        public IndentedStringBuilder(string indentString = "    ")
        {
            _sb = new StringBuilder();
            _indentString = indentString;
        }

        public IndentedStringBuilder(StringBuilder sb, string indentString = "    ")
        {
            _sb = sb;
            _indentString = indentString;
        }

        /// <summary>
        /// Increases the indentation level.
        /// </summary>
        public void IncrementIndent()
        {
            _indentLevel++;
        }

        /// <summary>
        /// Decreases the indentation level.
        /// </summary>
        public void DecrementIndent()
        {
            if (_indentLevel > 0)
            {
                _indentLevel--;
            }
        }

        /// <summary>
        /// Creates a scope that increments indentation and decrements it when disposed.
        /// Usage: using (sb.Indent()) { ... }
        /// </summary>
        public IDisposable Indent()
        {
            IncrementIndent();
            return new Indenter(this);
        }

        /// <summary>
        /// Appends text. If the current line is empty, indentation is added first.
        /// </summary>
        public void Append(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            // If we are at the start of a line, append indentation
            if (_indentPending)
            {
                AppendIndent();
            }

            _sb.Append(value);
        }

        /// <summary>
        /// Appends a line of text with indentation.
        /// </summary>
        public void AppendLine(string value = "")
        {
            if (value.Length > 0)
            {
                if (_indentPending)
                {
                    AppendIndent();
                }
                _sb.AppendLine(value);
            }
            else
            {
                _sb.AppendLine();
            }

            _indentPending = true;
        }

        /// <summary>
        /// Appends formatted text.
        /// </summary>
        public void AppendFormat(string format, params object[] args)
        {
            if (_indentPending)
            {
                AppendIndent();
            }
            _sb.AppendFormat(format, args);
        }

        private void AppendIndent()
        {
            for (int i = 0; i < _indentLevel; i++)
            {
                _sb.Append(_indentString);
            }
            _indentPending = false;
        }

        public override string ToString() => _sb.ToString();

        private class Indenter : IDisposable
        {
            private readonly IndentedStringBuilder _parent;

            public Indenter(IndentedStringBuilder parent)
            {
                _parent = parent;
            }

            public void Dispose()
            {
                _parent.DecrementIndent();
            }
        }
    }
}
