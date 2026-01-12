namespace Pe.Global.Services.Storage.Core.Json.RevitTypes;

/// <summary>
///     Discriminator for ForgeTypeId properties that determines which provider and label map to use.
/// </summary>
public enum ForgeKind {
    /// <summary> Spec types (data types like Length, Area, Volume, etc.) </summary>
    Spec,

    /// <summary> Group types (property groups like Dimensions, Constraints, etc.) </summary>
    Group
}

/// <summary>
///     Marks a ForgeTypeId property to indicate whether it represents a spec type or a group type.
///     This discriminator allows the schema processor to apply the correct provider for autocomplete.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ForgeKindAttribute(ForgeKind kind) : Attribute {
    public ForgeKind Kind { get; } = kind;
}