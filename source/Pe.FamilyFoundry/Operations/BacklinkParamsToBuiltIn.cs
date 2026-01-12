using Pe.FamilyFoundry.OperationSettings;
using Pe.Extensions.FamDocument;
using Pe.Extensions.FamManager;
using Pe.Extensions.FamParameter;

namespace Pe.FamilyFoundry.Operations;

/// <summary>
///     Creates backlinks from built-in parameters to their mapped shared parameter targets.
///     Sets formulas like: Model = PE_G___Model, so the built-in derives from the shared param.
/// </summary>
public class BacklinkParamsToBuiltIn(MapParamsSettings settings)
    : DocOperation<MapParamsSettings>(settings) {
    public override string Description => "Create backlinks from built-in params to their mapped targets";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext _) {
        var fm = doc.FamilyManager;

        // Find first built-in in CurrNames (priority order) and backlink it
        var data = this.Settings.MappingData
            .Select(m => (
                newParam: fm.FindParameter(m.NewName),
                currParams: m.CurrNames.Select(fm.FindParameter)
                    .Where(p => p is not null)
                    .Where(p => p.IsBuiltInParameter()).ToList()
            ))
            .Where(m => m.newParam is not null)
            .Where(m => m.currParams.Any())
            .ToList();

        var logs = new List<LogEntry>();
        foreach (var (newParam, currParams) in data) {
            foreach (var currParam in currParams) {
                var success = doc.TrySetFormulaFast(currParam, newParam.Definition.Name, out var err);
                var log = new LogEntry($"Backlink {newParam.Definition.Name} → {currParam.Definition.Name}");
                logs.Add(success
                    ? log.Success("Successfully backlinked")
                    : log.Error(err ?? "Failed to set formula"));
                break; // Only backlink first matching built-in per mapping
            }
        }

        return new OperationLog(this.Name, logs);
    }
}