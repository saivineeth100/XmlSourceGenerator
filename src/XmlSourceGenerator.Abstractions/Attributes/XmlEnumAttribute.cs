using System;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Customizes the XML value for an enum member.
    /// </summary>
    /// <example>
    /// <code>
    /// public enum Status
    /// {
    ///     [XmlEnum("active")]
    ///     Active,
    ///     [XmlEnum("inactive")]
    ///     Inactive,
    ///     [XmlEnum("pending")]
    ///     Pending
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class XmlEnumAttribute : Attribute
    {
        /// <summary>
        /// Gets the XML representation of the enum value.
        /// </summary>
        public string Name { get; }

        public XmlEnumAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
