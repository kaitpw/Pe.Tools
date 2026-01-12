using PeExtensions.FamDocument;

namespace Pe.FamilyFoundry.Operations;

public class PurgeModelLines(DefaultOperationSettings settings) : DocOperation<DefaultOperationSettings>(settings) {
    public override string Description => "Delete unused model lines from the family";

    public override OperationLog Execute(FamilyDocument famDoc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        // make settings later?
        var deleteGroupedLines = true;
        var deleteAlignedLines = true;

        var lines = new FilteredElementCollector(famDoc)
            .WhereElementIsNotElementType()
            .OfCategory(BuiltInCategory.OST_Lines)
            .OfClass(typeof(CurveElement))
            .OfType<CurveElement>()
            .Distinct()
            .ToDictionary(e =>
                e, e => (
                groupId: famDoc.Document.GetElement(e.Id).GroupId.Value,
                alignments: e.GetDependentElements(new ElementClassFilter(typeof(Dimension)))
                    .Select(d => famDoc.Document.GetElement(d))
                    .OfType<Dimension>()
                    .Where(d => d is not LinearDimension)
                    .Where(d => d is not AngularDimension)
                    .Where(d => d is not RadialDimension)
                    .Where(d => d is not ArcLengthDimension)
                    .Where(d => d is not SpotDimension)
                    .ToList()
            ))
            .ToList();

        var (grouped, aligned, other) = (0, 0, 0);

        foreach (var (line, (groupId, alignments)) in lines) {
            if (deleteAlignedLines && alignments.Any()) {
                var deleted = famDoc.Document.Delete(line.Id);
                if (deleted.Count != 0) aligned++;
            }

            if (deleteGroupedLines && groupId > 0) {
                var deleted = famDoc.Document.Delete(line.Id);
                if (deleted.Count != 0) grouped++;
            }

            if (!(groupId > 0) && !(alignments.Count != 0)) {
                var deleted = famDoc.Document.Delete(line.Id);
                if (deleted.Count != 0) other++;
            }
        }


        List<LogEntry> logs = [
            new LogEntry("Grouped").Success($"Deleted {grouped} aligned lines"),
            new LogEntry("Aligned").Success($"Deleted {aligned} aligned lines"),
            new LogEntry("Other").Success($"Deleted {other} other lines")
        ];


        return new OperationLog(this.Name, logs);
    }
}