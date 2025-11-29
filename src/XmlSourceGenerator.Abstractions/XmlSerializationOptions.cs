using System;
using System.Collections.Generic;

namespace SourceGeneratorUtils
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
        /// Gets or sets the naming policy used to convert property names to XML element names.
        /// If null, property names are used as-is.
        /// </summary>
        public XmlNamingPolicy PropertyNamingPolicy { get; set; }

        /// <summary>
        /// Dictionary of specific property overrides.
        /// Key: (Type of the class, Property Name)
        /// Value: The desired XML Element Name
        /// </summary>
        public Dictionary<(Type, string), string> PropertyOverrides { get; } = new Dictionary<(Type, string), string>();

        /// <summary>
        /// Resolves the XML element name for a given property.
        /// Priority:
        /// 1. Explicit Override in PropertyOverrides
        /// 2. PropertyNamingPolicy (if set)
        /// 3. Original Property Name
        /// </summary>
        public string GetXmlName(Type type, string propertyName)
        {
            // Fast path: if no overrides and no policy, return original name
            // This avoids tuple allocation and dictionary lookup for the common case
            if (PropertyOverrides.Count == 0 && PropertyNamingPolicy == null)
            {
                return propertyName;
            }

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
    }
}
