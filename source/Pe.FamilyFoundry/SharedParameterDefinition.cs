namespace Pe.FamilyFoundry;

public record SharedParameterDefinition(
    ExternalDefinition ExternalDefinition,
    ForgeTypeId GroupTypeId,
    bool IsInstance
);