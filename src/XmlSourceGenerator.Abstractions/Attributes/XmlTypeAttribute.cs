using System;

namespace SourceGeneratorUtils
{
    /// <summary>
    /// Specifies the type name to use in XML for a class, typically used with polymorphism.
    /// This is useful when the XML element name differs from the class name.
    /// 
    /// Example:
    /// <code>
    /// [XmlType("Customer")]
    /// public class CustomerType { }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class XmlTypeAttribute : Attribute
    {
        public string TypeName { get; }
        public string? Namespace { get; set; }

        public XmlTypeAttribute(string typeName)
        {
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        }
    }
}
