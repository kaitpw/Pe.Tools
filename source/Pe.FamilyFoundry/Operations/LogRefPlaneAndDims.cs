using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pe.Extensions.FamDocument;
using Pe.FamilyFoundry.Helpers;
using Pe.FamilyFoundry.Snapshots;
using Pe.Global.Services.Storage.Core.Json.ContractResolvers;

namespace Pe.FamilyFoundry.Operations;

/// <summary>
///     Ad-hoc operation that logs existing reference planes and dimensions in a format
///     compatible with MakeRefPlaneAndDimsSettings for copying into profile JSON.
///     <para>
///         Usage example:
///         <code>
///     queue.Add(new LogRefPlaneAndDims(storage.Output().GetFolderPath()), new LogRefPlaneAndDimsSettings());
///     </code>
///     </para>
/// </summary>
public class LogRefPlaneAndDims(string outputDir)
    : DocOperation<DefaultOperationSettings>(new DefaultOperationSettings()) {
    public string OutputPath { get; } = outputDir;
    public override string Description => "Log existing reference planes and dimensions in profile JSON format";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        var specs = new List<RefPlaneSpec>();
        var processedMirrorPlanes = new HashSet<(ReferencePlane, ReferencePlane)>();

        // Get all dimensions
        var dimensions = new FilteredElementCollector(doc)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>()
            .Where(d => d is not SpotDimension)
            .ToList();

        // First pass: Process 3-plane mirror patterns (dimensions with AreSegmentsEqual) and track their side planes
        foreach (var dimension in dimensions) {
            if (dimension.References.Size == 3 && dimension.AreSegmentsEqual) {
                var spec = RefPlaneAndDimHelper.SerializeDimensionToSpec(dimension, doc);
                if (spec != null && spec.Placement == Placement.Mirror) {
                    specs.Add(spec);

                    // Track the side planes from this mirror pattern
                    var refPlanes = GetReferencePlanes(dimension, doc);
                    if (refPlanes.Count == 3) {
                        var centerPlane = RefPlaneAndDimHelper.FindCenterPlaneGeometrically(refPlanes);
                        if (centerPlane != null) {
                            var sidePlanes = refPlanes.Where(p => p != centerPlane).ToList();
                            if (sidePlanes.Count == 2) {
                                _ = processedMirrorPlanes.Add((sidePlanes[0], sidePlanes[1]));
                                _ = processedMirrorPlanes.Add((sidePlanes[1], sidePlanes[0]));
                            }
                        }
                    }
                }
            }
        }

        // Second pass: Process remaining dimensions (2-plane that aren't part of mirror patterns)
        foreach (var dimension in dimensions) {
            var spec = RefPlaneAndDimHelper.SerializeDimensionToSpec(dimension, doc);
            if (spec != null && spec.Placement != Placement.Mirror) {
                // Check if this 2-plane dimension is part of a mirror pattern
                var refPlanes = GetReferencePlanes(dimension, doc);
                if (refPlanes.Count == 2) {
                    var isMirrorPair = processedMirrorPlanes.Contains((refPlanes[0], refPlanes[1])) ||
                                       processedMirrorPlanes.Contains((refPlanes[1], refPlanes[0]));
                    if (!isMirrorPair) specs.Add(spec);
                } else
                    specs.Add(spec);
            }
        }

        // Output JSON format
        var jsonOptions = new JsonSerializerSettings {
            Formatting = Formatting.Indented,
            ContractResolver = new RequiredAwareContractResolver(),
            Converters = [new StringEnumConverter()]
        };

        var json = JsonConvert.SerializeObject(specs, jsonOptions);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"ref-planes-dims_{timestamp}.json";
        var filePath = Path.Combine(this.OutputPath, filename);
        File.WriteAllText(filePath, json);

        var log = new LogEntry($"Wrote {specs.Count} reference plane specs to {filename}").Success();
        return new OperationLog(this.Name, [log]);
    }

    private static List<ReferencePlane> GetReferencePlanes(Dimension dim, Document doc) {
        var refPlanes = new List<ReferencePlane>();
        for (var i = 0; i < dim.References.Size; i++) {
            var reference = dim.References.get_Item(i);
            var elem = doc.GetElement(reference);
            if (elem is ReferencePlane rp && !string.IsNullOrEmpty(rp.Name))
                refPlanes.Add(rp);
        }

        return refPlanes;
    }
}