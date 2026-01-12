namespace Pe.Extensions.FamParameter.Formula;

/// <summary>
///     Extension methods for analyzing and classifying formulas.
/// </summary>
public static class FormulaAnalysis {
    /// <summary>
    ///     Checks if a formula is a constant expression (contains no parameter references).
    ///     Constant formulas include literals like "20", "7.75\"", "60 Hz", "\"text\"",
    ///     and constant expressions like "2 A + 5 A".
    /// </summary>
    /// <returns>True if the formula has no parameter references</returns>
    public static bool IsConstant(this FamilyParameterSet parameters, string formula) {
        if (string.IsNullOrWhiteSpace(formula)) return false;
        return !parameters.GetReferencedIn(formula).Any();
    }

    /// <summary>
    ///     Checks if a formula is just a single parameter reference (no operators, no functions).
    ///     Returns the referenced parameter if so, null otherwise.
    /// </summary>
    /// <param name="parameters">The family parameter set containing all parameters</param>
    /// <param name="formula">The formula string to check</param>
    /// <returns>The single referenced parameter, or null if not a single reference</returns>
    public static FamilyParameter TryGetSingleReference(this FamilyParameterSet parameters, string formula) {
        if (string.IsNullOrWhiteSpace(formula)) return null;

        var referencedParams = parameters.GetReferencedIn(formula).ToList();
        if (referencedParams.Count != 1) return null;

        var param = referencedParams[0];
        // Formula must be EXACTLY the parameter name (trimmed)
        return formula.Trim() == param.Definition.Name ? param : null;
    }

    /// <summary>
    ///     Follows a chain of single-parameter-reference formulas to find the ultimate source.
    ///     Stops when hitting: no formula, a constant formula, or a complex formula.
    ///     Revit guarantees no cycles exist in formula chains.
    /// </summary>
    /// <param name="param">The starting parameter</param>
    /// <param name="parameters">The family parameter set containing all parameters</param>
    /// <returns>Result containing ultimate source and any intermediate parameters</returns>
    public static FormulaChainResult ResolveChain(this FamilyParameter param, FamilyParameterSet parameters) {
        var intermediates = new List<FamilyParameter>();
        var current = param;

        while (true) {
            var formula = current.Formula;

            // Terminal: no formula - current is the source
            if (string.IsNullOrWhiteSpace(formula))
                return new FormulaChainResult(current, intermediates, false);

            // Terminal: constant formula - current is the source
            if (parameters.IsConstant(formula))
                return new FormulaChainResult(current, intermediates, true);

            // Check for single parameter reference to continue chain
            var nextParam = parameters.TryGetSingleReference(formula);
            if (nextParam == null)
                // Terminal: complex formula - current is the source
                return new FormulaChainResult(current, intermediates, false);

            // Continue following the chain
            intermediates.Add(current);
            current = nextParam;
        }
    }
}