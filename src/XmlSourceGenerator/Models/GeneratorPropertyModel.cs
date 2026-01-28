using System.Collections.Generic;

namespace XmlSourceGenerator.Models
{
    public class GeneratorPropertyModel
    {
        public string Name { get; set; }
        public string TypeName { get; set; } // Fully qualified string for generated code
        
        // These can be derived or set explicitly
        public GeneratorTypeModel TypeInfo { get; set; }
        public GeneratorTypeModel? ItemTypeInfo { get; set; } // For collections
        
        // XML Metadata
        public bool IsIgnored { get; set; }
        public bool IsFlattened { get; set; }
        public bool SerializeAsAttribute { get; set; }
        public bool SerializeAsInnerText { get; set; }
        public bool IsAnyElement { get; set; }
        public bool IsAnyAttribute { get; set; }
        
        public string? XmlElementName { get; set; }
        public string? AttributeName { get; set; }
        public string? ArrayElementName { get; set; }
        public string? ArrayItemElementName { get; set; }
        public string? Namespace { get; set; }
        public bool? IsNullable { get; set; }
        
        public string[]? Formats { get; set; }
        
        public List<PolymorphicMappingModel> PolymorphicMappings { get; set; } = new List<PolymorphicMappingModel>();
        public bool IsPolymorphic => PolymorphicMappings.Count > 0;
        
        public GeneratorPropertyModel() 
        {
            Name = string.Empty;
            TypeName = string.Empty;
            TypeInfo = new GeneratorTypeModel();
        }

        public GeneratorPropertyModel(string name, string typeName, GeneratorTypeModel typeInfo)
        {
            Name = name;
            TypeName = typeName;
            TypeInfo = typeInfo;
        }

        public int Order { get; set; } = -1;
    }

    public class PolymorphicMappingModel
    {
        public string XmlName { get; set; } = string.Empty;
        public string TargetTypeName { get; set; } = string.Empty;
        public bool ImplementsIXmlStreamable { get; set; }
        public GeneratorTypeModel? TargetTypeInfo { get; set; } // Optional full info
    }
}
