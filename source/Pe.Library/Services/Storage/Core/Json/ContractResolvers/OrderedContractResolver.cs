using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Pe.Library.Services.Storage.Core.Json.ContractResolvers;

/// <summary> Contract resolver that orders properties by declaration order, respecting inheritance hierarchy </summary>
internal class OrderedContractResolver : DefaultContractResolver {
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        var properties = base.CreateProperties(type, memberSerialization);

        // Build inheritance chain from base to derived
        var typeHierarchy = new List<Type>();
        var currentType = type;
        while (currentType != null && currentType != typeof(object)) {
            typeHierarchy.Insert(0, currentType);
            currentType = currentType.BaseType;
        }

        // Create ordered list: base class properties first, then derived class properties
        var orderedProperties = new List<JsonProperty>();
        var seenProperties = new HashSet<JsonProperty>();

        foreach (var t in typeHierarchy) {
            var declaredProps = t.GetProperties(BindingFlags.Public |
                                                BindingFlags.Instance |
                                                BindingFlags.DeclaredOnly)
                .OrderBy(p => p.MetadataToken) // Order by metadata token to ensure declaration order
                .ToList();

            foreach (var declaredProp in declaredProps) {
                var jsonProp = properties.FirstOrDefault(p => p.UnderlyingName == declaredProp.Name);
                if (jsonProp != null && !seenProperties.Contains(jsonProp)) {
                    _ = seenProperties.Add(jsonProp);
                    orderedProperties.Add(jsonProp);
                }
            }
        }

        return orderedProperties;
    }
}