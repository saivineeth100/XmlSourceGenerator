using System;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Indicates that a property should capture any XML elements not mapped to other properties.
    /// The property must be of type List&lt;XElement&gt;.
    /// 
    /// Example:
    /// <code>
    /// [XmlAnyElement]
    /// public List&lt;XElement&gt; ExtraElements { get; set; }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class XmlAnyElementAttribute : Attribute
    {
    }
}
