using System;
using System.Text;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Base class for naming policies used to convert property names to XML element names.
    /// </summary>
    public abstract class XmlNamingPolicy
    {
        /// <summary>
        /// Returns the naming policy for camel-casing.
        /// </summary>
        public static XmlNamingPolicy CamelCase { get; } = new CamelCaseXmlNamingPolicy();

        /// <summary>
        /// Returns the naming policy for snake_casing.
        /// </summary>
        public static XmlNamingPolicy SnakeCase { get; } = new SnakeCaseXmlNamingPolicy();

        /// <summary>
        /// Converts the specified name according to the policy.
        /// </summary>
        /// <param name="name">The name to convert.</param>
        /// <returns>The converted name.</returns>
        public abstract string ConvertName(string name);
    }

    internal class CamelCaseXmlNamingPolicy : XmlNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
                return name;

            char[] chars = name.ToCharArray();
            
            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                bool hasNext = (i + 1 < chars.Length);
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    // if the next character is a space, which is not considered uppercase 
                    // (otherwise we wouldn't be here...)
                    // we want to ensure that the following:
                    // 'FOOBar' is rewritten as 'fooBar', and not as 'foOBar'
                    // The code was written in such a way that the first word in uppercase
                    // ends when if finds an uppercase letter followed by a lowercase letter.
                    if (char.IsSeparator(chars[i + 1]))
                    {
                        chars[i] = char.ToLowerInvariant(chars[i]);
                    }
                    break;
                }

                chars[i] = char.ToLowerInvariant(chars[i]);
            }

            return new string(chars);
        }
    }

    internal class SnakeCaseXmlNamingPolicy : XmlNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
