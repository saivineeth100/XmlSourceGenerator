; Shipped analyzer releases
; <https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md>

## 1.0

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|-------
XSG001  | Usage       | Error    | XSG001_ClassMustBePartial, [Documentation](https://github.com/saivineeth100/XmlSourceGenerator/blob/main/docs/diagnostics.md#xsg001-class-must-be-partial)
XSG002  | Usage       | Error    | XSG002_ClassMustHaveParameterlessConstructor, [Documentation](https://github.com/saivineeth100/XmlSourceGenerator/blob/main/docs/diagnostics.md#xsg002-class-must-have-parameterless-constructor)
XSG003  | Usage       | Error    | XSG003_XmlAttributeOnComplexType, [Documentation](https://github.com/saivineeth100/XmlSourceGenerator/blob/main/docs/diagnostics.md#xsg003-xmlattribute-used-on-complex-type)
XSG004  | Usage       | Error    | XSG004_XmlListElementOnNonCollection, [Documentation](https://github.com/saivineeth100/XmlSourceGenerator/blob/main/docs/diagnostics.md#xsg004-xmlstreamlistelement-used-on-non-collection-type)
XSG005  | Usage       | Error    | XSG005_DuplicateXmlName, [Documentation](https://github.com/saivineeth100/XmlSourceGenerator/blob/main/docs/diagnostics.md#xsg005-duplicate-xml-name)
XSG006  | Performance | Warning  | XSG006_ReflectionFallbackWarning, [Documentation](https://github.com/saivineeth100/XmlSourceGenerator/blob/main/docs/diagnostics.md#xsg006-reflection-fallback-used)
