# XmlSourceGenerator vs XmlSerializer Feature Comparison

## Feature Parity Matrix

| Feature | XmlSerializer | XmlSourceGenerator | Status |
|:--------|:-------------|:-------------------|:-------|
| **Basic Serialization** | ✅ | ✅ | **Complete** |
| **Property Attributes** ||||
| `[XmlElement]` | ✅ | ✅ | **Complete** |
| `[XmlAttribute]` | ✅ | ✅ | **Complete** |
| `[XmlIgnore]` | ✅ | ✅ | **Complete** |
| `[XmlRoot]` | ✅ | ✅ | **Complete** |
| `[XmlText]` | ✅ | ✅ | **Complete** |
| `[XmlArray]` | ✅ | ✅ | **Complete** |
| `[XmlArrayItem]` | ✅ | ✅ | **Complete** |
| **Namespace Support** ||||
| Element Namespaces | ✅ | ⚠️ | **Partial** (defined but not used) |
| Root Namespaces | ✅ | ⚠️ | **Partial** (defined but not used) |
| Namespace Prefixes | ✅ | ❌ | **Missing** |
| **Type Support** ||||
| Primitives (int, string, etc.) | ✅ | ✅ | **Complete** |
| Enums | ✅ | ✅ | **Complete** |
| DateTime/DateOnly/TimeOnly | ✅ | ✅ | **Complete** |
| Custom DateTime Formats | ✅ | ✅ | **Complete** |
| Nullable Types | ✅ | ✅ | **Complete** |
| Collections (List, Array) | ✅ | ✅ | **Complete** |
| **Advanced Features** ||||
| `[XmlInclude]` | ✅ | ❌ | **Missing** |
| `[XmlEnum]` | ✅ | ❌ | **Missing** |
| `[XmlType]` | ✅ | ❌ | **Missing** |
| `[XmlAnyElement]` | ✅ | ❌ | **Missing** |
| `[XmlAnyAttribute]` | ✅ | ❌ | **Missing** |
| `[XmlChoiceIdentifier]` | ✅ | ❌ | **Missing** |
| **Polymorphism** ||||
| Type Inheritance | ✅ | ✅ | **Complete** |
| Property Override | ✅ | ✅ | **Complete** |
| Runtime Type Mapping | ✅ | ✅ | **Complete** (via `[XmlStreamListElement]`) |
| **Special Behaviors** ||||
| `ShouldSerializeXXX()` | ✅ | ❌ | **Missing** |
| `XXXSpecified` Pattern | ✅ | ❌ | **Missing** |
| `[DefaultValue]` | ✅ | ❌ | **Missing** |
| **Performance** ||||
| Reflection-Based | ✅ | ❌ | N/A (compile-time) |
| Source Generation | ❌ | ✅ | **Advantage** |
| AOT Compatible | ⚠️ | ✅ | **Advantage** |
| Trimming Safe | ⚠️ | ✅ | **Advantage** |

## Summary

### ✅ Implemented (18 features)
- All basic XML attributes
- All primitive and standard types
- Inheritance and polymorphism
- Custom DateTime formatting
- Collection handling
- Property overriding

### ⚠️ Partially Implemented (2 features)
- XML Namespaces (attributes exist, code generation incomplete)
- Namespace Prefixes

### ❌ Not Implemented (9 features)
- `[XmlInclude]` for declarative polymorphism
- `[XmlEnum]` for custom enum values
- `[XmlType]` for type name customization
- `[XmlAnyElement]` for unknown elements
- `[XmlAnyAttribute]` for unknown attributes  
- `[XmlChoiceIdentifier]` for choice patterns
- `ShouldSerializeXXX()` methods
- `XXXSpecified` pattern
- `[DefaultValue]` attribute

## XmlSourceGenerator Advantages

1. **Compile-Time Code Generation**: No reflection overhead at runtime
2. **Full AOT Support**: Works with Native AOT compilation
3. **Trimming Safe**: No dynamic type discovery
4. **Visible Generated Code**: Easy to debug and understand
5. **Type Safety**: Compile-time errors instead of runtime exceptions

## Migration Path

For most use cases, XmlSourceGenerator is a **drop-in replacement** for XmlSerializer. The main compatibility issues are:

1. **Namespaces**: Currently not fully implemented (in progress)
2. **Advanced Attributes**: `XmlInclude`, `XmlEnum`, etc. need alternatives
3. **Conditional Serialization**: `ShouldSerialize` pattern not supported

### Recommended Approach

- ✅ Use for new code
- ✅ Use for simple to moderate XML scenarios
- ⚠️ Test thoroughly when migrating from XmlSerializer
- ❌ Avoid for code heavily using `XmlInclude` or `XmlAnyElement` (until implemented)
