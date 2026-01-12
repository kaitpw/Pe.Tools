using Pe.FamilyFoundry.Helpers;
using Pe.FamilyFoundry.OperationSettings;
using Pe.Extensions.FamDocument;

namespace Pe.FamilyFoundry.OperationGroups;

public class MakeRefPlaneAndDims(MakeRefPlaneAndDimsSettings settings) : OperationGroup<MakeRefPlaneAndDimsSettings>(
    "Make reference planes and dimensions for the family",
    InitializeOperations(settings),
    settings.Specs.Select(s => s.ToString())) {
    private static List<IOperation> InitializeOperations(
        MakeRefPlaneAndDimsSettings settings) {
        var sharedHelper = new SharedHelper();
        return [
            new MakeRefPlanes(settings, sharedHelper),
            new MakeDimensions(settings, sharedHelper)
        ];
    }
}

public class SharedHelper {
    public PlaneQuery Query { get; set; }
    public RefPlaneAndDimHelper Helper { get; set; }
    public List<LogEntry> Logs { get; set; }
}

public class MakeRefPlanes : DocOperation<MakeRefPlaneAndDimsSettings> {
    private readonly SharedHelper _shared;

    public MakeRefPlanes(MakeRefPlaneAndDimsSettings settings, SharedHelper shared) : base(settings) =>
        this._shared = shared;

    public override string Description => "Make reference planes for the family";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        this._shared.Logs = new List<LogEntry>();
        this._shared.Query = new PlaneQuery(doc);
        this._shared.Helper = new RefPlaneAndDimHelper(doc, this._shared.Query, this._shared.Logs);

        foreach (var spec in this.Settings.Specs) this._shared.Helper.CreatePlanes(spec);

        return new OperationLog(this.Name, this._shared.Logs);
    }
}

public class MakeDimensions : DocOperation<MakeRefPlaneAndDimsSettings> {
    private readonly SharedHelper _shared;

    public MakeDimensions(MakeRefPlaneAndDimsSettings settings, SharedHelper shared) : base(settings) =>
        this._shared = shared;

    public override string Description => "Make dimensions for the family";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        foreach (var spec in this.Settings.Specs) this._shared.Helper.CreateDimension(spec);

        return new OperationLog(this.Name, this._shared.Logs);
    }
}