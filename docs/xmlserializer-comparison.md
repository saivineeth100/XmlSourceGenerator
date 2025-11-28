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
| Element Namespaces | ✅ | ✅ | **Complete** |
| Root Namespaces | ✅ | ✅ | **Complete** |
| Namespace Prefixes | ✅ | ⚠️ | **Partial** (XNamespace used, prefixes not customizable) |
| **Type Support** ||||
| Primitives (int, string, etc.) | ✅ | ✅ | **Complete** |
| Enums | ✅ | ✅ | **Complete** |
| DateTime/DateOnly/TimeOnly | ✅ | ✅ | **Complete** |
| Custom DateTime Formats | ✅ | ✅ | **Complete** |
| Nullable Types | ✅ | ✅ | **Complete** |
| Collections (List, Array) | ✅ | ✅ | **Complete** |
| **Advanced Features** ||||
| `[XmlInclude]` | ✅ | ✅ | **Complete** |
| `[XmlEnum]` | ✅ | ✅ | **Complete** |
| `[XmlType]` | ✅ | ✅ | **Complete** |
| `[XmlAnyElement]` | ✅ | ✅ | **Complete** |
| `[XmlAnyAttribute]` | ✅ | ✅ | **Complete** |
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

### ✅ Implemented (23 features)
- All basic XML attributes (`XmlElement`, `XmlAttribute`, `XmlIgnore`, `XmlRoot`, `XmlText`, `XmlArray`, `XmlArrayItem`)
- All primitive and standard types
- Inheritance and polymorphism
- Custom DateTime formatting
- Collection handling
- Property overriding
- **XML Namespaces** (element and root namespaces)
- **`[XmlInclude]`** for declarative polymorphism
- **`[XmlEnum]`** for custom enum values
- **`[XmlType]`** for type name customization
- **`[XmlAnyElement]`** for capturing unknown elements
- **`[XmlAnyAttribute]`** for capturing unknown attributes

### ⚠️ Partially Implemented (1 feature)
- Namespace Prefixes (XNamespace used, but prefix customization not available)

### ❌ Not Implemented (4 features)
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

1. **Namespace Prefixes**: Custom prefix names not supported (uses XNamespace with default prefixes)
2. **Conditional Serialization**: `ShouldSerialize` pattern and `[DefaultValue]` not supported
3. **Choice Patterns**: `[XmlChoiceIdentifier]` not implemented

### Recommended Approach

- ✅ Use for new code with confidence
- ✅ Use for simple to complex XML scenarios (now includes polymorphism and namespaces)
- ✅ Excellent for AOT and trimming scenarios
- ⚠️ Test thoroughly when migrating from XmlSerializer
- ⚠️ Manual handling needed for conditional serialization patterns
