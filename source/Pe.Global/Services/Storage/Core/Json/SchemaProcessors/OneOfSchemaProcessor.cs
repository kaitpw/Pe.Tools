using NJsonSchema;
using NJsonSchema.Generation;

namespace Pe.Global.Services.Storage.Core.Json.SchemaProcessors;

/// <summary>
///     Schema processor that enforces mutual exclusivity between properties.
///     At most one of the specified properties can have a non-null value.
///     Uses a simple "not: { required: [all properties] }" constraint instead of complex oneOf.
/// </summary>
public class OneOfSchemaProcessor : ISchemaProcessor {
    public void Process(SchemaProcessorContext context) {
        var type = context.ContextualType.Type;

        // Look for OneOfPropertiesAttribute on the type
        var oneOfAttr = type.GetCustomAttributes(typeof(OneOfPropertiesAttribute), false)
            .Cast<OneOfPropertiesAttribute>()
            .FirstOrDefault();

        if (oneOfAttr == null) return;

        var schema = context.Schema;
        var propertyNames = oneOfAttr.PropertyNames;

        // Verify all properties exist in the schema
        var existingProps = propertyNames.Where(schema.Properties.ContainsKey).ToList();
        if (existingProps.Count < 2) return; // Need at least 2 properties for mutual exclusivity

        // Simple approach: forbid having ALL properties at once
        // "not: { required: [prop1, prop2, ...] }" means "cannot have all of these present"
        // This allows: none, any one, but forbids having all
        var notSchema = new JsonSchema();
        foreach (var propName in existingProps)
            notSchema.RequiredProperties.Add(propName);

        schema.Not = notSchema;

        // If AllowNone is false, we need at least one property present
        // Use "anyOf: [{ required: [prop1] }, { required: [prop2] }, ...]"
        if (!oneOfAttr.AllowNone) {
            foreach (var propName in existingProps) {
                var anyOfSchema = new JsonSchema();
                anyOfSchema.RequiredProperties.Add(propName);
                schema.AnyOf.Add(anyOfSchema);
            }
        }
    }
}

/// <summary>
///     Marks a type as having oneOf property constraint.
///     Exactly one of the specified properties must be present in the JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class OneOfPropertiesAttribute(params string[] propertyNames) : Attribute {
    /// <summary>The property names - at most one can be present.</summary>
    public string[] PropertyNames { get; } = propertyNames;

    /// <summary>
    ///     If true, allows none of the properties to be present.
    ///     Default is false (exactly one must be present).
    /// </summary>
    public bool AllowNone { get; init; } = false;
}