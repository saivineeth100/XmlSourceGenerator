using Microsoft.CodeAnalysis;

namespace XmlSourceGenerator.Helpers
{
    /// <summary>
    /// Helper methods for property traversal and filtering.
    /// </summary>
    internal static class PropertyHelpers
    {
        /// <summary>
        /// Gets all properties from class hierarchy, respecting overrides.
        /// Base properties are returned first, then derived.
        /// </summary>
        public static IEnumerable<IPropertySymbol> GetAllProperties(INamedTypeSymbol symbol)
        {
            var props = new List<IPropertySymbol>();
            var propIndex = new Dictionary<string, int>();
            var hierarchy = new Stack<INamedTypeSymbol>();

            var current = symbol;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                hierarchy.Push(current);
                current = current.BaseType;
            }

            foreach (var type in hierarchy)
            {
                foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.IsStatic || prop.DeclaredAccessibility != Accessibility.Public || prop.IsIndexer)
                        continue;

                    if (propIndex.TryGetValue(prop.Name, out int index))
                    {
                        // Override or shadow: replace with most derived version
                        props[index] = prop;
                    }
                    else
                    {
                        // New property: add to end
                        propIndex[prop.Name] = props.Count;
                        props.Add(prop);
                    }
                }
            }
            return props;
        }

        /// <summary>
        /// Determines if a type is a primitive for XML serialization.
        /// </summary>
        public static bool IsPrimitive(ITypeSymbol type)
        {
            return type.SpecialType == SpecialType.System_String || 
                   type.SpecialType == SpecialType.System_Int32 || 
                   type.SpecialType == SpecialType.System_Int64 ||
                   type.SpecialType == SpecialType.System_Double ||
                   type.SpecialType == SpecialType.System_Single ||
                   type.SpecialType == SpecialType.System_Decimal ||
                   type.SpecialType == SpecialType.System_Boolean ||
                   type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }
    }
}
