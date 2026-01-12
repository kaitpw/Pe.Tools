using System.Text.RegularExpressions;

namespace Pe.Extensions.FamilyParameter.Formula;

/// <summary>
///     Low-level formula tokenization utilities.
///     Internal implementation - consumers should use higher-level methods in <see cref="FormulaReferences" />.
/// </summary>
internal static class Tokenizer {
    /// <summary>
    ///     Revit formula functions (case-insensitive).
    ///     These are excluded from parameter name extraction.
    /// </summary>
    public static readonly HashSet<string> RevitFunctions = new(StringComparer.OrdinalIgnoreCase) {
        "sin",
        "cos",
        "tan",
        "asin",
        "acos",
        "atan",
        "exp",
        "log",
        "sqrt",
        "abs",
        "if",
        "or",
        "and",
        "not",
        "text_file_lookup_obsoleted",
        "pi",
        "ConduitSize_Lookup_obsoleted",
        "round",
        "roundup",
        "rounddown",
        "size_lookup",
        "ln"
    };

    /// <summary>
    ///     Boundary chars: operators + structural formula characters.
    ///     Excludes quotes (") because they are used to delimit string literals.
    /// </summary>
    public static readonly char[] BoundaryChars = [
        '+', '-', '*', '/', '^', '=', '>', '<', ' ', '[', ']', '(', ')', ',', '\t', '\r', '\n'
    ];

    /// <summary>
    ///     Extract potential parameter name tokens from a formula string.
    ///     Returns unvalidated string tokens - prefer <see cref="FormulaReferences.GetReferencedIn" /> for validated parameter
    ///     references.
    /// </summary>
    internal static IEnumerable<string> ExtractTokens(string formula) {
        // Strip string literals (content between quotes) before tokenizing
        // This prevents "2025_12_10 18:19:51" from being parsed as parameter names
        var withoutStrings = Regex.Replace(formula, "\"[^\"]*\"", " ");
        var tokens = withoutStrings.Split(BoundaryChars, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Where(t => !IsNumericOrFunction(t)).Distinct();
    }

    /// <summary>
    ///     Check if a token is a number or a known Revit function.
    /// </summary>
    internal static bool IsNumericOrFunction(string token) {
        if (double.TryParse(token, out _)) return true;
        if (RevitFunctions.Contains(token)) return true;
        return false;
    }
}