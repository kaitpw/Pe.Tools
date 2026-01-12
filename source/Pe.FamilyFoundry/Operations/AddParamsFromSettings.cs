using Pe.FamilyFoundry.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace Pe.FamilyFoundry.Operations;

/// <summary>
///     Creates missing family parameters from AddAndSetParamsSettings.
///     Uses the PropertiesGroup/DataType/IsInstance from ParamSettingModel.
///     Only used when CreateIfMissing=true in the settings.
/// </summary>
public class AddFamilyParams(AddAndSetParamsSettings settings)
    : DocOperation<AddAndSetParamsSettings>(settings) {
    public override string Description =>
        "Create missing family parameters from AddAndSetParams settings.";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        var logs = new List<LogEntry>();
        var fm = doc.FamilyManager;

        // Get all unique parameter names that need to be created
        var paramsToCreate = new Dictionary<string, (ForgeTypeId group, ForgeTypeId dataType, bool isInstance)>();

        foreach (var p in this.Settings.Parameters) {
            if (fm.FindParameter(p.Name) is not null) continue; // Already exists
            paramsToCreate[p.Name] = (p.PropertiesGroup, p.DataType, p.IsInstance);
        }

        // Create the parameters
        foreach (var (name, (group, dataType, isInstance)) in paramsToCreate) {
            try {
                _ = doc.AddFamilyParameter(name, group, dataType, isInstance);
                logs.Add(new LogEntry(name).Success("Created"));
            } catch (Exception ex) {
                logs.Add(new LogEntry(name).Error(ex));
            }
        }

        return new OperationLog(this.Name, logs);
    }
}