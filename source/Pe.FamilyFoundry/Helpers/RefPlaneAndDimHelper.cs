using Pe.FamilyFoundry.Snapshots;
using System.Text.Json.Serialization;

namespace Pe.FamilyFoundry.Helpers;

[JsonConverter(typeof(JsonStringEnumConverter))]
public class PlaneQuery {
    private readonly Dictionary<string, ReferencePlane> _cache = new();
    private readonly Document _doc;

    public PlaneQuery(Document doc) => this._doc = doc;

    public ReferencePlane Get(string name) {
        if (string.IsNullOrEmpty(name)) return null;
        if (!this._cache.ContainsKey(name)) {
            this._cache[name] = new FilteredElementCollector(this._doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .FirstOrDefault(rp => rp.Name == name);
        }

        return this._cache[name];
    }

    public ReferencePlane ReCache(string name) =>
        string.IsNullOrEmpty(name)
            ? null
            : this._cache[name] = new FilteredElementCollector(this._doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .FirstOrDefault(rp => rp.Name == name);
}

public class RefPlaneAndDimHelper {
    private readonly Dictionary<string, int> _depths = new() {
        ["Center (Left/Right)"] = 0, ["Center (Front/Back)"] = 0, ["Ref. Level"] = 0
    };

    private readonly Document _doc;
    private readonly List<LogEntry> _logs;
    private readonly Dictionary<string, (double plane, double dim)> _offsetCache = new();
    private readonly PlaneQuery _query;
    private readonly Dictionary<string, int> _siblingCounts = new();

    public RefPlaneAndDimHelper(Document doc, PlaneQuery query, List<LogEntry> logs) {
        this._doc = doc;
        this._query = query;
        this._logs = logs;
    }

    public static string GetPlaneName(RefPlaneSpec spec, XYZ normal, int side) =>
        (spec.Placement, side) switch {
            (Placement.Mirror, -1) => $"{spec.Name} ({GetOrientationLabel(normal, -1)})",
            (Placement.Mirror, 1) => $"{spec.Name} ({GetOrientationLabel(normal, 1)})",
            (Placement.Mirror, 0) => GetCenterPlaneName(normal),
            _ => spec.Name
        };

    public static string GetOrientationLabel(XYZ normal, double sign) =>
        Math.Abs(normal.X) == 1.0 ? sign < 0 ? "Left" : "Right" :
        Math.Abs(normal.Y) == 1.0 ? sign < 0 ? "Back" : "Front" :
        Math.Abs(normal.Z) == 1.0 ? sign < 0 ? "Bottom" : "Top" :
        throw new ArgumentException($"Invalid normal: ({normal.X:F3}, {normal.Y:F3}, {normal.Z:F3})");

    public static string GetCenterPlaneName(XYZ normal) =>
        Math.Abs(normal.X) == 1.0 ? "Center (Left/Right)" :
        Math.Abs(normal.Y) == 1.0 ? "Center (Front/Back)" :
        Math.Abs(normal.Z) == 1.0 ? "Ref. Level" :
        throw new ArgumentException("Invalid normal, only X/Y/Z supported");

    private (double plane, double dim) GetOffsets(RefPlaneSpec spec) {
        if (this._offsetCache.TryGetValue(spec.Name, out var cached)) return cached;

        var siblingIndex = this._siblingCounts.GetValueOrDefault(spec.AnchorName, 0);
        var depth = this._depths.GetValueOrDefault(spec.AnchorName, 0) + 1;

        this._siblingCounts[spec.AnchorName] = siblingIndex + 1;
        this._depths[spec.Name] = depth;

        var planeOffset = 0.5 + (siblingIndex * 2);
        var dimOffset = spec.Placement == Placement.Mirror ? siblingIndex : depth * 0.5;
        var offsets = (planeOffset, dimOffset);
        this._offsetCache[spec.Name] = offsets;
        return offsets;
    }

    private bool SpecExists(RefPlaneSpec spec) {
        Debug.WriteLine(
            $"[SpecExists] Checking if spec exists: {spec.Name}, Anchor: {spec.AnchorName}, Placement: {spec.Placement}");

        var dims = new FilteredElementCollector(this._doc)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>()
            .Where(d => d is not SpotDimension)
            .ToList();

        Debug.WriteLine($"[SpecExists] Found {dims.Count} dimensions in document");

        // For mirror specs, track processed side planes to avoid false matches from 2-plane dims
        var processedMirrorPlanes = new HashSet<(string, string)>();

        // First pass: Find mirror patterns
        if (spec.Placement == Placement.Mirror) {
            Debug.WriteLine("[SpecExists] Checking for mirror pattern");

            foreach (var dim in dims) {
                if (dim.References.Size == 3 && dim.AreSegmentsEqual) {
                    var existing = SerializeDimensionToSpec(dim, this._doc);
                    if (existing != null && existing.Placement == Placement.Mirror) {
                        Debug.WriteLine(
                            $"[SpecExists]   Found mirror dim: Name={existing.Name}, Anchor={existing.AnchorName}, Param={existing.Parameter}");

                        // Match on Name and AnchorName; Parameter can be null/empty in serialized dimensions
                        var nameMatch = existing.Name == spec.Name;
                        var anchorMatch = existing.AnchorName == spec.AnchorName;
                        var paramMatch = string.IsNullOrEmpty(existing.Parameter) ||
                                         string.IsNullOrEmpty(spec.Parameter) ||
                                         existing.Parameter == spec.Parameter;

                        var matches = nameMatch && anchorMatch && paramMatch;
                        if (matches) {
                            Debug.WriteLine("[SpecExists]   MATCH FOUND! Returning true");
                            return true;
                        }

                        // Track side planes from this mirror pattern
                        var refPlanes = this.GetReferencePlanes(dim);
                        if (refPlanes.Count == 3) {
                            var centerPlane = FindCenterPlaneGeometrically(refPlanes);
                            if (centerPlane != null) {
                                var sidePlanes = refPlanes.Where(p => p != centerPlane).ToList();
                                if (sidePlanes.Count == 2) {
                                    _ = processedMirrorPlanes.Add((sidePlanes[0].Name, sidePlanes[1].Name));
                                    _ = processedMirrorPlanes.Add((sidePlanes[1].Name, sidePlanes[0].Name));
                                }
                            }
                        }
                    }
                }
            }

            Debug.WriteLine("[SpecExists] No mirror match found, returning false");
            return false;
        }

        // Second pass: For non-mirror specs, check for matches (Name/AnchorName are interchangeable)
        Debug.WriteLine("[SpecExists] Checking for non-mirror pattern");
        foreach (var dim in dims) {
            var existing = SerializeDimensionToSpec(dim, this._doc);
            if (existing == null || existing.Placement == Placement.Mirror) continue;

            // Skip if this 2-plane dim is part of a mirror pattern
            var refPlanes = this.GetReferencePlanes(dim);
            if (refPlanes.Count == 2) {
                var isMirrorPair = processedMirrorPlanes.Contains((refPlanes[0].Name, refPlanes[1].Name));
                if (isMirrorPair) {
                    Debug.WriteLine("[SpecExists]   Skipping dimension (part of mirror pair)");
                    continue;
                }
            }

            Debug.WriteLine(
                $"[SpecExists]   Checking dim: Name={existing.Name}, Anchor={existing.AnchorName}, Placement={existing.Placement}, Param={existing.Parameter}");

            // Parameter can be null/empty in serialized dimensions, so make it optional for matching
            var paramMatch = string.IsNullOrEmpty(existing.Parameter) ||
                             string.IsNullOrEmpty(spec.Parameter) ||
                             existing.Parameter == spec.Parameter;
            var placementMatch = existing.Placement == spec.Placement;

            // For 2-plane specs, Name and AnchorName are interchangeable
            var nameMatch = (existing.Name == spec.Name && existing.AnchorName == spec.AnchorName) ||
                            (existing.Name == spec.AnchorName && existing.AnchorName == spec.Name);

            Debug.WriteLine(
                $"[SpecExists]     nameMatch={nameMatch}, placementMatch={placementMatch}, paramMatch={paramMatch}");

            if (nameMatch && placementMatch && paramMatch) {
                Debug.WriteLine("[SpecExists]   MATCH FOUND! Returning true");
                return true;
            }
        }

        Debug.WriteLine("[SpecExists] No match found, returning false");
        return false;
    }

    private List<ReferencePlane> GetReferencePlanes(Dimension dim) {
        var refPlanes = new List<ReferencePlane>();
        for (var i = 0; i < dim.References.Size; i++) {
            var reference = dim.References.get_Item(i);
            var elem = this._doc.GetElement(reference);
            if (elem is ReferencePlane rp && !string.IsNullOrEmpty(rp.Name))
                refPlanes.Add(rp);
        }

        return refPlanes;
    }

    public static RefPlaneSpec SerializeDimensionToSpec(Dimension dim, Document doc) {
        if (dim.References.Size < 2) return null;

        // Get the reference planes from the dimension
        var refPlanes = new List<ReferencePlane>();
        for (var i = 0; i < dim.References.Size; i++) {
            var reference = dim.References.get_Item(i);
            var elem = doc.GetElement(reference);
            if (elem is ReferencePlane rp && !string.IsNullOrEmpty(rp.Name))
                refPlanes.Add(rp);
        }

        if (refPlanes.Count < 2) return null;

        // Check if this is a mirror pattern (3 planes with center)
        if (refPlanes.Count == 3) {
            // Find center plane geometrically by finding which plane is between the other two
            var centerPlane = FindCenterPlaneGeometrically(refPlanes);
            if (centerPlane != null) {
                var sidePlanes = refPlanes.Where(p => p != centerPlane).ToList();
                var normal = centerPlane.Normal;

                // Determine which side plane is on which side geometrically
                var (side1, side2) = DetermineSidePlanes(sidePlanes[0], sidePlanes[1], normal);

                // Extract base name from dimension parameter or derive from plane names
                var baseName = GetBaseNameFromDimension(dim, side1, side2);

                return new RefPlaneSpec {
                    Name = baseName,
                    AnchorName = centerPlane.Name,
                    Placement = Placement.Mirror,
                    Parameter = GetDimensionParameter(dim),
                    Strength = GetStrength(side1)
                };
            }
        }

        // Check if this is a 2-plane dimension (positive/negative pattern)
        if (refPlanes.Count == 2) {
            var plane1 = refPlanes[0];
            var plane2 = refPlanes[1];

            var placement = DeterminePlacement(plane1, plane2);
            var anchor = placement == Placement.Positive ? plane1 : plane2;
            var target = placement == Placement.Positive ? plane2 : plane1;

            return new RefPlaneSpec {
                Name = target.Name,
                AnchorName = anchor.Name,
                Placement = placement,
                Parameter = GetDimensionParameter(dim),
                Strength = GetStrength(target)
            };
        }

        return null;
    }

    public static ReferencePlane FindCenterPlaneGeometrically(List<ReferencePlane> planes) {
        if (planes.Count != 3) return null;

        // All planes should have the same normal for a valid dimension
        var normal = planes[0].Normal;

        // Calculate midpoints and their positions along the normal
        var midpoints = planes.Select(p => (p, mid: (p.BubbleEnd + p.FreeEnd) * 0.5)).ToList();

        // Project midpoints onto a line along the normal (use first plane's midpoint as origin)
        var origin = midpoints[0].mid;
        var positions = midpoints.Select(m => (m.p, pos: (m.mid - origin).DotProduct(normal))).ToList();

        // Sort by position along normal
        positions.Sort((a, b) => a.pos.CompareTo(b.pos));

        // The middle plane is the center
        return positions[1].p;
    }

    private static (ReferencePlane negativeSide, ReferencePlane positiveSide) DetermineSidePlanes(
        ReferencePlane plane1,
        ReferencePlane plane2,
        XYZ normal) {
        var mid1 = (plane1.BubbleEnd + plane1.FreeEnd) * 0.5;
        var mid2 = (plane2.BubbleEnd + plane2.FreeEnd) * 0.5;

        // Determine which is negative/positive relative to their midpoint
        var midpoint = (mid1 + mid2) * 0.5;
        var pos1 = (mid1 - midpoint).DotProduct(normal);
        var pos2 = (mid2 - midpoint).DotProduct(normal);

        return pos1 < pos2 ? (plane1, plane2) : (plane2, plane1);
    }

    private static string GetBaseNameFromDimension(Dimension dim,
        ReferencePlane side1,
        ReferencePlane side2) {
        // Try to get base name from dimension parameter first
        var paramName = GetDimensionParameter(dim);
        if (!string.IsNullOrEmpty(paramName)) {
            // If parameter name looks like it has a suffix, try to extract base
            return paramName;
        }

        // Try to find common base name from side plane names
        var name1 = side1.Name;
        var name2 = side2.Name;

        // Find longest common prefix
        var commonPrefix = "";
        var minLength = Math.Min(name1.Length, name2.Length);
        for (var i = 0; i < minLength; i++) {
            if (name1[i] == name2[i])
                commonPrefix += name1[i];
            else
                break;
        }

        // If we have a reasonable common prefix, use it
        if (commonPrefix.Length > 3) {
            // Trim trailing whitespace and opening parenthesis from common prefix
            return commonPrefix.TrimEnd(' ', '(');
        }

        // Otherwise, use the shorter name (likely the base name)
        return name1.Length <= name2.Length ? name1 : name2;
    }

    public static string GetDimensionParameter(Dimension dim) {
        try {
            var label = dim.FamilyLabel;
            return label?.Definition?.Name;
        } catch {
            return null;
        }
    }

    public static RpStrength GetStrength(ReferencePlane rp) {
        try {
            var strength = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME).AsInteger();
            return (RpStrength)strength;
        } catch {
            return RpStrength.NotARef;
        }
    }

    private static Placement DeterminePlacement(ReferencePlane anchor, ReferencePlane target) {
        var anchorMid = (anchor.BubbleEnd + anchor.FreeEnd) * 0.5;
        var targetMid = (target.BubbleEnd + target.FreeEnd) * 0.5;
        var diff = targetMid - anchorMid;
        var dot = diff.DotProduct(anchor.Normal);

        return dot > 0 ? Placement.Positive : Placement.Negative;
    }

    public void CreatePlanes(RefPlaneSpec spec) {
        Debug.WriteLine(
            $"[CreatePlanes] Checking spec: {spec.Name}, Anchor: {spec.AnchorName}, Placement: {spec.Placement}");

        if (this.SpecExists(spec)) {
            Debug.WriteLine($"[CreatePlanes] Spec exists, skipping: {spec.Name}");
            this._logs.Add(new LogEntry($"RefPlane: {spec.Name}").Skip("Already exists"));
            return;
        }

        Debug.WriteLine($"[CreatePlanes] Spec does not exist, proceeding to create: {spec.Name}");

        var anchor = this._query.Get(spec.AnchorName);
        if (anchor == null) {
            Debug.WriteLine($"[CreatePlanes] Anchor plane not found: {spec.AnchorName}");
            this._logs.Add(new LogEntry($"RefPlane: {spec.Name}").Error($"Anchor plane '{spec.AnchorName}' not found"));
            return;
        }

        Debug.WriteLine($"[CreatePlanes] Anchor plane found: {spec.AnchorName}, Id: {anchor.Id}");

        var (planeOffset, _) = this.GetOffsets(spec);
        var extent = 8.0;
        var midpoint = (anchor.BubbleEnd + anchor.FreeEnd) * 0.5;
        var normal = anchor.Normal;
        var direction = anchor.Direction;
        var cutVec = normal.CrossProduct(direction);
        var t = direction * extent;

        var planesToCreate = spec.Placement switch {
            Placement.Mirror => new[] {
                (GetPlaneName(spec, normal, -1), midpoint - (normal * planeOffset)),
                (GetPlaneName(spec, normal, 1), midpoint + (normal * planeOffset))
            },
            Placement.Positive => new[] { (spec.Name, midpoint + (normal * planeOffset)) },
            Placement.Negative => new[] { (spec.Name, midpoint - (normal * planeOffset)) },
            _ => throw new ArgumentException($"Unknown placement: {spec.Placement}")
        };

        foreach (var (name, origin) in planesToCreate) {
            try {
                if (this._query.Get(name) != null) {
                    Debug.WriteLine($"[CreatePlanes] Plane already exists, skipping: {name}");
                    continue;
                }

                Debug.WriteLine($"[CreatePlanes] Creating plane: {name}");
                var rp = this._doc.FamilyCreate.NewReferencePlane(origin + t, origin - t, cutVec, this._doc.ActiveView);
                rp.Name = name;
                _ = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME).Set((int)spec.Strength);
                _ = this._query.ReCache(name);
                this._logs.Add(new LogEntry($"RefPlane: {name}").Success("Created"));
                Debug.WriteLine($"[CreatePlanes] Successfully created plane: {name}, Id: {rp.Id}");
            } catch (Exception ex) {
                Debug.WriteLine($"[CreatePlanes] ERROR creating plane {name}: {ex.GetType().Name} - {ex.Message}");
                this._logs.Add(new LogEntry($"RefPlane: {name}").Error(ex));
            }
        }
    }

    public void CreateDimension(RefPlaneSpec spec) {
        Debug.WriteLine(
            $"[CreateDimension] Checking spec: {spec.Name}, Anchor: {spec.AnchorName}, Placement: {spec.Placement}, Parameter: {spec.Parameter}");

        if (this.SpecExists(spec)) {
            Debug.WriteLine($"[CreateDimension] Spec exists, skipping: {spec.Name}");
            this._logs.Add(new LogEntry($"Dimension: {spec.Name}").Skip("Already exists"));
            return;
        }

        Debug.WriteLine($"[CreateDimension] Spec does not exist, proceeding to create: {spec.Name}");

        var anchor = this._query.Get(spec.AnchorName);

        // If anchor doesn't exist, try to use Name as anchor (makes them interchangeable)
        if (anchor == null) {
            Debug.WriteLine($"[CreateDimension] Anchor '{spec.AnchorName}' not found, trying '{spec.Name}' as anchor");
            anchor = this._query.Get(spec.Name);
            if (anchor == null) {
                Debug.WriteLine(
                    $"[CreateDimension] Neither anchor '{spec.AnchorName}' nor '{spec.Name}' found, returning");
                return;
            }
        }

        Debug.WriteLine($"[CreateDimension] Anchor plane found: {anchor.Name}, Id: {anchor.Id}");

        var (_, dimOffset) = this.GetOffsets(spec);
        var planes = (spec.Placement switch {
            Placement.Mirror => new[] { -1, 0, 1 }.Select(i => this._query.Get(GetPlaneName(spec, anchor.Normal, i))),
            Placement.Positive => new[] { this._query.Get(spec.AnchorName), this._query.Get(spec.Name) },
            Placement.Negative => new[] { this._query.Get(spec.Name), this._query.Get(spec.AnchorName) },
            _ => throw new ArgumentException($"Unknown placement: {spec.Placement}")
        }).Where(p => p != null).ToArray();

        if (planes.Length < 2) {
            Debug.WriteLine($"[CreateDimension] Not enough reference planes found. Found: {planes.Length}");
            this._logs.Add(new LogEntry($"Dimension: {spec.Name}").Error("Reference planes not found"));
            return;
        }

        Debug.WriteLine($"[CreateDimension] Found {planes.Length} planes for dimension");

        try {
            var refArray = new ReferenceArray();
            foreach (var plane in planes) refArray.Append(plane.GetReference());

            var dimLine = CreateDimensionLine(planes[0], planes[^1], dimOffset);
            Dimension dim;

            if (spec.Placement == Placement.Mirror) {
                Debug.WriteLine($"[CreateDimension] Creating mirror dimension for: {spec.Name}");
                var refArrayMirror = new ReferenceArray();
                refArrayMirror.Append(planes[0].GetReference());
                refArrayMirror.Append(planes[^1].GetReference());
                dim = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLine, refArrayMirror);
                Debug.WriteLine($"[CreateDimension] Created mirror dimension, Id: {dim.Id}");

                Debug.WriteLine("[CreateDimension] Creating equal segments dimension");
                var dimLineEq = CreateDimensionLine(planes[0], planes[^1], dimOffset - 0.5);
                var dimEq = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLineEq, refArray);
                dimEq.AreSegmentsEqual = true;
                Debug.WriteLine($"[CreateDimension] Created equal segments dimension, Id: {dimEq.Id}");
            } else {
                Debug.WriteLine($"[CreateDimension] Creating non-mirror dimension for: {spec.Name}");
                dim = this._doc.FamilyCreate.NewLinearDimension(this._doc.ActiveView, dimLine, refArray);
                Debug.WriteLine($"[CreateDimension] Created dimension, Id: {dim.Id}");
            }

            if (!string.IsNullOrEmpty(spec.Parameter)) {
                Debug.WriteLine($"[CreateDimension] Setting parameter: {spec.Parameter}");
                dim.FamilyLabel = this._doc.FamilyManager.get_Parameter(spec.Parameter);
            }

            this._logs.Add(new LogEntry($"Dimension: {spec.Name}").Success("Created"));
            Debug.WriteLine($"[CreateDimension] Successfully completed dimension: {spec.Name}");
        } catch (Exception ex) {
            Debug.WriteLine(
                $"[CreateDimension] ERROR creating dimension {spec.Name}: {ex.GetType().Name} - {ex.Message}");
            Debug.WriteLine($"[CreateDimension] Stack trace: {ex.StackTrace}");
            this._logs.Add(new LogEntry($"Dimension: {spec.Name}").Error(ex));
        }
    }

    private static Line CreateDimensionLine(ReferencePlane rp1, ReferencePlane rp2, double offset) {
        var normal = rp1.Normal;
        var direction = rp1.Direction;
        var distanceAlongNormal =
            (((rp2.BubbleEnd + rp2.FreeEnd) * 0.5) - ((rp1.BubbleEnd + rp1.FreeEnd) * 0.5)).DotProduct(normal);

        var p1 = rp1.BubbleEnd + (direction * offset);
        var p2 = p1 + (normal * distanceAlongNormal);

        return Line.CreateBound(p1, p2);
    }
}