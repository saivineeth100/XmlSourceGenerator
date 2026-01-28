namespace XmlSourceGenerator.Abstractions
{
    /// <summary>
    /// Options for configuring XML serialization behavior.
    /// </summary>
    public class XmlSerializationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether XML output should be indented.
        /// Default is false (minified).
        /// </summary>
        public bool WriteIndented { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the encoding to use for XML output.
        /// Default is UTF-8 if null.
        /// </summary>
        public System.Text.Encoding? Encoding { get; set; }

        /// <summary>
        /// Gets or sets the naming policy used to convert property names to XML element names.
        /// If null, property names are used as-is.
        /// </summary>
        public XmlNamingPolicy? PropertyNamingPolicy { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether options should take precedence over attributes.
        /// If true, PropertySettings will be checked even if [XmlElement] or other attributes are present.
        /// Default is false.
        /// </summary>
        public bool PreferOptionsOverAttributes { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether parsing errors should be ignored.
        /// If true, items with invalid XML or type conversion errors will be skipped.
        /// Default is false.
        /// </summary>
        public bool IgnoreParsingErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether null values should be ignored.
        /// If true, nullable properties with null values will be omitted from the XML.
        /// If false (default), they may be emitted with xsi:nil="true".
        /// </summary>
        public bool IgnoreNullValues { get; set; } = false;

        /// <summary>
        /// Dictionary of specific property settings.
        /// Key: (Type of the class, Property Name)
        /// Value: The settings for that property
        /// </summary>
        public Dictionary<(Type, string), XmlPropertySettings> PropertySettings { get; } = new Dictionary<(Type, string), XmlPropertySettings>();

        /// <summary>
        /// Legacy dictionary for simple name overrides.
        /// Kept for backward compatibility, but PropertySettings is preferred.
        /// </summary>
        public Dictionary<(Type, string), string> PropertyOverrides { get; } = new Dictionary<(Type, string), string>();

        /// <summary>
        /// Resolves the XML element name for a given property.
        /// Priority:
        /// 1. PropertySettings.XmlName (if present)
        /// 2. Explicit Override in PropertyOverrides
        /// 3. PropertyNamingPolicy (if set)
        /// 4. Original Property Name
        /// </summary>
        public string GetXmlName(Type type, string propertyName)
        {
            // Check PropertySettings first
            if (PropertySettings.TryGetValue((type, propertyName), out var settings) && !string.IsNullOrEmpty(settings.XmlName))
            {
                return settings.XmlName!;
            }

            // Fallback to legacy overrides
            if (PropertyOverrides.TryGetValue((type, propertyName), out string overrideName))
            {
                return overrideName;
            }

            if (PropertyNamingPolicy != null)
            {
                return PropertyNamingPolicy.ConvertName(propertyName);
            }

            return propertyName;
        }

        /// <summary>
        /// Helper to get the override name for a property, if any.
        /// Returns null if no override is configured.
        /// </summary>
        public string? GetOverride(Type type, string propertyName)
        {
            // Check PropertySettings first
            if (PropertySettings.TryGetValue((type, propertyName), out var settings) && !string.IsNullOrEmpty(settings.XmlName))
            {
                return settings.XmlName;
            }

            // Fallback to legacy overrides
            if (PropertyOverrides.TryGetValue((type, propertyName), out string overrideName))
            {
                return overrideName;
            }

            return null;
        }

        /// <summary>
        /// Helper to get polymorphic mappings for a property.
        /// </summary>
        public List<(Type Type, string Name)>? GetPolymorphicMappings(Type type, string propertyName)
        {
            if (PropertySettings.TryGetValue((type, propertyName), out var settings))
            {
                return settings.PolymorphicMappings;
            }
            return null;
        }
    }
}
