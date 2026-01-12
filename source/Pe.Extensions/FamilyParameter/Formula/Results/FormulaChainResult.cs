namespace Pe.Extensions.FamilyParameter.Formula;

/// <summary>
///     Result of resolving a formula chain via <see cref="FormulaAnalysis.ResolveChain" />.
/// </summary>
public class FormulaChainResult {
    public FormulaChainResult(
        Autodesk.Revit.DB.FamilyParameter ultimateSource,
        IReadOnlyList<Autodesk.Revit.DB.FamilyParameter> intermediates,
        bool sourceHasConstantFormula
    ) {
        this.UltimateSource = ultimateSource;
        this.Intermediates = intermediates;
        this.SourceHasConstantFormula = sourceHasConstantFormula;
    }

    /// <summary>The final parameter in the chain (has value, constant formula, or complex formula)</summary>
    public Autodesk.Revit.DB.FamilyParameter UltimateSource { get; }

    /// <summary>Parameters between the start and ultimate source (empty if start IS the source)</summary>
    public IReadOnlyList<Autodesk.Revit.DB.FamilyParameter> Intermediates { get; }

    /// <summary>True if the ultimate source has a constant formula that should be unwrapped</summary>
    public bool SourceHasConstantFormula { get; }
}