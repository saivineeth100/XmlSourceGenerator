using Microsoft.CodeAnalysis;

namespace XmlSourceGenerator.Helpers
{
    /// <summary>
    /// Helper methods for property traversal and filtering.
    /// </summary>
    public static class PropertyHelpers
    {
        /// <summary>
        /// Gets all properties from class hierarchy, respecting overrides.
        /// Base properties are returned first, then derived.
        /// </summary>
        public static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol symbol)
        {
            var members = new List<ISymbol>();
            var memberIndex = new Dictionary<string, int>();
            var hierarchy = new Stack<INamedTypeSymbol>();

            var current = symbol;
            while (current != null && current.SpecialType != SpecialType.System_Object)
            {
                hierarchy.Push(current);
                current = current.BaseType;
            }

            foreach (var type in hierarchy)
            {
                foreach (var member in type.GetMembers())
                {
                    if (member is IPropertySymbol prop)
                    {
                        if (prop.IsStatic || prop.DeclaredAccessibility != Accessibility.Public || prop.IsIndexer)
                            continue;
                        if (prop.IsReadOnly)
                            continue;
                        
                        AddOrUpdateMember(members, memberIndex, prop);
                    }
                    else if (member is IFieldSymbol field)
                    {
                        if (field.IsStatic || field.DeclaredAccessibility != Accessibility.Public || field.IsConst)
                            continue;
                        
                        AddOrUpdateMember(members, memberIndex, field);
                    }
                }
            }
            return members;
        }

        private static void AddOrUpdateMember(List<ISymbol> members, Dictionary<string, int> memberIndex, ISymbol member)
        {
            if (memberIndex.TryGetValue(member.Name, out int index))
            {
                members[index] = member;
            }
            else
            {
                memberIndex[member.Name] = members.Count;
                members.Add(member);
            }
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
