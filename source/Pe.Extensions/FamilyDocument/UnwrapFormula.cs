using Pe.Extensions.FamilyDocument.GetValue;
using Pe.Extensions.FamilyParameter.Formula;

namespace Pe.Extensions.FamilyDocument;

/// <summary>
///     Result of an unwrap operation.
/// </summary>
public class UnwrapResult {
    public UnwrapResult(bool wasUnwrapped,
        FamilyParameter ultimateSource,
        IReadOnlyList<FamilyParameter> deletedIntermediates) {
        this.WasUnwrapped = wasUnwrapped;
        this.UltimateSource = ultimateSource;
        this.DeletedIntermediates = deletedIntermediates;
    }

    public bool WasUnwrapped { get; }
    public FamilyParameter UltimateSource { get; }
    public IReadOnlyList<FamilyParameter> DeletedIntermediates { get; }

    public static UnwrapResult NoChange { get; } = new(false, null, Array.Empty<FamilyParameter>());
}

public static class FamilyDocumentUnwrapFormula {
    /// <summary>
    ///     Attempts to unwrap a parameter's formula if it's a constant or single-param reference chain.
    ///     <list type="bullet">
    ///         <item>Constant formula (e.g., "20", "7.75\"", "60 Hz"): Sets value directly, clears formula</item>
    ///         <item>
    ///             Chain reference (e.g., = Model): Resolves to ultimate source, sets value,
    ///             backlinks if source is built-in, and cleans up intermediate parameters
    ///         </item>
    ///     </list>
    /// </summary>
    /// <param name="doc">The family document</param>
    /// <param name="param">The parameter to potentially unwrap</param>
    /// <returns>Result indicating if unwrap occurred and what was affected</returns>
    public static UnwrapResult TryUnwrapFormula(this FamilyDocument doc, FamilyParameter param) {
        var formula = param.Formula;
        if (string.IsNullOrWhiteSpace(formula))
            return UnwrapResult.NoChange;

        var fm = doc.FamilyManager;

        // Get referenced parameters ONCE - don't iterate all params multiple times
        var referencedParams = fm.Parameters.GetReferencedIn(formula).ToList();

        // Case 1: Constant formula (no parameter references) - unwrap to value
        if (referencedParams.Count == 0)
            return UnwrapConstantFormula(doc, param);

        // Case 2: Single param reference where formula IS the param name - resolve chain
        if (referencedParams.Count == 1 && formula.Trim() == referencedParams[0].Definition.Name)
            return UnwrapFormulaChain(doc, param, referencedParams[0]);

        // Case 3: Complex formula (multiple refs or operators) - leave as-is
        return UnwrapResult.NoChange;
    }

    /// <summary>
    ///     Unwraps a constant formula by setting the evaluated value and clearing the formula.
    /// </summary>
    private static UnwrapResult UnwrapConstantFormula(FamilyDocument doc, FamilyParameter param) {
        // Get the current evaluated value (Revit has already computed it)
        var value = doc.GetValue(param);
        if (value == null)
            return UnwrapResult.NoChange;

        _ = doc.UnsetFormula(param);
        _ = doc.SetUnsetFormula(param, value);

        return new UnwrapResult(true, param, Array.Empty<FamilyParameter>());
    }

    /// <summary>
    ///     Unwraps a formula chain by resolving to the ultimate source, copying its value,
    ///     optionally backlinking, and cleaning up intermediates.
    /// </summary>
    /// <param name="doc">The family document</param>
    /// <param name="param">The parameter to unwrap</param>
    /// <param name="firstRef">The first referenced parameter (already resolved by caller)</param>
    private static UnwrapResult
        UnwrapFormulaChain(FamilyDocument doc, FamilyParameter param, FamilyParameter firstRef) {
        var fm = doc.FamilyManager;

        // Build param name lookup ONCE for chain resolution
        var paramLookup = fm.Parameters
            .OfType<FamilyParameter>()
            .ToDictionary(p => p.Definition.Name);

        // Resolve chain using the lookup (avoids repeated iteration)
        var (ultimateSource, intermediates, hasConstantFormula) =
            ResolveChainFast(firstRef, paramLookup);

        var value = doc.GetValue(ultimateSource);
        if (value == null)
            return new UnwrapResult(false, ultimateSource, Array.Empty<FamilyParameter>());

        // Clear our formula and set value globally
        _ = doc.UnsetFormula(param);
        _ = doc.SetUnsetFormula(param, value);

        // If ultimate source is built-in, set backlink: source.Formula = param.Name
        if (ParameterUtils.IsBuiltInParameter(ultimateSource.Id) &&
            ultimateSource.IsInstance == param.IsInstance) {
            try {
                var success = doc.TrySetFormulaFast(ultimateSource, param.Definition.Name, out var errorMessage);
                if (!success) throw new Exception(errorMessage);
            } catch (InvalidOperationException) {
                // Backlink failed - continue anyway
            }
        }

        // Skip intermediate cleanup for performance - it's expensive and rarely needed
        // The DeleteUnusedParams operation handles this more efficiently
        return new UnwrapResult(true, ultimateSource, Array.Empty<FamilyParameter>());
    }

    /// <summary>
    ///     Fast chain resolution using pre-built parameter lookup.
    ///     Avoids repeated iteration over all parameters.
    /// </summary>
    private static (FamilyParameter ultimateSource, List<FamilyParameter> intermediates, bool hasConstantFormula)
        ResolveChainFast(FamilyParameter start, Dictionary<string, FamilyParameter> paramLookup) {
        var intermediates = new List<FamilyParameter>();
        var current = start;

        while (true) {
            var formula = current.Formula;

            // Terminal: no formula
            if (string.IsNullOrWhiteSpace(formula))
                return (current, intermediates, false);

            // Check if formula is a single parameter reference (just the name, no operators)
            var trimmed = formula.Trim();
            if (paramLookup.TryGetValue(trimmed, out var nextParam)) {
                // It's a direct reference - continue chain
                intermediates.Add(current);
                current = nextParam;
                continue;
            }

            // Terminal: constant formula or complex formula
            // (If it's not in paramLookup, it's either a constant or has operators)
            var isConstant = !paramLookup.Keys.Any(name =>
                current.Definition.Name != name && trimmed.Contains(name));
            return (current, intermediates, isConstant);
        }
    }
}