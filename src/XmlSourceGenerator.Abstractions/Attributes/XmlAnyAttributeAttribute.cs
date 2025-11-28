using System;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Indicates that a property should capture any XML attributes not mapped to other properties.
    /// The property must be of type List&lt;XAttribute&gt;.
    /// 
    /// Example:
    /// <code>
    /// [XmlAnyAttribute]
    /// public List&lt;XAttribute&gt; ExtraAttributes { get; set; }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class XmlAnyAttributeAttribute : Attribute
    {
    }
}
