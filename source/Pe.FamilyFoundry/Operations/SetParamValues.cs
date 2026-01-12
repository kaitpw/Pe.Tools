using Pe.Extensions.FamDocument;
using Pe.Extensions.FamDocument.GetValue;
using Pe.Extensions.FamManager;
using Pe.FamilyFoundry.OperationSettings;

namespace Pe.FamilyFoundry.Operations;

/// <summary>
///     Sets parameter values or formulas based on SetAsFormula property.
///     - If SetAsFormula is true (default) → SetFormula (applies to all types, "locks" the parameter)
///     - If SetAsFormula is false → SetGlobalValue (fast path for all types at once)
///     On SetGlobalValue failure, defers to SetParamValuesPerType via GroupContext.
/// </summary>
public class SetParamValues(AddAndSetParamsSettings settings)
    : DocOperation<AddAndSetParamsSettings>(settings) {
    public override string Description =>
        "Set parameter values or formulas based on SetAsFormula property.";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        if (groupContext is null) {
            throw new InvalidOperationException(
                $"{this.Name} requires a GroupContext (must be used within an OperationGroup)");
        }

        var fm = doc.FamilyManager;
        var incomplete = groupContext.GetAllInComplete();
        var data = incomplete.Select(e => {
            var paramModel = this.Settings.Parameters.First(p => e.Key == p.Name);
            return (paramModel, e.Value);
        });

        foreach (var (p, log) in data) {
            // Skip if using ValuesPerType (handled by SetParamValuesPerType)
            if (string.IsNullOrWhiteSpace(p.ValueOrFormula)) continue;

            var parameter = fm.FindParameter(p.Name);
            if (parameter is null) {
                _ = log.Error($"Parameter '{p.Name}' not found");
                continue;
            }

            if (!this.Settings.OverrideExistingValues && doc.HasValue(parameter)) {
                _ = log.Skip("Already has value");
                continue;
            }

            var result = SetValueOrFormula(doc, parameter, p, out var errMsg);
            _ = result.NeedsFallback
                ? log.Defer($"Needs per-type fallback: {errMsg}")
                : log.Success("Set global value");
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }

    private static SetResult SetValueOrFormula(FamilyDocument doc,
        FamilyParameter param,
        ParamSettingModel paramModel,
        out string errorMessage
    ) {
        errorMessage = null;
        try {
            if (paramModel.SetAsFormula) {
                var success = doc.TrySetFormula(param, paramModel.ValueOrFormula, out errorMessage);
                return success ? SetResult.Success : SetResult.NeedsFallbackResult;
            } else {
                var success = doc.SetUnsetFormula(param, paramModel.ValueOrFormula);
                return success ? SetResult.Success : SetResult.NeedsFallbackResult;
            }
        } catch {
            // SetGlobalValue failed (e.g., Force datatype issues) - needs per-type fallback
            return SetResult.NeedsFallbackResult;
        }
    }

    private readonly struct SetResult(bool needsFallback) {
        public bool NeedsFallback { get; } = needsFallback;
        public static SetResult Success => new(false);
        public static SetResult NeedsFallbackResult => new(true);
    }
}