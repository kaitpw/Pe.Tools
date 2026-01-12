namespace Pe.Extensions.FamParameter.Formula;

/// <summary>
///     Cycle detection utilities for formula dependency graphs.
/// </summary>
public static class FormulaCycleDetection {
    /// <summary>
    ///     Checks if setting a formula on a target parameter would create a cycle.
    ///     A cycle occurs if any parameter referenced in the formula (transitively) depends on the target.
    /// </summary>
    /// <param name="targetParam">The parameter that would receive the formula</param>
    /// <param name="formula">The formula to check</param>
    /// <param name="familyManager">The family manager containing all parameters</param>
    /// <returns>Result with cycle details if a cycle would be created, or WouldCycle=false if safe</returns>
    public static CycleDetectionResult DetectCycle(
        FamilyParameter targetParam,
        string formula,
        FamilyManager familyManager
    ) {
        if (string.IsNullOrWhiteSpace(formula))
            return CycleDetectionResult.NoCycle;

        var referencedParams = familyManager.Parameters.GetReferencedIn(formula).ToList();

        foreach (var param in referencedParams) {
            var path = new List<FamilyParameter>();
            if (FindCyclePath(param, targetParam, familyManager.Parameters, path, [])) {
                // Path goes: param -> ... -> target
                // Full cycle is: target --(formula)--> param -> ... -> target
                return new CycleDetectionResult(true, param, path);
            }
        }

        return CycleDetectionResult.NoCycle;
    }

    /// <summary>
    ///     Recursively finds the path from 'current' to 'target' through formula dependencies.
    ///     Returns true if a path exists, populating 'path' with the parameters in the cycle.
    /// </summary>
    private static bool FindCyclePath(
        FamilyParameter current,
        FamilyParameter target,
        FamilyParameterSet parameters,
        List<FamilyParameter> path,
        HashSet<ElementId> visited
    ) {
        path.Add(current);

        if (current.Id == target.Id)
            return true;

        if (!visited.Add(current.Id)) {
            path.RemoveAt(path.Count - 1);
            return false;
        }

        var formula = current.Formula;
        if (string.IsNullOrWhiteSpace(formula)) {
            path.RemoveAt(path.Count - 1);
            return false;
        }

        var dependencies = parameters.GetReferencedIn(formula);
        foreach (var dep in dependencies) {
            if (FindCyclePath(dep, target, parameters, path, visited))
                return true;
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }
}