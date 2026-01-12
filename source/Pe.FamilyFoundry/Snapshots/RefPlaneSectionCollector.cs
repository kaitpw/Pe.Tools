using Pe.Extensions.FamDocument;
using Pe.FamilyFoundry.Aggregators.Snapshots;
using Pe.FamilyFoundry.Helpers;

namespace Pe.FamilyFoundry.Snapshots;

/// <summary>
///     Collects reference planes and dimensions from family document.
///     Can ONLY collect from family document (data not accessible from project).
/// </summary>
public class RefPlaneSectionCollector : IFamilyDocCollector {
    public bool ShouldCollect(FamilySnapshot snapshot) =>
        snapshot.RefPlanesAndDims?.Data?.Count == 0 || snapshot.RefPlanesAndDims == null;

    public void Collect(FamilySnapshot snapshot, FamilyDocument famDoc) =>
        snapshot.RefPlanesAndDims = this.CollectFromFamilyDoc(famDoc);

    private SnapshotSection<RefPlaneSpec> CollectFromFamilyDoc(FamilyDocument famDoc) {
        var specs = new List<RefPlaneSpec>();
        var processedMirrorPlanes = new HashSet<(ReferencePlane, ReferencePlane)>();

        // Get all dimensions
        var dimensions = new FilteredElementCollector(famDoc.Document)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>()
            .Where(d => d is not SpotDimension)
            .ToList();

        // First pass: Process 3-plane mirror patterns (dimensions with AreSegmentsEqual) and track their side planes
        foreach (var dimension in dimensions) {
            if (dimension.References.Size == 3 && dimension.AreSegmentsEqual) {
                var spec = RefPlaneAndDimHelper.SerializeDimensionToSpec(dimension, famDoc.Document);
                if (spec != null && spec.Placement == Placement.Mirror) {
                    specs.Add(spec);

                    // Track the side planes from this mirror pattern
                    var refPlanes = GetReferencePlanes(dimension, famDoc.Document);
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
            var spec = RefPlaneAndDimHelper.SerializeDimensionToSpec(dimension, famDoc.Document);
            if (spec != null && spec.Placement != Placement.Mirror) {
                // Check if this 2-plane dimension is part of a mirror pattern
                var refPlanes = GetReferencePlanes(dimension, famDoc.Document);
                if (refPlanes.Count == 2) {
                    var isMirrorPair = processedMirrorPlanes.Contains((refPlanes[0], refPlanes[1])) ||
                                       processedMirrorPlanes.Contains((refPlanes[1], refPlanes[0]));
                    if (!isMirrorPair) specs.Add(spec);
                } else
                    specs.Add(spec);
            }
        }

        return new SnapshotSection<RefPlaneSpec> { Source = SnapshotSource.FamilyDoc, Data = specs };
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