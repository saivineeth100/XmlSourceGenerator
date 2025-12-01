namespace XmlSourceGenerator.Abstractions
{
    /// <summary>
    /// Configuration settings for a specific property during XML serialization.
    /// </summary>
    public class XmlPropertySettings
    {
        /// <summary>
        /// The XML element name to use for this property.
        /// If null, the default name (or other strategies) will be used.
        /// </summary>
        public string? XmlName { get; set; }

        /// <summary>
        /// Polymorphic type mappings for this property.
        /// List of (Type, ElementName) tuples.
        /// </summary>
        public List<(Type Type, string Name)>? PolymorphicMappings { get; set; }
    }
}
