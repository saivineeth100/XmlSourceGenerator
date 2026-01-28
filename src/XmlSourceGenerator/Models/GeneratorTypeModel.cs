using System.Collections.Generic;

namespace XmlSourceGenerator.Models
{
    public class GeneratorTypeModel
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string FullName { get; set; }
        public bool IsEnum { get; set; }
        public bool IsReferenceType { get; set; }
        public bool IsString { get; set; }
        public bool ImplementsIXmlStreamable { get; set; }
        public Dictionary<string, string> EnumMapping { get; set; } = new Dictionary<string, string>();
        public PropertyKind Kind { get; set; }
        
        public GeneratorPropertyModel[] Properties { get; set; } = [];

        public GeneratorTypeModel(string name, string ns, string fullName, bool isEnum, bool isReferenceType, bool isString)
        {
            Name = name;
            Namespace = ns;
            FullName = fullName;
            IsEnum = isEnum;
            IsReferenceType = isReferenceType;
            IsString = isString;
        }

        public GeneratorTypeModel() 
        {
            Name = string.Empty;
            Namespace = string.Empty;
            FullName = string.Empty;
        }
    }

    public enum PropertyKind
    {
        Primitive,
        Nullable,
        DateTime,
        Enum,
        Collection,
        ComplexObject,
        XElement
    }
}
