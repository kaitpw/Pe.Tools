using Nice3point.Revit.Extensions;
using Pe.FamilyFoundry.Aggregators.Snapshots;
using Pe.FamilyFoundry.Operations;
using Pe.FamilyFoundry.OperationSettings;

namespace Pe.FamilyFoundry.OperationGroups;

/// <summary>
///     Operation group that optionally creates missing parameters, then sets their values/formulas.
///     Execution order:
///     1. AddParamsFromSettings (if CreateIfMissing=true) - creates missing family params
///     2. SetParamValues - sets formulas (default) or global values based on SetAsFormula property
///     3. SetParamValuesPerType - handles explicit per-type values and failed global value fallbacks
/// </summary>
public class AddAndSetParams(AddAndSetParamsSettings settings)
    : OperationGroup<AddAndSetParamsSettings>(
        InitializeDescription(settings),
        InitializeOperations(settings),
        settings.Parameters.Select(p => p.Name)) {
#pragma warning disable IDE0060 // Remove unused parameter
    public static string InitializeDescription(AddAndSetParamsSettings settings) =>
        $"Set a parameter within the family to a value or formula. " +
        $"By default, values are set as formulas (even simple numbers/text). Use <{nameof(ParamSettingModel.SetAsFormula)}>=false to set as values instead. " +
        $"If <{nameof(settings.OverrideExistingValues)}> is true, then existing parameter values will be overwritten. " +
        $"If <{nameof(settings.CreateFamParamIfMissing)}> is true, then a family parameter will be created " +
        $"with <{nameof(ParamDefinitionBase.Name)}>. The default values of the parameter are:" +
        $"\n\t<{nameof(ParamDefinitionBase.PropertiesGroup)}>: <{new ParamDefinitionBase { Name = "" }.PropertiesGroup.ToLabel()}>" +
        $"\n\t<{nameof(ParamDefinitionBase.DataType)}>: <{new ParamDefinitionBase { Name = "" }.DataType.ToLabel()}>>" +
        $"\n\t<{nameof(ParamDefinitionBase.IsInstance)}>: <{GetDesignation(new ParamDefinitionBase { Name = "" }.IsInstance)}>";
#pragma warning restore IDE0060 // Remove unused parameter


    private static string GetDesignation(bool isInstance) => isInstance ? "Instance" : "Type";

    private static List<IOperation> InitializeOperations(
        AddAndSetParamsSettings settings
    ) {
        var ops = new List<IOperation>();

        // 1. Optionally create missing params first
        if (settings.CreateFamParamIfMissing)
            ops.Add(new AddFamilyParams(settings));

        // 2. Set global/formula values (with per-type fallback tracking via OperationContext)
        if (settings.Parameters.Any(p => !string.IsNullOrEmpty(p.ValueOrFormula))) {
            ops.Add(new SetParamValues(settings));
            // 3. Set explicit per-type values AND handle fallbacks from SetParamValues failures
            if (!settings.DisablePerTypeFallback) ops.Add(new SetParamValuesPerType(settings));
        }

        return ops;
    }
}