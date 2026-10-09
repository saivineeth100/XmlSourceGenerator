using System.Diagnostics.CodeAnalysis;

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
        public List<PolymorphicMapping>? PolymorphicMappings { get; set; }

    }

    public record class PolymorphicMapping(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type Type,
        string Name
    )
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type Type { get; } = Type;
        public string Name { get; } = Name;
    };
}
