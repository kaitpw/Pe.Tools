namespace Pe.Extensions.FamilyParameter.Formula;

/// <summary>
///     Result of cycle detection when checking if a formula would create a circular reference.
/// </summary>
public class CycleDetectionResult {
    public CycleDetectionResult(
        bool wouldCycle,
        Autodesk.Revit.DB.FamilyParameter directReference,
        IReadOnlyList<Autodesk.Revit.DB.FamilyParameter> cyclePath
    ) {
        this.WouldCycle = wouldCycle;
        this.DirectReference = directReference;
        this.CyclePath = cyclePath;
    }

    /// <summary>True if setting the formula would create a cycle</summary>
    public bool WouldCycle { get; }

    /// <summary>The parameter directly referenced in the formula that leads to the cycle (null if no cycle)</summary>
    public Autodesk.Revit.DB.FamilyParameter DirectReference { get; }

    /// <summary>
    ///     The path of parameters forming the cycle, from DirectReference back to the target.
    ///     Example: If setting A.Formula = B and B.Formula = C and C.Formula = A,
    ///     DirectReference = B, CyclePath = [B, C, A]
    /// </summary>
    public IReadOnlyList<Autodesk.Revit.DB.FamilyParameter> CyclePath { get; }

    public static CycleDetectionResult NoCycle => new(false, null, null);

    /// <summary>
    ///     Formats the cycle path as a readable string for error messages.
    ///     Example: "B → C → A" or "B (references A directly)"
    /// </summary>
    public string FormatCyclePath() {
        if (!this.WouldCycle || this.CyclePath == null || this.CyclePath.Count == 0)
            return string.Empty;

        var names = this.CyclePath.Select(p => p.Definition.Name).ToList();
        return string.Join(" → ", names);
    }
}